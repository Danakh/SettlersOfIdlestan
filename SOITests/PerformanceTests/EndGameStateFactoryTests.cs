using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOIBench;
using Xunit;

namespace SOITests.PerformanceTests;

/// <summary>
/// Ces tests ne peuvent pas tourner en parallèle du reste de l'assembly.
/// <see cref="EndGameStateFactory"/> achète toute la carte de prestige, et chaque achat lève
/// <c>PrestigeMap.DefaultMap.VertexPurchased</c> — un événement porté par un objet <b>statique</b>
/// auquel s'abonne le <c>PrestigeModifierProvider</c> de <i>chaque</i> MainGameController vivant
/// (voir PrestigeMapController.PurchaseVertex). Avec 16 threads de test, ces événements atterrissent
/// dans les agrégateurs de modificateurs d'autres tests en cours et les font échouer par
/// intermittence. En jeu le problème ne se pose pas : les providers sont Dispose() à chaque
/// changement d'île et une seule partie tourne à la fois.
/// </summary>
[CollectionDefinition(EndGameFixtureCollection.Name, DisableParallelization = true)]
public class EndGameFixtureCollection
{
    public const string Name = "EndGameFixture";
}

/// <summary>
/// Valide le générateur d'états de fin de partie de SOIBench. Ces tests ne mesurent aucun temps —
/// un seuil de durée serait instable en CI et ne dirait rien d'utile. Ils vérifient que le cas de
/// charge est bien celui qu'on croit mesurer : la bonne taille, les règles du jeu respectées, un
/// état sérialisable, et une simulation qui fait réellement quelque chose.
/// </summary>
[Collection(EndGameFixtureCollection.Name)]
public class EndGameStateFactoryTests
{
    /// <summary>Petit gabarit : suffisant pour exercer toutes les étapes, assez rapide pour la CI.</summary>
    private static EndGameIslandOptions SmallOptions(int cityCount = 40) => new()
    {
        Seed = 4242,
        WorldId = 6,
        TargetCityCount = cityCount,
        SurfaceRadius = 8,
        UnderworldRadius = 6,
        BuildingLevel = 3,
    };

