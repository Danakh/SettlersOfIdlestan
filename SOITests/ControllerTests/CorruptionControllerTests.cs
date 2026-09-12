using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests de CorruptionController : production de Dominion / réduction de Corruption par les Temples
/// (ProcessTempleProduction), production de Corruption par les Os Divins, monstres et Sources, et
/// cascade commune à toutes ces sources (FindProductionTarget). Les scénarios avec un seul hex
/// existant autour de la ville évitent toute dépendance au tirage de départage des ex aequo (un
/// candidat unique ne consomme pas le générateur) ; les scénarios de cascade utilisent une mini-carte
/// à 2 hexes (un seul voisin candidat) ou saturent tout sauf une cible pour la même
/// raison. Le PRNG (Lehmer/Park-Miller) donne un tout premier tirage quasi nul pour toute petite
/// graine (1, 2, 3, …) — sans effet sur les scénarios "100% de déclenchement" (0 déclenche toujours),
/// mais rend une graine minuscule impropre à démontrer un NON-déclenchement sur le premier tirage :
/// ces scénarios précis utilisent une graine plus grande (voir commentaire au cas par cas).
/// </summary>
public class CorruptionControllerTests
{
    /// <summary>
    /// Ville sur un vertex avec un seul hex existant sur la carte (les deux autres n'ont pas de
    /// tuile — l'eau est désormais un hex valide pour la Corruption/le Dominion) — cible du Temple garantie.
    /// </summary>
    private static (WorldState state, City city, HexCoord landHex) CreateSingleLandHexCitySetup()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var city = new City(Vertex.Create(a, b, c)) { CivilizationIndex = civ.Index };
        civ.AddCity(city);

