using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
using SOITests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

/// <summary>
/// Système de races (choix à l'Ascension) : déblocage par rangées de pouvoirs divins
/// (AscensionController.IsRaceSelectionUnlocked / AreAdvancedRacesUnlocked / GetSelectableRaces),
/// enregistrement des races ayant ascensionné et bâtiments raciaux permanents, restrictions de
/// placement (CITY_MIN_DISTANCE, CITY_PLACEMENT_REQUIRES_TERRAIN), réduction de coût de ville
/// (NEW_CITY_COST_REDUCTION) et effet Ziggourat (production instantanée de Dominion des Temples).
/// </summary>
public class RaceSystemTests
{
    // ── Déblocage des races par les rangées de pouvoirs divins ──────────────

    private static AscensionController CreateAscension(out GodState godState, int godPoints = 100)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        godState = new GodState { GodPoints = godPoints };
        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState);
        return ascension;
    }

    private static void UnlockFirstRow(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.HandOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.EyeOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
    }

    [Fact]
    public void IsRaceSelectionUnlocked_RequiresAllFourFirstRowPowers()
    {
        var ascension = CreateAscension(out _);

        Assert.False(ascension.IsRaceSelectionUnlocked);

        ascension.PurchasePower(AscensionPowerId.Faith);
        ascension.PurchasePower(AscensionPowerId.HandOfGod);
        ascension.PurchasePower(AscensionPowerId.EyeOfGod);
        ascension.PurchasePower(AscensionPowerId.WalkOfGod);
        Assert.False(ascension.IsRaceSelectionUnlocked);

        ascension.PurchasePower(AscensionPowerId.ArmOfGod);
        Assert.True(ascension.IsRaceSelectionUnlocked);
    }

    [Fact]
    public void AreAdvancedRacesUnlocked_RequiresFullSecondRow()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);

        Assert.False(ascension.AreAdvancedRacesUnlocked);

        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.False(ascension.AreAdvancedRacesUnlocked);

        Assert.True(ascension.PurchasePower(AscensionPowerId.PresenceOfGod));
        Assert.True(ascension.AreAdvancedRacesUnlocked);
    }

    [Fact]
    public void GetSelectableRaces_LockedReturnsHumanOnly()
    {
        var ascension = CreateAscension(out _);

        Assert.Equal(new[] { RaceId.Human }, ascension.GetSelectableRaces());
    }

    [Fact]
    public void GetSelectableRaces_UnlockedReturnsBaseRacesButNeverAdvancedOnes()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);

        var races = ascension.GetSelectableRaces();

        Assert.Contains(RaceId.Human, races);
        Assert.Contains(RaceId.Elf, races);
        Assert.Contains(RaceId.Dwarf, races);
        Assert.Contains(RaceId.Goblin, races);
        Assert.Contains(RaceId.Giant, races);
        // Sirènes et Elfes noirs : races avancées non implémentées, jamais sélectionnables.
        Assert.DoesNotContain(RaceId.Mermaid, races);
        Assert.DoesNotContain(RaceId.DarkElf, races);
    }

    [Fact]
    public void GetModifiers_EmitsSelectedRaceModifiers()
    {
        var ascension = CreateAscension(out var godState);
        godState.AscensionState.SelectedRace = RaceId.Goblin;

        var modifiers = ascension.GetModifiers().ToList();

        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_MIN_DISTANCE && m.Type == EType.REPLACER && (int)m.Value == 2);
        Assert.Contains(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == nameof(BuildingType.Sawmill) && (int)m.Value == -1);
    }

    [Fact]
    public void PermanentUniqueBuildingChoices_IncludesRacialBuildingsOfAscendedRaces()
    {
        var ascension = CreateAscension(out var godState);

        Assert.DoesNotContain(BuildingType.Ziggurat, ascension.PermanentUniqueBuildingChoices);

        godState.AscensionState.AscendedRaces.Add(RaceId.Human);
        godState.AscensionState.AscendedRaces.Add(RaceId.Elf);

        var choices = ascension.PermanentUniqueBuildingChoices;
        Assert.Contains(BuildingType.Ziggurat, choices);
        Assert.Contains(BuildingType.HeartTree, choices);
        Assert.DoesNotContain(BuildingType.RunicForge, choices);
    }

    // ── PerformAscension : enregistrement de la race et validation du choix ──

    [Fact]
    public void PerformAscension_RecordsPreviousRaceAndAppliesChosenRace()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 4;
        UnlockFirstRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Elf);

        Assert.Equal(RaceId.Elf, godState.AscensionState.SelectedRace);
        Assert.Contains(RaceId.Human, godState.AscensionState.AscendedRaces);
        // Ascension effectuée en Humain : la Ziggourat rejoint définitivement les choix permanents.
        Assert.Contains(BuildingType.Ziggurat, controller.AscensionController.PermanentUniqueBuildingChoices);
        // La nouvelle île démarre avec les modifiers elfes actifs sur la civilisation du joueur.
        var playerCiv = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization;
        Assert.True(playerCiv.ModifierAggregator.HasModifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Forest)));
    }

    [Fact]
    public void PerformAscension_NonSelectableRace_Throws()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.DivineEssence = 4;

        // Choix de race non débloqué : seule Human est acceptée.
        Assert.Throws<InvalidOperationException>(() => controller.PerformAscension(RaceId.Elf));
        // L'échec ne doit rien avoir consommé.
        Assert.Equal(4, godState.DivineEssence);
    }

    [Fact]
    public void PerformAscension_WithoutRaceUnlock_KeepsHumanFlow()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.DivineEssence = 4;

        controller.PerformAscension();

        Assert.Equal(RaceId.Human, godState.AscensionState.SelectedRace);
        Assert.Contains(RaceId.Human, godState.AscensionState.AscendedRaces);
        Assert.Equal(4, godState.GodPoints);
    }

    // ── Restrictions de placement (CityBuilderController) ────────────────────
    //
    // Layout "ruban" : h1(0,0) — h2(1,0) — h3(0,1) — h4(1,1) — h5(0,2) — h6(1,2)
    //   v1(h1,h2,h3), vMiddle(h2,h3,h4) à distance 1, v2(h3,h4,h5) à distance 2,
    //   v3(h4,h5,h6) à distance 3 de v1. h5 est la seule Forêt.

    private static HexCoord H(int q, int r) => new(q, r, IslandMap.SurfaceLayer);

    private static (WorldState state, Civilization civ, Vertex v1, Vertex v2, Vertex v3) RibbonIsland()
    {
        var h1 = H(0, 0);
        var h2 = H(1, 0);
        var h3 = H(0, 1);
        var h4 = H(1, 1);
        var h5 = H(0, 2);
        var h6 = H(1, 2);

        var map = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Plain),
            new(h2, TerrainType.Plain),
            new(h3, TerrainType.Plain),
            new(h4, TerrainType.Plain),
            new(h5, TerrainType.Forest),
            new(h6, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        civ.AddRoad(new Road(Edge.Create(h2, h3)) { CivilizationIndex = 0 });
        civ.AddRoad(new Road(Edge.Create(h3, h4)) { CivilizationIndex = 0 });
        civ.AddRoad(new Road(Edge.Create(h4, h5)) { CivilizationIndex = 0 });

        return (state, civ, Vertex.Create(h1, h2, h3), Vertex.Create(h3, h4, h5), Vertex.Create(h4, h5, h6));
    }

    private static CityBuilderController Controller(WorldState state)
    {
        var controller = new CityBuilderController();
        controller.Initialize(state);
        return controller;
    }

    private static void AddRaceModifiers(Civilization civ, params Modifier[] modifiers)
        => civ.AddCustomAggregator(new StaticModifierProvider(modifiers));

    [Fact]
    public void GetBuildableVertices_GoblinMinDistance2_AllowsCityAtDistance2()
    {
        var (state, civ, v1, v2, _) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        // Sans modifier : distance 2 < 3, bloqué.
        Assert.DoesNotContain(Controller(state).GetBuildableVertices(0), v => v.Equals(v2));

        AddRaceModifiers(civ, new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2));
        Assert.Contains(Controller(state).GetBuildableVertices(0), v => v.Equals(v2));
    }

    [Fact]
    public void GetBuildableVertices_GiantMinDistance4_BlocksCityAtDistance3()
    {
        var (state, civ, v1, _, v3) = RibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        // Sans modifier : distance 3 >= 3, constructible.
        Assert.Contains(Controller(state).GetBuildableVertices(0), v => v.Equals(v3));

        AddRaceModifiers(civ, new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 4));
        Assert.DoesNotContain(Controller(state).GetBuildableVertices(0), v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_ElfForestRestriction_OnlyKeepsForestAdjacentVertices()
    {
        var (state, civ, v1, v2, v3) = RibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Forest), EType.ADDITIVE, 1));

        var vertices = Controller(state).GetBuildableVertices(0);

        // v1 ne touche que des Plaines ; v2 et v3 touchent h5 (Forêt).
        Assert.DoesNotContain(vertices, v => v.Equals(v1));
        Assert.Contains(vertices, v => v.Equals(v2));
        Assert.Contains(vertices, v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_TerrainRestriction_SeesTerrainChangesWithoutCacheStaleness()
    {
        var (state, civ, _, v2, _) = RibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Mountain), EType.ADDITIVE, 1));
        var controller = Controller(state);

        Assert.Empty(controller.GetBuildableVertices(0));

        // Marche de Dieu peut transformer un terrain sans toucher aux compteurs de routes/villes :
        // le résultat doit suivre immédiatement (pas de cache pour les races à restriction).
        var h5 = H(0, 2);
        state.GetMapFor(h5)!.GetTile(h5)!.TerrainType = TerrainType.Mountain;

        Assert.Contains(controller.GetBuildableVertices(0), v => v.Equals(v2));
    }

    [Fact]
    public void NewCityBuildingCostFor_GreatBurrowReduction_LowersCost()
    {
        var (state, civ, v1, _, _) = RibbonIsland();
        var controller = Controller(state);

        var baseCost = controller.NewCityBuildingCostFor(v1, civ);
        Assert.Equal(10, baseCost[Resource.Brick]);
        Assert.Equal(10, baseCost[Resource.Wood]);
        Assert.Equal(15, baseCost[Resource.Food]);

        AddRaceModifiers(civ, new Modifier(ECategory.NEW_CITY_COST_REDUCTION, EType.ADDITIVE, 0.25));
        var reducedCost = controller.NewCityBuildingCostFor(v1, civ);
        Assert.Equal(8, reducedCost[Resource.Brick]);
        Assert.Equal(8, reducedCost[Resource.Wood]);
        Assert.Equal(11, reducedCost[Resource.Food]);
    }

    // ── Définitions des races ────────────────────────────────────────────────

    [Fact]
    public void RaceDefinitions_RacialBuildingsAreUniqueModifierOnlyBuildingsGatedByRace()
    {
        foreach (var race in RaceDefinitions.All.Where(r => r.RacialBuilding != null))
        {
            var prototype = BuildingController.CreateBuilding(race.RacialBuilding!.Value);
            Assert.NotNull(prototype);
            Assert.True(prototype!.IsUnique, $"{race.RacialBuilding} doit être unique");
            Assert.IsAssignableFrom<IUniqueBuilding>(prototype);
            // Niveau max par défaut 0 : seul le modifier +1 de la race le rend constructible.
            Assert.Equal(0, prototype.GetDefaultMaxLevel());
            Assert.Contains(race.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL &&
                m.SubCategory == race.RacialBuilding.Value.ToString() &&
                (int)m.Value == 1);
        }
    }

    [Fact]
    public void RaceDefinitions_GoblinMaxLevelMalus_SparesTownHallAndUniqueBuildings()
    {
        var goblin = RaceDefinitions.Get(RaceId.Goblin);
        var malus = goblin.Modifiers
            .Where(m => m.Category == ECategory.BUILDING_MAX_LEVEL && (int)m.Value == -1)
            .Select(m => m.SubCategory)
            .ToList();

        Assert.NotEmpty(malus);
        Assert.Contains(nameof(BuildingType.Sawmill), malus);
        Assert.DoesNotContain(nameof(BuildingType.TownHall), malus);
        Assert.DoesNotContain(nameof(BuildingType.WarRoom), malus);
        Assert.DoesNotContain(nameof(BuildingType.GreatBurrow), malus);
        // Temple : niveau max par défaut 1 (le +3 vient de Foi) — jamais pénalisé.
        Assert.DoesNotContain(nameof(BuildingType.Temple), malus);
    }

    // ── Ziggourat : production instantanée de Dominion ──────────────────────

    [Fact]
    public void ApplyZigguratInstantProduction_SeedsDominionOnAllCityHexesAndDispelsCorruption()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.Buildings.Add(new Temple { Level = 1 });

        var cityHexes = city.Position.GetHexes().ToList();
        state.AddFeature(new Corruption(cityHexes[0], level: 2));

        var corruptionController = new CorruptionController();
        corruptionController.Initialize(state, clock: null, new GamePRNG(1));

        corruptionController.ApplyZigguratInstantProduction(city);

        // Hex corrompu : un point de Corruption dissipé, pas encore de Dominion.
        Assert.Equal(1, state.GetFeaturesAt(cityHexes[0]).OfType<Corruption>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(cityHexes[0]).OfType<Dominion>());
        // Les deux autres hexs reçoivent chacun un Dominion niveau 1.
        Assert.Equal(1, state.GetFeaturesAt(cityHexes[1]).OfType<Dominion>().Single().Level);
        Assert.Equal(1, state.GetFeaturesAt(cityHexes[2]).OfType<Dominion>().Single().Level);
    }

    [Fact]
    public void ApplyZigguratInstantProduction_RespectsTempleDominionCap()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.Buildings.Add(new Temple { Level = 1 });

        // Temple niveau 1 : plafond 2 par hex — un Dominion déjà au plafond ne monte plus.
        var cappedHex = city.Position.GetHexes().First();
        state.AddFeature(new Dominion(cappedHex, level: 2));

        var corruptionController = new CorruptionController();
        corruptionController.Initialize(state, clock: null, new GamePRNG(1));

        corruptionController.ApplyZigguratInstantProduction(city);

        Assert.Equal(2, state.GetFeaturesAt(cappedHex).OfType<Dominion>().Single().Level);
    }
}
