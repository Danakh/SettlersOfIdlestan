using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Races;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Elfes noirs : départ dans l'Inframonde sur le triangle Caverne aux champignons / Colline /
/// Montagne (surface générée mais inhabitée, vertex d'arrivée mémorisé), kit de recherches offert
/// (STARTING_RESEARCH), vertex de prestige racial offert et Pacte des Profondeurs
/// (MONSTER_ATTACK_IMMUNITY). Voir aussi <see cref="SurfaceBreachControllerTests"/> pour la sortie
/// vers la surface.
/// </summary>
public class DarkElfRaceTests
{
    /// <summary>Ascension complète jusqu'à une partie jouée en Elfe noir (races avancées débloquées).</summary>
    private static MainGameController AscendAsDarkElf()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();

        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 1000;
        godState.DivineEssence = 10;

        var ascension = controller.AscensionController;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PrestigiousAscension));
        // Combinaison propre aux Elfes noirs (voir RaceDefinitions.All) : Poing de Dieu, Présence de
        // Dieu, Purification Supérieure.
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PresenceOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.GreaterPurification));

        controller.PerformAscension(RaceId.DarkElf);
        return controller;
    }

    // ── Définition de la race ────────────────────────────────────────────────

    [Fact]
    public void DarkElf_IsImplementedAndStartsUnderground()
    {
        var race = RaceDefinitions.Get(RaceId.DarkElf);

        Assert.True(race.IsImplemented);
        Assert.Equal(BuildingType.SpiderShrine, race.RacialBuilding);
        Assert.True(race.StartsInUnderworld);
        Assert.Equal(
            new[] { TerrainType.MushroomCave, TerrainType.Hill, TerrainType.Mountain },
            race.UnderworldStartTerrains);
        Assert.Contains(PrestigeMap.MushroomCultureVertex, race.FreePrestigeVertices);
    }

    // ── Départ souterrain ────────────────────────────────────────────────────

    [Fact]
    public void Ascension_StartsWithSingleUnderworldOutpostAndNoSurfaceCity()
    {
        var controller = AscendAsDarkElf();
        var state = controller.CurrentMainState!.CurrentWorldState!;

        var city = Assert.Single(state.PlayerCivilization.Cities);
        Assert.Equal(LayerState.UnderworldZ, city.Position.Z);
        Assert.Equal(LayerState.UnderworldZ, state.CurrentViewedLayer);
    }

    [Fact]
    public void Ascension_UnderworldTriangleCoversMushroomCaveHillAndMountain()
    {
        var controller = AscendAsDarkElf();
        var state = controller.CurrentMainState!.CurrentWorldState!;

        var map = state.GetMapForZ(LayerState.UnderworldZ)!;
        var terrains = map.Tiles.Values.Select(t => t.TerrainType).ToList();

        Assert.Contains(TerrainType.MushroomCave, terrains);
        Assert.Contains(TerrainType.Hill, terrains);
        Assert.Contains(TerrainType.Mountain, terrains);
    }

    [Fact]
    public void Ascension_MemorizesSurfaceArrivalVertexOnGeneratedSurface()
    {
        var controller = AscendAsDarkElf();
        var state = controller.CurrentMainState!.CurrentWorldState!;

        // La surface existe bel et bien, avec son point d'arrivée déjà résolu par le générateur.
        Assert.NotEmpty(state.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles);
        var arrivalVertex = state.Layers[IslandMap.SurfaceLayer].ArrivalVertex;
        Assert.NotNull(arrivalVertex);
        Assert.Equal(IslandMap.SurfaceLayer, arrivalVertex!.Z);
    }

    [Fact]
    public void Ascension_ReservesTheSurfaceArrivalVertexWithALandingSite()
    {
        var controller = AscendAsDarkElf();
        var state = controller.CurrentMainState!.CurrentWorldState!;

        var arrivalVertex = state.Layers[IslandMap.SurfaceLayer].ArrivalVertex;
        var site = Assert.Single(state.PlayerCivilization.LandingSites);
        Assert.Equal(arrivalVertex, site.Position);
    }

    [Fact]
    public void LandingSite_SurvivesASaveRoundTrip()
    {
        var controller = AscendAsDarkElf();
        var arrivalVertex = controller.CurrentMainState!.CurrentWorldState!
            .Layers[IslandMap.SurfaceLayer].ArrivalVertex;

        var reloaded = new MainGameController();
        reloaded.ImportMainState(controller.ExportMainState());

        var site = Assert.Single(reloaded.CurrentMainState!.CurrentWorldState!.PlayerCivilization.LandingSites);
        Assert.Equal(arrivalVertex, site.Position);
    }

    [Fact]
    public void Ascension_OtherRaces_KeepSurfaceStartAndNoUnderworld()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 1000;
        godState.DivineEssence = 10;
        var ascension = controller.AscensionController;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PrestigiousAscension));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineLegacy));

        controller.PerformAscension(RaceId.Dwarf);

        var state = controller.CurrentMainState.CurrentWorldState!;
        Assert.Contains(state.PlayerCivilization.Cities, c => c.Position.Z == IslandMap.SurfaceLayer);
        Assert.False(state.Layers.ContainsKey(LayerState.UnderworldZ));
        Assert.Equal(IslandMap.SurfaceLayer, state.CurrentViewedLayer);
    }

    // ── Kit de démarrage ─────────────────────────────────────────────────────

    [Fact]
    public void Ascension_GrantsSpeleologieAndBoisDeChampignonButNotCultureFongique()
    {
        var controller = AscendAsDarkElf();
        var completed = controller.CurrentMainState!.PrestigeState!.TechnologyTree.CompletedTechnologies;

        Assert.Contains(TechnologyId.Speleologie, completed);
        Assert.Contains(TechnologyId.BoisDeChampignon, completed);
        // Volontairement omise : c'est le premier objectif économique de la race.
        Assert.DoesNotContain(TechnologyId.CultureFongique, completed);
    }

    [Fact]
    public void Ascension_GrantsMushroomCulturePrestigeVertex()
    {
        var controller = AscendAsDarkElf();
        var purchased = controller.CurrentMainState!.PrestigeState!.PurchasedVertices;

        Assert.Contains(PrestigeMap.MushroomCultureVertex, purchased);
    }

    [Fact]
    public void Ascension_OtherRaces_DoNotGetStartingResearchNorRacialVertex()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 1000;
        godState.DivineEssence = 10;
        var ascension = controller.AscensionController;
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PrestigiousAscension));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineLegacy));

        controller.PerformAscension(RaceId.Elf);

        var prestigeState = controller.CurrentMainState.PrestigeState!;
        Assert.Empty(prestigeState.TechnologyTree.CompletedTechnologies);
        Assert.DoesNotContain(PrestigeMap.MushroomCultureVertex, prestigeState.PurchasedVertices);
    }

    // ── Pacte des Profondeurs ────────────────────────────────────────────────

    private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);

    /// <summary>Un Troll posté sur un hex de la ville du joueur, prêt à l'attaquer.</summary>
    private static (WorldState state, GameClock clock, Civilization civ) CreateTrollOnCitySetup()
    {
        var ne = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var east = new HexCoord(1, 0, IslandMap.SurfaceLayer);

        var map = new IslandMap(new List<HexTile>
        {
            new(Center, TerrainType.Mountain),
            new(ne,     TerrainType.Plain),
            new(east,   TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        civ.AddCity(new City(Vertex.Create(ne, east, Center)) { CivilizationIndex = 0 });

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        state.AddFeature(new Troll(Center) { Found = true });

        var clock = new GameClock();
        clock.Start();
        var controller = new MonsterFeatureController();
        controller.Initialize(state, clock, new GamePRNG(1));

        return (state, clock, civ);
    }

    private static void GrantImmunity(Civilization civ, string monsterTypeName)
        => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
        {
            new(ECategory.MONSTER_ATTACK_IMMUNITY, monsterTypeName, EType.ADDITIVE, 1),
        }));

    [Fact]
    public void Troll_WithoutImmunity_AttacksTheCity()
    {
        var (state, clock, civ) = CreateTrollOnCitySetup();
        civ.AddResource(Resource.Wood, 20);

        clock.SimulateAdvance(Troll.TrollHpRegenIntervalTicks * 10);

        var troll = state.Features.OfType<Troll>().First();
        Assert.NotNull(troll.LastAttackTargetVertex);
    }

    [Fact]
    public void Troll_WithImmunity_NeverTargetsTheCity()
    {
        var (state, clock, civ) = CreateTrollOnCitySetup();
        civ.AddResource(Resource.Wood, 20);
        GrantImmunity(civ, nameof(Troll));
        int woodBefore = civ.GetResourceQuantity(Resource.Wood);

        clock.SimulateAdvance(Troll.TrollHpRegenIntervalTicks * 10);

        var troll = state.Features.OfType<Troll>().First();
        Assert.Null(troll.LastAttackTargetVertex);
        Assert.Equal(woodBefore, civ.GetResourceQuantity(Resource.Wood));
    }

    [Fact]
    public void Troll_ImmunityIsPerMonsterType()
    {
        var (state, clock, civ) = CreateTrollOnCitySetup();
        civ.AddResource(Resource.Wood, 20);
        // Immunité aux Ogres seulement : le Troll continue d'attaquer.
        GrantImmunity(civ, nameof(Ogre));

        clock.SimulateAdvance(Troll.TrollHpRegenIntervalTicks * 10);

        var troll = state.Features.OfType<Troll>().First();
        Assert.NotNull(troll.LastAttackTargetVertex);
    }

    [Fact]
    public void DarkElfModifiers_GrantTrollAndOgreImmunity()
    {
        var controller = AscendAsDarkElf();
        var civ = controller.CurrentMainState!.CurrentWorldState!.PlayerCivilization;

        Assert.True(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Troll)));
        Assert.True(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Ogre)));
        // Les autres habitants de l'Inframonde restent hostiles tant que le Sanctuaire n'est pas bâti.
        Assert.False(civ.ModifierAggregator.HasModifier(ECategory.MONSTER_ATTACK_IMMUNITY, nameof(Rats)));
    }

    // ── Sanctuaire de l'Araignée ─────────────────────────────────────────────

    [Fact]
    public void SpiderShrine_IsUnderworldOnlyAndUniqueWithZeroDefaultMaxLevel()
    {
        var shrine = (SpiderShrine)BuildingController.CreateBuilding(BuildingType.SpiderShrine)!;

        Assert.True(shrine.IsUnique);
        Assert.Equal(0, shrine.GetDefaultMaxLevel());
        Assert.False(shrine.IsAvailableInLayer(IslandMap.SurfaceLayer));
        Assert.True(shrine.IsAvailableInLayer(LayerState.UnderworldZ));
    }

    [Fact]
    public void SpiderShrine_ExtendsImmunityToRatsAndMinorDemons()
    {
        var shrine = (SpiderShrine)BuildingController.CreateBuilding(BuildingType.SpiderShrine)!;

        Assert.Empty(shrine.GetUniqueBuildingModifiers());

        shrine.Level = 1;
        var modifiers = shrine.GetUniqueBuildingModifiers().ToList();
        Assert.Contains(modifiers, m => m.Category == ECategory.MONSTER_ATTACK_IMMUNITY && m.SubCategory == nameof(Rats));
        Assert.Contains(modifiers, m => m.Category == ECategory.MONSTER_ATTACK_IMMUNITY && m.SubCategory == nameof(MinorDemon));
    }
}
