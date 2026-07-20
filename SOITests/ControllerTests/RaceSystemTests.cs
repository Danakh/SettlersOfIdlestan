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
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
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
/// placement (CITY_MIN_DISTANCE, CITY_PLACEMENT_REQUIRES_TERRAIN, CITY_PLACEMENT_FLYING),
/// réduction de coût de ville (NEW_CITY_COST_REDUCTION) et effet Ziggourat (production
/// instantanée de Dominion des Temples).
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
    public void GetSelectableRaces_FirstRowOnly_ExcludesAdvancedRaces()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);

        var races = ascension.GetSelectableRaces();

        Assert.Contains(RaceId.Human, races);
        Assert.Contains(RaceId.Elf, races);
        Assert.Contains(RaceId.Dwarf, races);
        Assert.Contains(RaceId.Goblin, races);
        Assert.Contains(RaceId.Orc, races);
        // Géants et Garudas : races avancées, verrouillées tant que la seconde rangée de pouvoirs
        // n'est pas complète ; Sirènes et Elfes noirs : stubs non implémentés, jamais sélectionnables.
        Assert.DoesNotContain(RaceId.Mermaid, races);
        Assert.DoesNotContain(RaceId.DarkElf, races);
        Assert.DoesNotContain(RaceId.Giant, races);
        Assert.DoesNotContain(RaceId.Garuda, races);
    }

    [Fact]
    public void GetSelectableRaces_SecondRowComplete_AddsImplementedAdvancedRaces()
    {
        var ascension = CreateAscension(out _);
        UnlockFirstRow(ascension);
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.PurchasePower(AscensionPowerId.PresenceOfGod));

        var races = ascension.GetSelectableRaces();

        Assert.Contains(RaceId.Giant, races);
        Assert.Contains(RaceId.Garuda, races);
        // Les stubs restent non sélectionnables même seconde rangée complète.
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
    public void GetModifiers_Garuda_EmitsFlightMinDistanceAndAttackRange()
    {
        var ascension = CreateAscension(out var godState);
        godState.AscensionState.SelectedRace = RaceId.Garuda;

        var modifiers = ascension.GetModifiers().ToList();

        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_PLACEMENT_FLYING && m.Type == EType.ADDITIVE && (int)m.Value == 3);
        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_MIN_DISTANCE && m.Type == EType.REPLACER && (int)m.Value == 2);
        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_ATTACK_RANGE && (int)m.Value == 1);
        Assert.Contains(modifiers, m => m.Category == ECategory.CITY_DEFENSE && (int)m.Value == -3);
        // Constructions légères : même malus standard que les Gobelins.
        Assert.Contains(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == nameof(BuildingType.Sawmill) && (int)m.Value == -1);
        Assert.Contains(modifiers, m => m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == nameof(BuildingType.ThroneOfWinds) && (int)m.Value == 1);
    }

    [Fact]
    public void PermanentUniqueBuildingChoices_IncludesRacialBuildingsOfAscendedRaces()
    {
        var ascension = CreateAscension(out var godState);

        Assert.DoesNotContain(BuildingType.Ziggurat, ascension.PermanentUniqueBuildingChoices);

        godState.AscensionState.AscendedRaces.Add(RaceId.Human);
        godState.AscensionState.AscendedRaces.Add(RaceId.Elf);
        godState.AscensionState.AscendedRaces.Add(RaceId.Garuda);

        var choices = ascension.PermanentUniqueBuildingChoices;
        Assert.Contains(BuildingType.Ziggurat, choices);
        Assert.Contains(BuildingType.HeartTree, choices);
        Assert.Contains(BuildingType.ThroneOfWinds, choices);
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
        // Sans Foi débloquée, aucun vertex de prestige n'est offert.
        Assert.Empty(controller.CurrentMainState.PrestigeState!.PurchasedVertices);
    }

    // ── Vertex de prestige offerts à l'Ascension ─────────────────────────────

    [Fact]
    public void PerformAscension_WithFaithOnly_GrantsCentralPrestigeVertexOnly()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 4;
        Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.Faith));

        controller.PerformAscension();

        var purchased = controller.CurrentMainState.PrestigeState!.PurchasedVertices;
        Assert.Contains(PrestigeMap.CentralVertex, purchased);
        Assert.Single(purchased);
    }

    [Fact]
    public void PerformAscension_WithRacesUnlocked_GrantsCentralVertexAndItsThreeNeighborsFree()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 4;
        UnlockFirstRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Dwarf);

        var prestigeState = controller.CurrentMainState.PrestigeState!;
        var purchased = prestigeState.PurchasedVertices;
        Assert.Contains(PrestigeMap.CentralVertex, purchased);
        var neighbors = PrestigeMapController.DefaultMap.GetNeighbors(PrestigeMap.CentralVertex);
        Assert.Equal(3, neighbors.Count);
        foreach (var neighbor in neighbors)
            Assert.Contains(neighbor.Coord, purchased);
        Assert.Equal(4, purchased.Count);
        // Gratuit = aucun point de prestige dépensé.
        Assert.Equal(0, prestigeState.PrestigePoints);
        // Le voisin Port & Marché garantit un Marché de départ : la civilisation peut acheter la
        // ressource que son terrain de départ ne produit pas (ex. la brique des Nains).
        var startingCity = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization.Cities[0];
        Assert.Contains(startingCity.Buildings, b => b.Type == BuildingType.Market);
    }

    [Fact]
    public void PerformAscension_Dwarf_StartVertexTouchesMountainForestAndWater()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 4;
        UnlockFirstRow(controller.AscensionController);

        controller.PerformAscension(RaceId.Dwarf);

        // Le générateur remplace la Colline par la Montagne dans la paire de départ : la capitale
        // naine respecte sa propre restriction de placement.
        var worldState = controller.CurrentMainState.CurrentWorldState!;
        var startingCity = worldState.PlayerCivilization.Cities[0];
        var map = worldState.GetMapFor(startingCity.Position)!;
        Assert.True(map.VertexHasTerrainType(startingCity.Position, TerrainType.Mountain));
        Assert.True(map.VertexHasTerrainType(startingCity.Position, TerrainType.Forest));
        Assert.True(map.VertexHasTerrainType(startingCity.Position, TerrainType.Water));
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
    public void GetBuildableVertices_TerrainRestriction_CacheInvalidatedByNotifyTerrainChanged()
    {
        var (state, civ, _, v2, _) = RibbonIsland();
        AddRaceModifiers(civ, new Modifier(ECategory.CITY_PLACEMENT_REQUIRES_TERRAIN, nameof(TerrainType.Mountain), EType.ADDITIVE, 1));
        var controller = Controller(state);

        Assert.Empty(controller.GetBuildableVertices(0));

        // Marche de Dieu transforme un terrain sans toucher aux compteurs de routes/villes : le
        // cache de GetBuildableVertices est invalidé via WorldState.NotifyTerrainChanged
        // (TerrainVersion), appelé par tous les mutateurs de terrain.
        var h5 = H(0, 2);
        state.GetMapFor(h5)!.GetTile(h5)!.TerrainType = TerrainType.Mountain;
        state.NotifyTerrainChanged();

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

    // ── Vol (CITY_PLACEMENT_FLYING, Garudas) ─────────────────────────────────
    //
    // Ruban sans aucune route : seul le Vol peut fournir des candidats. Prolongé d'un hex
    // h7(0,3) pour obtenir v4 à distance 4 de v1 (hors portée de vol 3).

    private static (WorldState state, Civilization civ, Vertex v1, Vertex vMiddle, Vertex v2, Vertex v3, Vertex v4)
        RoadlessRibbonIsland(bool waterStrait = false)
    {
        var h1 = H(0, 0);
        var h2 = H(1, 0);
        var h3 = H(0, 1);
        var h4 = H(1, 1);
        var h5 = H(0, 2);
        var h6 = H(1, 2);
        var h7 = H(0, 3);

        // waterStrait : h3/h4/h5 en Eau — v2(h3,h4,h5) devient un vertex tout-eau, v3 reste
        // terrestre de l'autre côté du bras de mer.
        var strait = waterStrait ? TerrainType.Water : TerrainType.Plain;
        var map = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Plain),
            new(h2, TerrainType.Plain),
            new(h3, strait),
            new(h4, strait),
            new(h5, strait),
            new(h6, TerrainType.Plain),
            new(h7, TerrainType.Plain),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        return (state, civ,
            Vertex.Create(h1, h2, h3),
            Vertex.Create(h2, h3, h4),
            Vertex.Create(h3, h4, h5),
            Vertex.Create(h4, h5, h6),
            Vertex.Create(h5, h6, h7));
    }

    private static void AddGarudaPlacementModifiers(Civilization civ)
        => AddRaceModifiers(civ,
            new Modifier(ECategory.CITY_PLACEMENT_FLYING, EType.ADDITIVE, 3),
            new Modifier(ECategory.CITY_MIN_DISTANCE, EType.REPLACER, 2));

    [Fact]
    public void GetBuildableVertices_Flight_AllowsRoadlessVerticesWithinRange()
    {
        var (state, civ, v1, _, v2, v3, _) = RoadlessRibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });

        // Sans Vol : aucune route, aucun candidat.
        Assert.Empty(Controller(state).GetBuildableVertices(0));

        AddGarudaPlacementModifiers(civ);
        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.Contains(vertices, v => v.Equals(v2));
        Assert.Contains(vertices, v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_Flight_RespectsMinDistanceAndRange()
    {
        var (state, civ, v1, vMiddle, _, _, v4) = RoadlessRibbonIsland();
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        AddGarudaPlacementModifiers(civ);

        var vertices = Controller(state).GetBuildableVertices(0);

        // Distance 1 < distance minimale 2 : trop proche même en volant.
        Assert.DoesNotContain(vertices, v => v.Equals(vMiddle));
        // Distance 4 > portée de vol 3 : hors d'atteinte.
        Assert.DoesNotContain(vertices, v => v.Equals(v4));
    }

    [Fact]
    public void GetBuildableVertices_Flight_FliesOverWaterButNeverLandsOnAllWaterVertex()
    {
        var (state, civ, v1, _, v2, v3, _) = RoadlessRibbonIsland(waterStrait: true);
        civ.AddCity(new City(v1) { CivilizationIndex = 0 });
        AddGarudaPlacementModifiers(civ);

        var vertices = Controller(state).GetBuildableVertices(0);

        // v2 ne touche que de l'Eau : pas d'atterrissage en pleine mer.
        Assert.DoesNotContain(vertices, v => v.Equals(v2));
        // v3, terrestre de l'autre côté du bras de mer, est atteint en le survolant.
        Assert.Contains(vertices, v => v.Equals(v3));
    }

    [Fact]
    public void GetBuildableVertices_Flight_SurfaceOnly()
    {
        var (state, civ, _, _, _, _, _) = RoadlessRibbonIsland();

        // Ville d'Inframonde : le Vol ne part que des villes de surface — aucun candidat.
        var hu1 = new HexCoord(0, 0, LayerState.UnderworldZ);
        var hu2 = new HexCoord(1, 0, LayerState.UnderworldZ);
        var hu3 = new HexCoord(0, 1, LayerState.UnderworldZ);
        civ.AddCity(new City(Vertex.Create(hu1, hu2, hu3)) { CivilizationIndex = 0 });
        AddGarudaPlacementModifiers(civ);

        Assert.Empty(Controller(state).GetBuildableVertices(0));
    }

    [Fact]
    public void PerformAscension_Garuda_AfterSecondRow_AppliesFlightToPlayerCivilization()
    {
        var controller = new MainGameController();
        controller.CreateNewGame();
        var godState = controller.CurrentMainState!.GodState;
        godState.GodPoints = 100;
        godState.DivineEssence = 4;
        UnlockFirstRow(controller.AscensionController);
        Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(controller.AscensionController.PurchasePower(AscensionPowerId.PresenceOfGod));

        controller.PerformAscension(RaceId.Garuda);

        Assert.Equal(RaceId.Garuda, godState.AscensionState.SelectedRace);
        var playerCiv = controller.CurrentMainState.CurrentWorldState!.PlayerCivilization;
        Assert.True(playerCiv.ModifierAggregator.HasModifier(ECategory.CITY_PLACEMENT_FLYING));
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