    [Fact]
    public void Build_ReachesTheRequestedCityCount()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions());

        Assert.Equal(40, fixture.CityCount);
        Assert.Equal(40, fixture.Controller.CurrentMainState!.CurrentWorldState!.GetAllCities().Count());
    }

    [Fact]
    public void Build_IsDeterministicForAGivenSeed()
    {
        var first = EndGameStateFactory.Build(SmallOptions());
        var second = EndGameStateFactory.Build(SmallOptions());

        Assert.Equal(first.CityCount, second.CityCount);
        Assert.Equal(first.RoadCount, second.RoadCount);
        Assert.Equal(first.BuildingCount, second.BuildingCount);
        Assert.Equal(first.HexCount, second.HexCount);
        Assert.Equal(CityPositions(first), CityPositions(second));
    }

    /// <summary>
    /// PlayerCityShare pilote la taille du territoire du joueur (Voronoï pondéré) puis l'ordre de
    /// pose des villes. La part exacte n'est pas garantie — elle plafonne dès que le territoire du
    /// joueur est saturé, la distance minimale entre ses propres villes (3) étant plus grande
    /// qu'entre villes de civilisations différentes (2). Ce qui doit tenir, c'est la monotonie : plus
    /// de part demandée, plus de villes pour le joueur.
    /// </summary>
    [Fact]
    public void Build_HonoursThePlayerShare()
    {
        var low = SmallOptions(60);
        low.PlayerCityShare = 0.2;
        var high = SmallOptions(60);
        high.PlayerCityShare = 0.8;

        var withLowShare = EndGameStateFactory.Build(low);
        var withHighShare = EndGameStateFactory.Build(high);

        Assert.True(withHighShare.PlayerCityCount > withLowShare.PlayerCityCount,
            $"part 0,8 → {withHighShare.PlayerCityCount} villes joueur, part 0,2 → {withLowShare.PlayerCityCount}.");
        Assert.True(withHighShare.PlayerCityCount > withHighShare.CityCount / withHighShare.CivilizationCount,
            "le joueur n'a pas plus que sa part égalitaire avec une part demandée de 0,8.");
    }

    [Fact]
    public void Build_PopulatesBothLayers()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions());
        var world = fixture.Controller.CurrentMainState!.CurrentWorldState!;

        Assert.Contains(fixture.Layers, l => l.Z == IslandMap.SurfaceLayer && l.HexCount > 0);
        Assert.Contains(fixture.Layers, l => l.Z == LayerState.UnderworldZ && l.HexCount > 0);
        Assert.Contains(world.GetAllCities(), c => c.Position.Z == LayerState.UnderworldZ);
    }

    /// <summary>
    /// Le générateur passe par CityBuilderController.CreateCityFree, donc les distances minimales du
    /// jeu doivent tenir. Si ce test casse, c'est que le cas de charge contient des configurations
    /// qu'une vraie partie ne peut pas produire — et les mesures ne veulent plus rien dire.
    /// </summary>
    [Fact]
    public void Build_RespectsCityPlacementDistances()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions());
        var world = fixture.Controller.CurrentMainState!.CurrentWorldState!;
        var cityBuilder = fixture.Controller.CityBuilderController;

        var cities = world.GetAllCities().ToList();
        for (int i = 0; i < cities.Count; i++)
            for (int j = i + 1; j < cities.Count; j++)
            {
                var a = cities[i];
                var b = cities[j];
                if (a.Position.Z != b.Position.Z) continue;

                int distance = a.Position.EdgeDistanceTo(b.Position);
                int minimum = a.CivilizationIndex == b.CivilizationIndex
                    ? cityBuilder.GetMinDistanceBetweenCivilizationCities(
                        world.Civilizations.First(c => c.Index == a.CivilizationIndex))
                    : cityBuilder.MinDistanceBetweenCities;

                Assert.True(distance >= minimum,
                    $"villes {a.Position} et {b.Position} distantes de {distance}, minimum {minimum}.");
            }
    }

    /// <summary>
    /// L'état généré doit pouvoir être chargé par le jeu : c'est ce qui permettra de rejouer le même
    /// cas de charge côté rendu, dans le vrai client, plutôt que de le remesurer à l'aveugle.
    /// </summary>
    [Fact]
    public void Build_ProducesAStateThatSurvivesSaveAndReload()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions());

        var reloaded = new MainGameController();
        reloaded.ImportMainState(fixture.Controller.ExportMainState());

        var world = reloaded.CurrentMainState!.CurrentWorldState!;
        Assert.Equal(fixture.CityCount, world.GetAllCities().Count());
        Assert.Equal(fixture.RoadCount, world.Civilizations.Sum(c => c.Roads.Count));
        Assert.Equal(fixture.BuildingCount, world.GetAllCities().Sum(c => c.Buildings.Count));
    }

    /// <summary>
    /// GameClock.SimulateAdvance avale les exceptions de ses abonnés : un état mal construit
    /// simulerait « sans erreur » en ne faisant rien du tout. On vérifie donc un effet observable —
    /// la production de ressources — plutôt que l'absence d'exception.
    /// </summary>
    [Fact]
    public void Build_ProducesAStateWhereSimulationActuallyRuns()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions());
        var clock = fixture.Controller.Clock!;
        var player = fixture.Controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;

        // Départ à sec : le générateur laisse les stocks au plafond après la phase de construction.
        foreach (var resource in Enum.GetValues<Resource>())
        {
            int quantity = player.GetResourceQuantity(resource);
            if (quantity > 0) player.RemoveResource(resource, quantity);
        }

        long tickBefore = clock.CurrentTick;
        for (int i = 0; i < 100; i++)
            clock.SimulateAdvance(100, 100);

        Assert.True(clock.CurrentTick > tickBefore);
        Assert.True(Enum.GetValues<Resource>().Any(r => player.GetResourceQuantity(r) > 0),
            "aucune ressource produite après 100 événements d'horloge — la simulation ne tourne pas.");
    }

    /// <summary>
    /// ClockProfiler atteint les abonnés de GameClock.Advanced par réflexion sur le champ de
    /// sauvegarde de l'événement. Ce test est le garde-fou : si l'événement est renommé ou
    /// réimplémenté avec add/remove explicites, la répartition par contrôleur redeviendrait
    /// silencieusement vide au lieu de faire échouer quoi que ce soit.
    /// </summary>
    [Fact]
    public void ClockProfiler_AttachesAndAttributesTimeToControllers()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions(20));
        var clock = fixture.Controller.Clock!;

        using var profiler = new ClockProfiler(clock);
        Assert.True(profiler.IsAttached, "ClockProfiler n'a pas trouvé le champ GameClock.Advanced.");

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(100, 100);

        Assert.True(profiler.Costs.Count >= 10,
            $"seulement {profiler.Costs.Count} abonnés vus sur l'horloge.");
        Assert.Contains(profiler.Costs, c => c.Name == nameof(SettlersOfIdlestan.Controller.Island.HarvestController));
        Assert.All(profiler.Costs, c => Assert.Equal(20, c.Calls));
    }

    /// <summary>Le profileur doit rendre l'horloge intacte : sinon tout test suivant mesurerait un jeu instrumenté.</summary>
    [Fact]
    public void ClockProfiler_RestoresTheClockOnDispose()
    {
        var fixture = EndGameStateFactory.Build(SmallOptions(20));
        var clock = fixture.Controller.Clock!;
        var player = fixture.Controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;

        using (var profiler = new ClockProfiler(clock))
            clock.SimulateAdvance(100, 100);

        long tickBefore = clock.CurrentTick;
        int roadsBefore = player.Roads.Count;
        clock.SimulateAdvance(100, 100);

        Assert.Equal(tickBefore + 100, clock.CurrentTick);
        Assert.True(player.Roads.Count >= roadsBefore);
    }

    private static List<string> CityPositions(EndGameFixture fixture)
        => fixture.Controller.CurrentMainState!.CurrentWorldState!.GetAllCities()
            .Select(c => $"{c.CivilizationIndex}@{c.Position}")
            .ToList();
}