        return (state, city, a);
    }

    /// <summary>
    /// Même chose, mais la ville est dans l'Inframonde (un seul hex existant sur cette couche, la
    /// surface se réduisant à un hex vide) — isole le malus de profondeur du Dominion.
    /// </summary>
    private static (WorldState state, City city, HexCoord underworldHex) CreateSingleHexUnderworldCitySetup()
    {
        var surface = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var map = new IslandMap(new[] { new HexTile(surface, TerrainType.Plain) });
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var a = new HexCoord(0, 0, LayerState.UnderworldZ);
        var b = new HexCoord(1, 0, LayerState.UnderworldZ);
        var c = new HexCoord(0, 1, LayerState.UnderworldZ);
        var underworldTiles = new[] { new HexTile(a, TerrainType.Mountain) };
        state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(underworldTiles, LayerState.UnderworldZ)));

        var city = new City(Vertex.Create(a, b, c)) { CivilizationIndex = civ.Index };
        civ.AddCity(city);

        return (state, city, a);
    }

    /// <summary>Deux hexes de terre adjacents, aucun autre hex sur la carte — un seul voisin candidat de chaque côté pour la cascade.</summary>
    private static (WorldState state, HexCoord a, HexCoord b) CreateTwoLandHexesSetup()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer); // voisin Est de a

        var tiles = new[] { new HexTile(a, TerrainType.Plain), new HexTile(b, TerrainType.Plain) };
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        return (state, a, b);
    }

    private static CorruptionController CreateController(WorldState state, GameClock clock, int seed = 1, PrestigeState? prestigeState = null)
    {
        var controller = new CorruptionController();
        controller.Initialize(state, clock, new GamePRNG(seed), prestigeState);
        return controller;
    }

    /// <summary>Complète une recherche sur la civilisation du joueur (mêmes modificateurs qu'en jeu).</summary>
    private static void CompleteResearch(WorldState state, TechnologyId id)
    {
        var tree = new TechnologyTree();
        tree.CompleteResearch(id);
        state.PlayerCivilization.AddCustomAggregator(tree);
    }

    // ── Production des Temples ──────────────────────────────────────────────

    [Fact]
    public void TempleLevel2_NoCorruptionOnTarget_CreatesDominionLevel1()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().SingleOrDefault();
        Assert.NotNull(dominion);
        Assert.Equal(1, dominion!.Level);
    }

    [Fact]
    public void TempleLevel2_ExistingDominionAtCap_DoesNotExceedTwiceTempleLevel()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Dominion(landHex, level: 4)); // cap = 2 * 2 = 4

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().Single();
        Assert.Equal(4, dominion.Level);
    }

    [Fact]
    public void TempleLevel4_ExistingDominionBelowCap_IncrementsUpToEight()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 4 });
        state.AddFeature(new Dominion(landHex, level: 7)); // cap = 2 * 4 = 8

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().Single();
        Assert.Equal(8, dominion.Level);
    }

    [Fact]
    public void TempleLevel2_CorruptionOnTarget_ReducesCorruptionInsteadOfCreatingDominion()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 3));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void TempleLevel2_CorruptionOnTarget_Evangelisation_RemovesTwoLevelsAtOnce()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 3));
        // Chance forcée à 100% (1 Os Divin purifié × 100%/Os) pour rendre le second niveau de
        // l'Évangélisation déterministe, sans reliquat pour un troisième.
        state.RunRecord.DivineBonesPurified = 1;
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.CORRUPTION_DOUBLE_CLEANSE_CHANCE, Modifier.EType.ADDITIVE, 1.0),
        }));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(1, corruption.Level);
    }

    [Fact]
    public void TempleLevel2_CorruptionAtLevel1_Evangelisation_RemovesFeatureWithoutGoingNegative()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 1));
        state.RunRecord.DivineBonesPurified = 1;
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.CORRUPTION_DOUBLE_CLEANSE_CHANCE, Modifier.EType.ADDITIVE, 1.0),
        }));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void TempleLevel2_CorruptionAtLevel1_RemovesFeatureOnceReducedToZero()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
        Assert.False(state.HasFeaturesAt(landHex));
    }

    [Fact]
    public void TempleLevel1_BelowThreshold_DoesNotProduce()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 1 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.False(state.HasFeaturesAt(landHex));
    }

    [Fact]
    public void TempleLevel5_AboveThreshold_DoesNotProduce()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 5 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.False(state.HasFeaturesAt(landHex));
    }

    // ── Cascade : seule façon pour la Corruption et le Dominion de gagner du terrain ─────

    /// <summary>Tous les hexes à distance &lt;= radius du centre, anneau par anneau.</summary>
    private static List<HexCoord> HexDisc(HexCoord center, int radius)
    {
        var all = new List<HexCoord> { center };
        var seen = new HashSet<HexCoord> { center };
        var frontier = new List<HexCoord> { center };

        for (int r = 0; r < radius; r++)
        {
            var next = new List<HexCoord>();
            foreach (var hex in frontier)
                foreach (var neighbor in hex.Neighbors())
                    if (seen.Add(neighbor))
                        next.Add(neighbor);
            all.AddRange(next);
            frontier = next;
        }

        return all;
    }

    /// <summary>
    /// Carte hexagonale pleine autour de l'origine, avec une ville du joueur sur le vertex
    /// (0,0)/(1,0)/(0,1) — les 3 hexes de la ville existent tous, et la cascade a de la place pour
    /// s'éloigner.
    /// </summary>
    private static (WorldState state, City city, HexCoord[] cityHexes, List<HexCoord> allHexes) CreateWideMapCitySetup(int radius)
    {
        var allHexes = HexDisc(new HexCoord(0, 0, IslandMap.SurfaceLayer), radius);
        var map = new IslandMap(allHexes.Select(h => new HexTile(h, TerrainType.Plain)).ToArray());
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var city = new City(Vertex.Create(a, b, c)) { CivilizationIndex = civ.Index };
        civ.AddCity(city);

        return (state, city, new[] { a, b, c }, allHexes);
    }

    /// <summary>Distance du hex au plus proche des hexes de départ de la cascade.</summary>
    private static int DistanceToSeeds(HexCoord hex, HexCoord[] seeds) => seeds.Min(s => s.DistanceTo(hex));

    /// <summary>Avance d'un intervalle de production, après l'intervalle sentinelle qui initialise LastDominionProductionTick (coldStartOnZero).</summary>
    private static void AdvanceOneTempleProduction(GameClock clock)
    {
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinelle
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
    }

    [Fact]
    public void PassiveFeatures_WithoutAnySource_NeverSpread()
    {
        // Coeur du changement : une poche de Corruption ou de Dominion ne déborde plus d'elle-même,
        // même au niveau maximal. Sans source pour la nourrir, elle est totalement inerte.
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: Corruption.MaxLevel);
        var dominion = new Dominion(b, level: Dominion.MaxLevel);
        state.AddFeature(corruption);
        state.AddFeature(dominion);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 50; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(Corruption.MaxLevel, corruption.Level);
        Assert.Equal(Dominion.MaxLevel, dominion.Level);
        Assert.Equal(2, state.Features.Count);
    }

    [Fact]
    public void Cascade_TempleFillsTheLeastFilledOfItsThreeCityHexes()
    {
        var (state, city, cityHexes, _) = CreateWideMapCitySetup(radius: 4);
        city.AddBuilding(new Temple { Level = 2 }); // plafond 2 x 2 = 4
        state.AddFeature(new Dominion(cityHexes[0], level: 3));
        state.AddFeature(new Dominion(cityHexes[1], level: 1));
        // cityHexes[2] est sain : remplissage 0, minimum strict des trois — aucun départage aléatoire.

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        Assert.Equal(3, state.GetFirstFeatureAt<Dominion>(cityHexes[0])!.Level);
        Assert.Equal(1, state.GetFirstFeatureAt<Dominion>(cityHexes[1])!.Level);
        Assert.Equal(1, state.GetFirstFeatureAt<Dominion>(cityHexes[2])!.Level);
    }

    [Fact]
    public void Cascade_AllCityHexesAtCap_ProducesOnRadiusOne()
    {
        var (state, city, cityHexes, _) = CreateWideMapCitySetup(radius: 4);
        city.AddBuilding(new Temple { Level = 2 }); // plafond 4
        foreach (var hex in cityHexes)
            state.AddFeature(new Dominion(hex, level: 4));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        var seeded = state.Features.OfType<Dominion>().Where(d => d.Level == 1).ToList();
        Assert.Single(seeded);
        Assert.Equal(1, DistanceToSeeds(seeded[0].Position, cityHexes));
        Assert.All(cityHexes, h => Assert.Equal(4, state.GetFirstFeatureAt<Dominion>(h)!.Level));
    }

    [Fact]
    public void Cascade_EverythingSaturatedWithinMaxRadius_ProductionIsLost()
    {
        // Tout est au plafond jusqu'au rayon maximal ; les hexes libres existent, mais plus loin.
        var (state, city, cityHexes, allHexes) = CreateWideMapCitySetup(radius: CorruptionController.MaxCascadeRadius + 3);
        city.AddBuilding(new Temple { Level = 2 }); // plafond 4
        foreach (var hex in allHexes)
            if (DistanceToSeeds(hex, cityHexes) <= CorruptionController.MaxCascadeRadius)
                state.AddFeature(new Dominion(hex, level: 4));

        int featuresBefore = state.Features.Count;

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(featuresBefore, state.Features.Count);
        Assert.All(state.Features.OfType<Dominion>(), d => Assert.Equal(4, d.Level));
    }

    [Fact]
    public void Cascade_NoValidHexAtAll_ProductionIsLost()
    {
        // Un seul hex sur la carte, déjà au plafond : la cascade ne trouve aucun candidat et la
        // production est simplement perdue, sans exception ni dépassement du plafond.
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 }); // plafond 4
        var dominion = new Dominion(landHex, level: 4);
        state.AddFeature(dominion);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(4, dominion.Level);
        Assert.Single(state.Features);
    }

    [Fact]
    public void Cascade_PrefersTheStrongestOpposingHex_OverAnEmptyOne_AndFightsIt()
    {
        // Le remplissage compte le statut opposé en négatif : la Corruption de niveau 5 (-5) passe
        // avant celle de niveau 2 (-2), elle-même avant l'hex sain (0). La production s'y dépense en
        // combat, elle ne pose aucun Dominion.
        var (state, city, cityHexes, _) = CreateWideMapCitySetup(radius: 4);
        city.AddBuilding(new Temple { Level = 2 });
        var weak = new Corruption(cityHexes[0], level: 2);
        var strong = new Corruption(cityHexes[1], level: 5);
        state.AddFeature(weak);
        state.AddFeature(strong);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        Assert.Equal(4, strong.Level);
        Assert.Equal(2, weak.Level);
        Assert.Empty(state.Features.OfType<Dominion>());
    }

    [Fact]
    public void Cascade_CorruptionSourceOwnHexAtCap_SpillsToItsNeighbour()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: 2);
        state.AddFeature(corruption);
        state.AddFeature(new CorruptionSource(a, corruptionLevel: 2)); // plafond 2, déjà atteint sur a

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(2, corruption.Level);
        Assert.Equal(1, state.GetFirstFeatureAt<Corruption>(b)!.Level);
    }

    [Fact]
    public void Cascade_CorruptionSource_FillsItsNeighbourhoodUpToItsCapAndStops()
    {
        // Une Source isolée finit par saturer tout son disque de rayon MaxCascadeRadius à son propre
        // plafond, et rien au-delà : la cascade borne l'emprise d'une source, elle ne l'étend pas sans fin.
        var sourceHex = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var allHexes = HexDisc(sourceHex, CorruptionController.MaxCascadeRadius + 2);
        var map = new IslandMap(allHexes.Select(h => new HexTile(h, TerrainType.Plain)).ToArray());
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        state.AddFeature(new CorruptionSource(sourceHex, corruptionLevel: 2)); // plafond 2

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 1000; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        foreach (var hex in allHexes)
        {
            var corruption = state.GetFirstFeatureAt<Corruption>(hex);
            if (hex.DistanceTo(sourceHex) <= CorruptionController.MaxCascadeRadius)
            {
                Assert.NotNull(corruption);
                Assert.Equal(2, corruption!.Level);
            }
            else
            {
                Assert.Null(corruption);
            }
        }
    }

    // ── Recherches de la Théocratie (Dogme de l'Emprise, Évangélisation, Terre Consacrée) ──

    [Fact]
    public void DominionLayerDivisor_WithoutDogmeDeLEmprise_DoublesPerLayer()
    {
        var civ = new Civilization { Index = 0 };

        Assert.Equal(1000, CorruptionController.GetDominionLayerDivisorMilli(civ, IslandMap.SurfaceLayer));
        Assert.Equal(2000, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.UnderworldZ));
        Assert.Equal(4000, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.AbyssZ));
        Assert.Equal(8000, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.PandemoniumZ));
    }

    [Fact]
    public void DominionLayerDivisor_WithDogmeDeLEmprise_UsesOnePointFivePerLayer()
    {
        var (state, _, _) = CreateSingleHexUnderworldCitySetup();
        CompleteResearch(state, TechnologyId.DogmeDeLEmprise);
        var civ = state.PlayerCivilization;

        Assert.Equal(1000, CorruptionController.GetDominionLayerDivisorMilli(civ, IslandMap.SurfaceLayer));
        Assert.Equal(1500, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.UnderworldZ));
        Assert.Equal(2250, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.AbyssZ));
        Assert.Equal(3375, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.PandemoniumZ));
    }

    [Fact]
    public void TempleInUnderworld_WithDogmeDeLEmprise_ClearsMoreCorruption()
    {
        int withoutDogme = RunUnderworldTempleCorruptionClearing(withDogme: false);
        int withDogme = RunUnderworldTempleCorruptionClearing(withDogme: true);

        Assert.True(withDogme < withoutDogme, $"Dogme : {withDogme} cycles, sans : {withoutDogme}");
    }

    /// <summary>
    /// Nombre de cycles qu'un Temple de l'Inframonde met à dissiper entièrement une zone de Corruption
    /// partie du niveau maximal, à graine identique : un tir sur deux aboutit sans le Dogme de l'Emprise
    /// (÷2), deux sur trois avec (÷1,5), donc moins de cycles avec. Compté en cycles plutôt qu'en points
    /// dissipés sur un intervalle fixe : depuis <see cref="Corruption.MaxLevel"/>, une zone finit
    /// toujours par être nettoyée et les deux mesures plafonneraient au même total.
    /// </summary>
    private static int RunUnderworldTempleCorruptionClearing(bool withDogme)
    {
        const int maxCycles = 1000;

        var (state, city, underworldHex) = CreateSingleHexUnderworldCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        var corruption = new Corruption(underworldHex, level: Corruption.MaxLevel);
        state.AddFeature(corruption);
        if (withDogme)
            CompleteResearch(state, TechnologyId.DogmeDeLEmprise);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, seed: 25555);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)

        for (int cycle = 1; cycle <= maxCycles; cycle++)
        {
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
            if (corruption.Level <= 0) return cycle;
        }

        return maxCycles;
    }

    [Fact]
    public void Temple_WithoutDoubleProductionBonus_ProducesOnePointPerInterval()
    {
        var (state, city, _, _) = CreateWideMapCitySetup(radius: 3);
        city.AddBuilding(new Temple { Level = 2 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        Assert.Equal(1, state.Features.OfType<Dominion>().Sum(d => d.Level));
    }

    [Fact]
    public void Temple_WithDoubleProductionBonus_ProducesASecondPointInTheSameInterval()
    {
        // 100 points de % par niveau effectif de Temple (2) = 200% : le second point tombe à coup sûr.
        var (state, city, _, _) = CreateWideMapCitySetup(radius: 3);
        city.AddBuilding(new Temple { Level = 2 });
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.DOMINION_SPREAD_CHANCE, Modifier.EType.ADDITIVE, 100),
        }));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        Assert.Equal(2, state.Features.OfType<Dominion>().Sum(d => d.Level));
    }

    [Fact]
    public void CorruptionSourceAttackingDominion_TempleProtection_DominionSpared()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var dominion = new Dominion(a, level: 4);
        state.AddFeature(dominion);
        state.AddFeature(new CorruptionSource(a, corruptionLevel: 3));

        // Ville du joueur touchant a avec un Temple niveau 1 (aucune production, donc aucune
        // consommation du PRNG par ProcessTempleProduction) ; chance de protection forcée à 100%
        // pour rendre le tirage de Terre Consacrée déterministe.
        var city = new City(Vertex.Create(a, b, new HexCoord(0, 1, IslandMap.SurfaceLayer))) { CivilizationIndex = 0 };
        city.AddBuilding(new Temple { Level = 1 });
        state.PlayerCivilization.AddCity(city);
        state.RunRecord.DivineBonesPurified = 1;
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, Modifier.EType.ADDITIVE, 1.0),
        }));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        // Le Dominion protégé ne perd jamais de niveau, et la production perdue ne se reporte pas
        // ailleurs : a reste la cible (remplissage -4, le plus bas) à chaque intervalle.
        Assert.Equal(4, dominion.Level);
        Assert.Empty(state.Features.OfType<Corruption>());
    }

    [Fact]
    public void CorruptionSourceAttackingDominion_ProtectionWithoutTemple_DominionStillReduced()
    {
        var (state, a, _) = CreateTwoLandHexesSetup();
        var dominion = new Dominion(a, level: 4);
        state.AddFeature(dominion);
        state.AddFeature(new CorruptionSource(a, corruptionLevel: 3));

        // Chance de protection maximale mais aucune ville avec Temple : la protection ne s'applique pas.
        state.RunRecord.DivineBonesPurified = 1;
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, Modifier.EType.ADDITIVE, 1.0),
        }));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, dominion.Level);
    }

    [Fact]
    public void NoSourceOnMap_NeverThrows()
    {
        var (state, _, _) = CreateTwoLandHexesSetup();

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.Features);
    }

    // ── Spire de Corruption / Faille des Abysses : aucune protection, décroissance garantie ──

    [Fact]
    public void MonumentDecay_ReducesCorruptionOnSpireHex_Guaranteed()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
    }

    [Fact]
    public void MonumentDecay_DoesNotReduceCorruptionOnSpireHex_WhileUnderConstruction()
    {
        // Tant que la Spire n'est pas achevée (Built = false), elle ne réduit pas encore la corruption,
        // y compris sur son propre hex.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
    }

    [Fact]
    public void MonumentDecay_ReducesCorruptionUnderAbyssGate_Guaranteed()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new AbyssGate(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
    }

    [Fact]
    public void MonumentDecay_ClearingToZero_RecordsPeakLevelNotFinalLevel()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        // Simule une zone qui a grimpé jusqu'au niveau 5 avant d'être ramenée à 1 par un autre biais :
        // c'est le pic (5), pas le niveau final au moment du nettoyage (1), qui doit être enregistré.
        state.AddFeature(new Corruption(landHex, level: 1) { PeakLevel = 5 });
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });

        var prestigeState = new PrestigeState();
        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: prestigeState);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
        Assert.Equal(5, state.RunRecord.MaxCorruptionLevelCleared);
    }

    [Fact]
    public void SpireHex_TempleProduction_CanStillCreateDominion()
    {
        // La Spire de Corruption ne protège plus son hex : un Temple peut toujours y poser du Dominion.
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new CorruptionSpire(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Single(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void SpireHex_NotProtected_ACorruptionSourceOnItStillProduces()
    {
        // La Spire de Corruption ne protège pas son hex : une source posée dessus y produit
        // normalement. Spire laissée en construction (Built = false) pour isoler ce mécanisme de la
        // décroissance garantie, qui effacerait aussitôt la Corruption produite.
        var (state, a, _) = CreateTwoLandHexesSetup();
        state.AddFeature(new CorruptionSpire(a));
        state.AddFeature(new CorruptionSource(a, corruptionLevel: 3));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFirstFeatureAt<Corruption>(a)!.Level);
    }

    [Fact]
    public void SpireHex_NotProtected_ATempleCascadeCanSeedDominionOnIt()
    {
        // Hex a saturé, b porte la Spire et rien d'autre : la cascade du Temple vise b.
        var (state, a, b) = CreateTwoLandHexesSetup();
        var city = new City(Vertex.Create(a, b, new HexCoord(0, 1, IslandMap.SurfaceLayer))) { CivilizationIndex = 0 };
        city.AddBuilding(new Temple { Level = 2 }); // plafond 4
        state.PlayerCivilization.AddCity(city);
        state.AddFeature(new Dominion(a, level: 4));
        state.AddFeature(new CorruptionSpire(b));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        Assert.Equal(1, state.GetFirstFeatureAt<Dominion>(b)!.Level);
    }

    [Fact]
    public void AbyssGateHex_TempleProduction_CanStillCreateDominion()
    {
        // La Faille des Abysses ne protège plus son hex non plus : un Temple peut toujours y poser du Dominion.
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new AbyssGate(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Single(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void AbyssGateHex_NotProtected_ACorruptionCascadeCanReachIt()
    {
        // La Faille des Abysses ne protège pas son hex : la cascade d'une Source voisine y sème de la
        // Corruption. La décroissance garantie de la Faille passe avant la production dans le même
        // intervalle, elle ne trouve donc rien à retirer ce tour-là.
        var (state, a, b) = CreateTwoLandHexesSetup();
        state.AddFeature(new Corruption(a, level: 2)); // a déjà au plafond de la Source
        state.AddFeature(new CorruptionSource(a, corruptionLevel: 2));
        state.AddFeature(new AbyssGate(b));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFirstFeatureAt<Corruption>(b)!.Level);
    }

    [Fact]
    public void AbyssGateHex_NotProtected_ATempleCascadeCanSeedDominionOnIt()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var city = new City(Vertex.Create(a, b, new HexCoord(0, 1, IslandMap.SurfaceLayer))) { CivilizationIndex = 0 };
        city.AddBuilding(new Temple { Level = 2 }); // plafond 4
        state.PlayerCivilization.AddCity(city);
        state.AddFeature(new Dominion(a, level: 4));
        state.AddFeature(new AbyssGate(b));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        AdvanceOneTempleProduction(clock);

        Assert.Equal(1, state.GetFirstFeatureAt<Dominion>(b)!.Level);
    }

    [Fact]
    public void MonumentDecay_ReducesCorruptionOnSpireNeighbor_WithinRadius()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        // Aucune source sur la carte : la Corruption est passive, ce qui isole la décroissance
        // garantie de la Spire de tout autre effet.
        state.AddFeature(new Corruption(b, level: 2)); // sur le voisin de la Spire, pas sur son propre hex
        state.AddFeature(new CorruptionSpire(a) { Built = true });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(b).OfType<Corruption>().Single();
        Assert.Equal(1, corruption.Level); // rayon 1 : le voisin immédiat est couvert
    }

    [Fact]
    public void MonumentDecay_DoesNotReachHexesBeyondRadius()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var farHex = new HexCoord(2, 0, IslandMap.SurfaceLayer); // à distance 2 de a
        var tiles = new[] { new HexTile(a, TerrainType.Plain), new HexTile(b, TerrainType.Plain), new HexTile(farHex, TerrainType.Plain) };
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        // Aucune source sur la carte : la Corruption est passive, ce qui isole la décroissance
        // garantie de la Spire de tout autre effet.
        state.AddFeature(new Corruption(farHex, level: 2));
        state.AddFeature(new CorruptionSpire(a) { Built = true }); // rayon 1 : n'atteint pas farHex (distance 2)

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(farHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level); // hors rayon : aucune décroissance garantie
    }

    // ── Éligibilité de la Faille des Abysses : basée sur le nettoyage, n'importe où ────────

    [Fact]
    public void ReduceLevel_ClearingCorruptionViaTempleProduction_MakesAbyssGateEligible_OnUnrelatedHex()
    {
        // AbyssGateController.IsAbyssGateEligible se base sur RunRecord.MaxCorruptionLevelCleared, ici
        // alimenté par la production d'un Temple sur un hex qui n'a AUCUN rapport avec celui de la
        // Spire — l'éligibilité doit être vraie quel que soit l'hex nettoyé et quel que soit le
        // mécanisme de nettoyage.
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: AbyssGate.RequiredCorruptionLevel));

        // Spire déjà bâtie sur un tout autre hex, sans lien avec le nettoyage ci-dessus.
        var spireHex = new HexCoord(50, 50, IslandMap.SurfaceLayer);
        state.AddFeature(new CorruptionSpire(spireHex) { Built = true });

        var prestigeState = new PrestigeState();
        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, seed: 1, prestigeState: prestigeState);

        // Le Temple cible le seul hex valide de la carte à chaque intervalle : largement assez pour
        // ramener la Corruption (niveau initial = seuil requis) à 0.
        for (int i = 0; i < AbyssGate.RequiredCorruptionLevel + 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
        Assert.True(state.RunRecord.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel);
        Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateEligible && e.Toast);

        var gateController = new AbyssGateController();
        gateController.Initialize(state, clock);
        Assert.True(gateController.IsAbyssGateEligible());
    }

    // ── Os Divins : générateurs de Corruption tant qu'ils ne sont pas purifiés ─────────────
    // La carte à un seul hex isole ce mécanisme de la cascade (aucun autre hex candidat).

    [Fact]
    public void DivineBones_RaiseCorruptionOnTheirOwnHex_EachInterval()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3)); // plafond 6

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
        Assert.Equal(3, corruption.PeakLevel); // le pic engendré compte pour le record de nettoyage
    }

    [Fact]
    public void DivineBones_SeedCorruption_WhenTheirHexIsClean()
    {
        // Hex sain (une Spire voisine a pu le nettoyer) : les Os y resèment une poche de niveau 1.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 2));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_StopRaisingCorruption_AtTwiceTheIslandCorruptionLevel()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        var bones = new DivineBones(landHex, corruptionLevel: 2); // plafond 4
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(bones);

        Assert.Equal(4, bones.GetCorruptionCap());

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 10; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(4, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_DoNotReduceCorruptionAlreadyAboveTheirCap()
    {
        // Le plafond ne borne que la génération : une Corruption plus élevée (tirage initial de
        // l'Abysse, cascade d'une source voisine) est laissée telle quelle, jamais rabaissée.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 9));
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 2)); // plafond 4

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_Purified_GenerateNoCorruption()
    {
        // En jeu, des Os purifiés sont retirés de la carte (DivineBonesController.ProcessInvestment) ;
        // l'état transitoire ne doit de toute façon plus rien engendrer.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3) { Purified = true });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void DivineBones_UnderBuiltSpire_CancelOutItsDecay()
    {
        // Décroissance garantie de la Spire (-1) puis génération des Os (+1) dans le même intervalle :
        // la Corruption reste figée tant que les Os ne sont pas purifiés.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3)); // plafond 6

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    // ── Sources de Corruption : générateurs de Corruption, sans le doublement de plafond des Os Divins ──
    // La carte à un seul hex isole ce mécanisme de la cascade (aucun autre hex candidat).

    [Fact]
    public void CorruptionSource_RaisesCorruptionOnItsOwnHex_EachInterval()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 3)); // plafond 3, pas 6

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
        Assert.Equal(3, corruption.PeakLevel); // le pic engendré compte pour le record de nettoyage
    }

    [Fact]
    public void CorruptionSource_SeedsCorruption_WhenItsHexIsClean()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 2));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void CorruptionSource_StopsRaisingCorruption_AtTheIslandCorruptionLevel_NotTwice()
    {
        // Contrairement aux Os Divins (plafond ×2), la Source s'arrête exactement au niveau de
        // corruption de l'île figé à sa génération.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        var source = new CorruptionSource(landHex, corruptionLevel: 2); // plafond 2, pas 4
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(source);

        Assert.Equal(2, source.GetCorruptionCap());

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 10; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void CorruptionSource_DoesNotReduceCorruptionAlreadyAboveItsCap()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 9));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 2)); // plafond 2

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    // ── Invariant : Corruption et Dominion ne coexistent jamais sur le même hex ────────────
    // Reproduit le bug signalé : une Source de Corruption (ou un Os Divin/monstre) ne doit jamais
    // semer de la Corruption sur un hex déjà occupé par du Dominion — elle doit d'abord le combattre.

    [Fact]
    public void CorruptionSource_FightsExistingDominion_InsteadOfSeedingCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 4));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Dominion>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void CorruptionSource_KeepsFightingDominion_UntilItIsGone_ThenSeedsCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 2));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // Dominion 2 -> 1
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // Dominion 1 -> 0 (supprimé)

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());

        // Le Dominion enfin parti, la Source recommence à semer normalement.
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_FightsExistingDominion_InsteadOfSeedingCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 1));
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void Monster_FightsExistingDominion_InsteadOfSeedingCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 1));
        state.AddFeature(new DemonGod(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 2 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    // ── Tentacules et Dieu démon : générateurs de Corruption tant qu'ils sont vivants ──────
    // Même mécanique que les Os Divins, avec le plafond calculé sur le niveau de corruption courant.

    [Fact]
    public void Tentacle_RaisesCorruptionOnItsOwnHex_EachInterval()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new Tentacle(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 3 }); // plafond 6

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
        Assert.Equal(3, corruption.PeakLevel); // le pic engendré compte pour le record de nettoyage
    }

    [Fact]
    public void DemonGod_SeedsCorruption_WhenItsHexIsClean()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new DemonGod(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 2 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void Monsters_StopRaisingCorruption_AtTwiceTheIslandCorruptionLevel()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new DemonGod(landHex));

        var clock = new GameClock();
        clock.Start();
        var controller = CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 4 });

        Assert.Equal(8, controller.GetMonsterCorruptionCap());

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(8, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void Monsters_DoNotReduceCorruptionAlreadyAboveTheirCap()
    {
        // Le plafond ne borne que la génération : le tirage initial d'une île de l'Abysse peut déjà
        // dépasser 2× le niveau de corruption, il est laissé tel quel.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 9));
        state.AddFeature(new Tentacle(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 2 }); // plafond 4

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void MonsterRemovedFromMap_GeneratesNoMoreCorruption()
    {
        // Abattre la Tentacule tarit la source ; la Corruption déjà semée reste à nettoyer.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        var tentacle = new Tentacle(landHex);
        state.AddFeature(tentacle);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 5 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        state.RemoveFeature(tentacle);
        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void OtherMonsters_GenerateNoCorruption()
    {
        // L'opt-in ne concerne que les monstres enracinés dans la Corruption (Tentacule, Dieu démon).
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new MajorDemon(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 5 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void Tentacle_UnderBuiltSpire_CancelsOutItsDecay()
    {
        // Décroissance garantie de la Spire (-1) puis génération de la Tentacule (+1) dans le même
        // intervalle : la Corruption reste figée tant que la Tentacule est vivante.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });
        state.AddFeature(new Tentacle(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 3 }); // plafond 6

        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    // ── Corruption semée à l'apparition d'un monstre enraciné ─────────────────────────────

    /// <summary>Carte pleine de rayon 2 autour de l'origine — de quoi observer les 6 voisins d'un monstre placé au centre.</summary>
    private static (WorldState state, HexCoord center) CreateRadius2MapSetup(TerrainType centerTerrain = TerrainType.Plain)
    {
        var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var tiles = new List<HexTile>();
        for (int q = -2; q <= 2; q++)
            for (int r = System.Math.Max(-2, -q - 2); r <= System.Math.Min(2, -q + 2); r++)
                tiles.Add(new HexTile(new HexCoord(q, r, IslandMap.SurfaceLayer), centerTerrain));

        var map = new IslandMap(tiles.ToArray());
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        return (state, center);
    }

    [Fact]
    public void NewMonster_CorruptsItsHexAndAllSixNeighbours_ToTheIslandCorruptionLevel()
    {
        var (state, center) = CreateRadius2MapSetup();
        var tentacle = new Tentacle(center);
        state.AddFeature(tentacle);

        CorruptionController.SeedCorruptionAroundNewMonster(state, tentacle, islandCorruptionLevel: 5);

        foreach (var hex in center.Neighbors().Append(center))
        {
            var corruption = state.GetFeaturesAt(hex).OfType<Corruption>().Single();
            Assert.Equal(5, corruption.Level);
            Assert.Equal(5, corruption.PeakLevel);
        }

        // Le deuxième anneau reste sain : seul le voisinage immédiat est semé.
        Assert.Empty(state.GetFeaturesAt(new HexCoord(2, 0, IslandMap.SurfaceLayer)).OfType<Corruption>());
    }

    [Fact]
    public void NewMonster_SeedsHalfOfItsGenerationCap()
    {
        // Le niveau semé est exactement la moitié du plafond que la génération continue atteindra.
        var (state, center) = CreateRadius2MapSetup();
        var god = new DemonGod(center);
        state.AddFeature(god);

        var clock = new GameClock();
        clock.Start();
        var prestigeState = new PrestigeState { CurrentCorruptionLevel = 4 };
        var controller = CreateController(state, clock, prestigeState: prestigeState);

        CorruptionController.SeedCorruptionAroundNewMonster(state, god, prestigeState.CurrentCorruptionLevel);

        Assert.Equal(8, controller.GetMonsterCorruptionCap());
        Assert.Equal(4, state.GetFeaturesAt(center).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void NewMonster_NeverLowersAnAlreadyDeeperCorruption()
    {
        var (state, center) = CreateRadius2MapSetup();
        var neighbour = center.Neighbors()[0];
        state.AddFeature(new Corruption(center, level: 9));
        state.AddFeature(new Corruption(neighbour, level: 2));

        var tentacle = new Tentacle(center);
        state.AddFeature(tentacle);

        CorruptionController.SeedCorruptionAroundNewMonster(state, tentacle, islandCorruptionLevel: 5);

        Assert.Equal(9, state.GetFeaturesAt(center).OfType<Corruption>().Single().Level);
        Assert.Equal(5, state.GetFeaturesAt(neighbour).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void NewMonster_SkipsVoidAndMissingHexes()
    {
        // Un hex de Void n'est jamais rendu ni interactif (cf. PlaceAbyssCorruption) ; un hex sans
        // tuile n'existe pas du tout.
        var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var voidHex = center.Neighbors()[0];
        var landHex = center.Neighbors()[1];
        var missingHex = center.Neighbors()[2];

        var map = new IslandMap(new[]
        {
            new HexTile(center, TerrainType.Plain),
            new HexTile(voidHex, TerrainType.Void),
            new HexTile(landHex, TerrainType.Plain),
        });
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var tentacle = new Tentacle(center);
        state.AddFeature(tentacle);

        CorruptionController.SeedCorruptionAroundNewMonster(state, tentacle, islandCorruptionLevel: 3);

        Assert.Equal(3, state.GetFeaturesAt(center).OfType<Corruption>().Single().Level);
        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(voidHex).OfType<Corruption>());
        Assert.Empty(state.GetFeaturesAt(missingHex).OfType<Corruption>());
    }

    [Fact]
    public void NewMonster_WithoutCorruptionGeneration_SeedsNothing()
    {
        var (state, center) = CreateRadius2MapSetup();
        var demon = new MajorDemon(center);
        state.AddFeature(demon);

        CorruptionController.SeedCorruptionAroundNewMonster(state, demon, islandCorruptionLevel: 5);

        Assert.Empty(state.Features.OfType<Corruption>());
    }
}
