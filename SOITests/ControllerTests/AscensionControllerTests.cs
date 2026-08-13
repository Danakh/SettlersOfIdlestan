using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests des pouvoirs divins et du bâtiment unique permanent d'Ascension (voir AscensionController.
/// CanPurchasePower/PurchasePower, PermanentUniqueBuildingChoices/SelectPermanentUniqueBuilding/
/// ApplyPermanentUniqueBuildingToCivilization) : coût en points divins, emplacements de bâtiment
/// permanent (1 par Ascension effectuée), application à la civilisation sans occuper d'emplacement
/// en ville, blocage de la construction manuelle, survie à la perte de toutes les villes, et cumul
/// avec un bâtiment unique physiquement construit.
/// </summary>
public class AscensionControllerTests
{
    private static (WorldState state, City city, Civilization civ, AscensionController ascension, GodState godState) CreateTestSetup(
        int godPoints = 100, int ascensionsPerformed = 1, int? prestigePoints = null)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        var godState = new GodState { GodPoints = godPoints };
        godState.AscensionState.AscensionsPerformed = ascensionsPerformed;
        if (prestigePoints.HasValue)
            godState.PrestigeState = new PrestigeState(state) { PrestigePoints = prestigePoints.Value };

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState);

        return (state, city, civ, ascension, godState);
    }

    private static void UnlockWalkOfGod(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
    }

    private static void UnlockPresenceOfGod(AscensionController ascension)
    {
        UnlockWalkOfGod(ascension);
        Assert.True(ascension.PurchasePower(AscensionPowerId.PresenceOfGod));
    }

    [Fact]
    public void PermanentUniqueBuildingChoices_AreAllUniqueIUniqueBuildingTypes()
    {
        foreach (var type in new AscensionController().PermanentUniqueBuildingChoices)
        {
            var prototype = BuildingController.CreateBuilding(type);
            Assert.NotNull(prototype);
            Assert.True(prototype!.IsUnique, $"{type} should be IsUnique to be a valid ascension choice");
            Assert.IsAssignableFrom<IUniqueBuilding>(prototype);
        }
    }

    [Fact]
    public void PermanentUniqueBuildingChoices_ExcludesBuildingsWithPhysicalSideEffects()
    {
        // Bâtiments IsUnique dont l'effet dépend d'une présence physique en ville (automatisation par
        // tick, adjacence, comportement propre à l'instance) — volontairement exclus du choix.
        var choices = new AscensionController().PermanentUniqueBuildingChoices;
        Assert.DoesNotContain(BuildingType.ImperialPort, choices);
        Assert.DoesNotContain(BuildingType.BuildersGuild, choices);
        Assert.DoesNotContain(BuildingType.AdventurersGuild, choices);
        Assert.DoesNotContain(BuildingType.Garrison, choices);
    }

    [Fact]
    public void PermanentUniqueBuildingSlots_EqualsAscensionsPerformed()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(ascensionsPerformed: 3);

        Assert.Equal(3, ascension.PermanentUniqueBuildingSlots);
        Assert.Equal(3, godState.AscensionState.AscensionsPerformed);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_ValidCandidateWithSlotAvailable_ReturnsTrueAndPersistsToState()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(ascensionsPerformed: 1);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        Assert.True(result);
        Assert.Contains(BuildingType.WarRoom, ascension.PermanentUniqueBuildings);
        Assert.Contains(BuildingType.WarRoom, godState.AscensionState.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_NonCandidateType_ReturnsFalseAndLeavesStateUnset()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(ascensionsPerformed: 1);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.TownHall);

        Assert.False(result);
        Assert.Empty(ascension.PermanentUniqueBuildings);
        Assert.Empty(godState.AscensionState.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_NoSlotsAvailable_ReturnsFalse()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 0);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        Assert.False(result);
        Assert.Empty(ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_ExceedingSlotCount_ReturnsFalseAndLeavesFirstChoiceUnchanged()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        Assert.False(result);
        Assert.Equal(new[] { BuildingType.WarRoom }, ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_WithMultipleSlots_AllowsDistinctChoicesUpToLimit()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 2);

        Assert.True(ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom));
        Assert.True(ascension.SelectPermanentUniqueBuilding(BuildingType.Academy));

        Assert.Equal(2, ascension.PermanentUniqueBuildings.Count);
        Assert.Contains(BuildingType.WarRoom, ascension.PermanentUniqueBuildings);
        Assert.Contains(BuildingType.Academy, ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void DeselectPermanentUniqueBuilding_FreesSlotForAnotherChoice()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        var deselectResult = ascension.DeselectPermanentUniqueBuilding(BuildingType.WarRoom);
        var selectResult = ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        Assert.True(deselectResult);
        Assert.True(selectResult);
        Assert.Equal(new[] { BuildingType.Academy }, ascension.PermanentUniqueBuildings);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_NoneSelected_GrantsNothing()
    {
        var (_, _, civ, ascension, _) = CreateTestSetup();

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        Assert.Empty(civ.UniqueBuildings);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_RegistersBuildingAtLevelOneWithoutPhysicalInstance()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        Assert.Contains(BuildingType.WarRoom, civ.UniqueBuildings);
        var granted = civ.GetUniqueBuilding(BuildingType.WarRoom);
        Assert.NotNull(granted);
        Assert.Equal(1, granted!.Level);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.WarRoom);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_ContributesUniqueBuildingModifiers()
    {
        var (_, _, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // WarRoom.GetUniqueBuildingModifiers() : UNIT_PRODUCTION_SPEED +0.5 additif (base 1.0)
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_MultipleGrantedBuildings_BothContributeModifiers()
    {
        var (_, _, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 2);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // WarRoom : UNIT_PRODUCTION_SPEED +0.5 (base 1.0). Academy niveau 1 (accordé) :
        // RESEARCH_PRODUCTION_SPEED +0.1*1 = +0.1 (base 1.0). Les deux doivent s'appliquer.
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
        Assert.Equal(1.1, civ.ResearchProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_BlocksManualConstruction()
    {
        var (state, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        civ.AddResource(Resource.Stone, 1000);
        civ.AddResource(Resource.Gold, 1000);
        civ.AddResource(Resource.Ore, 1000);

        var buildingController = new BuildingController(state);
        var result = buildingController.BuildBuilding(city, BuildingType.WarRoom);

        Assert.False(result);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.WarRoom);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_SurvivesLossOfAllCities()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        civ.RemoveCity(city);

        Assert.Contains(BuildingType.WarRoom, civ.UniqueBuildings);
        Assert.NotNull(civ.GetUniqueBuilding(BuildingType.WarRoom));
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_CombinesWithDifferentPhysicallyBuiltUniqueBuilding()
    {
        var (_, city, civ, ascension, _) = CreateTestSetup(ascensionsPerformed: 1);
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);
        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // Bâtiment unique différent, construit normalement dans une ville.
        var academy = new Academy { Level = 2 };
        city.AddBuilding(academy);
        civ.RegisterUniqueBuildingInCache(academy);
        civ.RebuildUniqueBuildingsModifiers();

        // WarRoom (Ascension) : UNIT_PRODUCTION_SPEED +0.5. Academy niveau 2 (physique) :
        // RESEARCH_PRODUCTION_SPEED +0.1*2 = +0.2 (base 1.0). Les deux doivent s'appliquer sans se
        // marcher dessus.
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
        Assert.Equal(1.2, civ.ResearchProductionSpeed, precision: 5);
    }

    [Fact]
    public void CanPurchasePower_Faith_CostsOneGodPointAndRequiresEnoughPoints()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 0);
        Assert.False(ascension.CanPurchasePower(AscensionPowerId.Faith));

        var (_, _, _, ascensionWithPoints, _) = CreateTestSetup(godPoints: 1);
        Assert.True(ascensionWithPoints.CanPurchasePower(AscensionPowerId.Faith));
    }

    [Fact]
    public void PurchasePower_DeductsGodPointCostOnSuccess()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 5);

        var result = ascension.PurchasePower(AscensionPowerId.Faith);

        Assert.True(result);
        Assert.True(ascension.IsPowerUnlocked(AscensionPowerId.Faith));
        Assert.Equal(4, godState.GodPoints);
    }

    [Fact]
    public void PurchasePower_InsufficientGodPoints_FailsAndLeavesPointsUntouched()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 0);

        var result = ascension.PurchasePower(AscensionPowerId.Faith);

        Assert.False(result);
        Assert.False(ascension.IsPowerUnlocked(AscensionPowerId.Faith));
        Assert.Equal(0, godState.GodPoints);
    }

    [Fact]
    public void PurchasePower_SecondTierColumnPower_RequiresFirstTierUnlockedRegardlessOfPoints()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100);

        // DivineInventory (colonne 0, coût 5) nécessite HandOfGod (colonne 0, coût 3) déjà débloqué,
        // même avec largement assez de points divins.
        Assert.False(ascension.CanPurchasePower(AscensionPowerId.DivineInventory));

        ascension.PurchasePower(AscensionPowerId.Faith);
        ascension.PurchasePower(AscensionPowerId.HandOfGod);

        Assert.True(ascension.CanPurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.Equal(100 - 1 - 3 - 5, godState.GodPoints);
    }

    /// <summary>Pose un Dominion sur un hex de l'île de test — Marche de Dieu ne cible que les hexs sous Dominion de niveau 2+.</summary>
    private static (HexCoord hex, Dominion dominion) SeedDominion(WorldState state, int level, int q = 0, int r = 0)
    {
        var hex = new HexCoord(q, r, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var dominion = new Dominion(hex, level);
        state.AddFeature(dominion);
        return (hex, dominion);
    }

    [Fact]
    public void GetWalkOfGodCost_FirstUseIsFreeThenEscalatesByOne()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var (hex, dominion) = SeedDominion(state, level: 5);
        Assert.Contains(hex, ascension.GetWalkOfGodTargetHexes());

        // Première marche depuis le dernier prestige : gratuite (seul le Dominion est consommé).
        Assert.Equal(0, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(4, dominion.Level);

        Assert.Equal(1, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(3, dominion.Level);

        Assert.Equal(2, ascension.GetWalkOfGodCost());
    }

    /// <summary>
    /// La gratuité ne vaut que pour la première marche : une fois celle-ci consommée, une cagnotte
    /// vide bloque bien le pouvoir (c'est le cas nominal sur la première île d'un cycle d'Ascension,
    /// où PrestigeState.PrestigePoints part de zéro).
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_InsufficientPrestigePointsAfterTheFreeUse_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockWalkOfGod(ascension);
        godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige = 1;
        var (hex, dominion) = SeedDominion(state, level: 2);
        var terrainBefore = state.GetMapFor(hex)!.GetTile(hex)!.TerrainType;

        var result = ascension.ApplyWalkOfGod(hex);

        Assert.False(result);
        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
        Assert.Equal(terrainBefore, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Equal(2, dominion.Level);
    }

    /// <summary>Le premier usage gratuit fonctionne avec une cagnotte à zéro — l'île 1 d'une Ascension.</summary>
    [Fact]
    public void ApplyWalkOfGod_WithNoPrestigePointsAtAll_StillWorksOnce()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);

        Assert.True(ascension.CanUseWalkOfGod());
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);

        Assert.False(ascension.CanUseWalkOfGod());
        Assert.False(ascension.ApplyWalkOfGod(hex));
    }

    [Fact]
    public void ApplyWalkOfGod_NoPrestigeState_Fails()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: null);
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 2);

        Assert.False(ascension.ApplyWalkOfGod(hex));
    }

    /// <summary>
    /// Race à terrain de prédilection : marcher sur n'importe quel autre terrain y fait pousser ce
    /// terrain, de façon déterministe. C'est ce qui rend le pouvoir utile à un Elfe — retomber au
    /// hasard sur la Forêt (1 chance sur 4) ne lui ouvrirait pratiquement jamais d'emplacement.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_RaceWithFavouredTerrain_GrowsItOnAnyOtherTerrain()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Elf;
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Desert;

        Assert.Equal(TerrainType.Forest, ascension.FavouredTerrain);
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(TerrainType.Forest, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    /// <summary>
    /// Sur le terrain de prédilection lui-même, le déterminisme tombe : la marche transforme, elle ne
    /// conserve pas. Le terrain obtenu est tiré au sort, et n'est donc jamais celui de départ.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_RaceWithFavouredTerrain_OnThatTerrain_IsRandom()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Elf;
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Forest;

        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.NotEqual(TerrainType.Forest, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    /// <summary>
    /// Comme <see cref="CreateTestSetup"/>, mais avec le CityBuilderController câblé : c'est lui qui
    /// détruit les villes que le terrain transformé ne permet plus d'occuper (voir
    /// AscensionController.ApplyWalkOfGod / CityBuilderController.DestroyCitiesInvalidatedByTerrain).
    /// </summary>
    private static (WorldState state, City city, Civilization civ, AscensionController ascension) CreateWalkOfGodSetupWithCityDestruction(RaceId race)
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        var godState = new GodState { GodPoints = 100 };
        godState.PrestigeState = new PrestigeState(state) { PrestigePoints = 10 };
        godState.AscensionState.SelectedRace = race;

        // En jeu, les modifiers de la race arrivent sur la civilisation via AscensionController
        // (IModifierProvider) enregistré par MainGameController.SetupModifierAggregators ; ici on
        // les pose directement, sinon CITY_PLACEMENT_REQUIRES_TERRAIN n'existe pas côté civ et la
        // règle de validité de terrain n'a rien à vérifier.
        civ.AddCustomAggregator(new StaticModifierProvider(RaceDefinitions.Get(race).Modifiers));

        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state);

        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState, cityBuilder);
        UnlockWalkOfGod(ascension);

        return (state, city, civ, ascension);
    }

    private static void SetTerrain(WorldState state, HexCoord hex, TerrainType terrain)
        => state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = terrain;

    /// <summary>
    /// Une ville naine dont on efface la Montagne adjacente n'a plus de quoi tenir : elle est détruite
    /// par la marche qui a transformé le terrain.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_DestroysCityLeftWithoutItsRaceRequiredTerrain()
    {
        var (state, city, civ, ascension) = CreateWalkOfGodSetupWithCityDestruction(RaceId.Dwarf);

        // La ville tient sur center/NE/E : on fait de E sa seule Montagne, puis on y marche. Pour un
        // Nain, la Montagne est le terrain de prédilection : la marche y tire donc un terrain au hasard,
        // qui n'est jamais la Montagne.
        var mountainHex = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        SetTerrain(state, mountainHex, TerrainType.Mountain);
        Assert.Contains(city, civ.Cities);

        SeedDominion(state, level: 5, q: mountainHex.Q, r: mountainHex.R);
        Assert.True(ascension.ApplyWalkOfGod(mountainHex));

        Assert.NotEqual(TerrainType.Mountain, state.GetMapFor(mountainHex)!.GetTile(mountainHex)!.TerrainType);
        Assert.DoesNotContain(city, civ.Cities);
    }

    /// <summary>Une ville dont les trois hexs deviennent de l'eau est engloutie, race sans contrainte comprise.</summary>
    [Fact]
    public void ApplyWalkOfGod_DestroysCitySurroundedByWater()
    {
        var (state, city, civ, ascension) = CreateWalkOfGodSetupWithCityDestruction(RaceId.Mermaid);

        // Deux des trois hexs de la ville sont déjà noyés ; le troisième le devient par la marche —
        // l'Eau étant le terrain de prédilection des Sirènes, la transformation est déterministe.
        var lastLandHex = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        SetTerrain(state, new HexCoord(0, 1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer), TerrainType.Water);
        SetTerrain(state, new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer), TerrainType.Water);
        Assert.Contains(city, civ.Cities);

        SeedDominion(state, level: 5, q: lastLandHex.Q, r: lastLandHex.R);
        Assert.True(ascension.ApplyWalkOfGod(lastLandHex));

        Assert.Equal(TerrainType.Water, state.GetMapFor(lastLandHex)!.GetTile(lastLandHex)!.TerrainType);
        Assert.DoesNotContain(city, civ.Cities);
    }

    /// <summary>
    /// Le garde-fou de la mécanique : une ville qui reste occupable survit à la transformation d'un de
    /// ses propres hexs. Sans ça, toute marche à proximité raserait la ville.
    /// </summary>
    [Fact]
    public void ApplyWalkOfGod_LeavesStillValidCityStanding()
    {
        var (state, city, civ, ascension) = CreateWalkOfGodSetupWithCityDestruction(RaceId.Human);

        var cityHex = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        SeedDominion(state, level: 5, q: cityHex.Q, r: cityHex.R);

        Assert.True(ascension.ApplyWalkOfGod(cityHex));

        // Les deux autres hexs de la ville restent terrestres, et les Humains n'exigent aucun terrain.
        Assert.Contains(city, civ.Cities);
    }

    /// <summary>Race sans contrainte de placement : aucun terrain privilégié, tirage aléatoire comme avant.</summary>
    [Fact]
    public void ApplyWalkOfGod_RaceWithoutFavouredTerrain_JustChangesTheTerrain()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        godState.AscensionState.SelectedRace = RaceId.Human;
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 5);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Desert;

        Assert.Null(ascension.FavouredTerrain);
        Assert.True(ascension.ApplyWalkOfGod(hex));
        Assert.NotEqual(TerrainType.Desert, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    [Fact]
    public void GetWalkOfGodTargetHexes_OnlyIncludesHexesWithDominionLevel2OrMore()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        Assert.Empty(ascension.GetWalkOfGodTargetHexes());

        var (weakHex, _) = SeedDominion(state, level: 1, q: 1, r: 0);
        var (strongHex, _) = SeedDominion(state, level: 2, q: -1, r: 0);

        var targets = ascension.GetWalkOfGodTargetHexes();
        Assert.DoesNotContain(weakHex, targets);
        Assert.Contains(strongHex, targets);
        Assert.Single(targets);
    }

    [Fact]
    public void ApplyWalkOfGod_WithoutDominionLevel2_FailsAndCostsNothing()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var (hex, _) = SeedDominion(state, level: 1);

        Assert.False(ascension.ApplyWalkOfGod(hex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(0, godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void ApplyWalkOfGod_OnWaterHexWithDominion_TransformsTerrainAndReducesDominion()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var (hex, dominion) = SeedDominion(state, level: 2);
        state.GetMapFor(hex)!.GetTile(hex)!.TerrainType = TerrainType.Water;

        Assert.True(ascension.ApplyWalkOfGod(hex));

        Assert.NotEqual(TerrainType.Water, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
        Assert.Equal(1, dominion.Level);
        // Première marche : gratuite, la cagnotte est intacte.
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);

        // Retombé au niveau 1 : l'hex n'est plus ciblable tant que le Dominion n'a pas regagné du niveau.
        Assert.DoesNotContain(hex, ascension.GetWalkOfGodTargetHexes());
        Assert.False(ascension.ApplyWalkOfGod(hex));
    }

    [Fact]
    public void GetWalkOfGodCost_ReadsDirectlyFromWalkOfGodUsesSinceLastPrestige()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige = 4;

        Assert.Equal(4, ascension.GetWalkOfGodCost());
    }

    [Fact]
    public void PresenceOfGod_RequiresWalkOfGodFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.PresenceOfGod));

        Assert.True(ascension.PurchasePower(AscensionPowerId.WalkOfGod));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.PresenceOfGod));
    }

    [Fact]
    public void ApplyPresenceOfGod_DispelsCorruptionThenSeedsDominionOnAreaAndCostsPrestige()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var center = new HexCoord(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var east = new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        var west = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.AddFeature(new Corruption(center, level: 2));
        state.AddFeature(new Corruption(east, level: 10));

        Assert.True(ascension.ApplyPresenceOfGod(center));

        // Hex visé (5 points) : corruption niveau 2 dissipée, reliquat 3 en Dominion.
        Assert.Empty(state.GetFeaturesAt(center).OfType<Corruption>());
        Assert.Equal(3, state.GetFeaturesAt(center).OfType<Dominion>().Single().Level);

        // Voisin corrompu (3 points) : corruption 10 → 7, pas de Dominion.
        Assert.Equal(7, state.GetFeaturesAt(east).OfType<Corruption>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(east).OfType<Dominion>());

        // Voisin vide (3 points) : Dominion niveau 3.
        Assert.Equal(3, state.GetFeaturesAt(west).OfType<Dominion>().Single().Level);

        // Première manifestation depuis le dernier prestige : gratuite.
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void GetPresenceOfGodCost_FirstUseIsFreeThenEscalatesByOne()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);
        var hex = ascension.GetPresenceOfGodTargetHexes()[0];

        Assert.Equal(0, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(1, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(2, ascension.GetPresenceOfGodCost());
    }

    [Fact]
    public void ApplyPresenceOfGod_InsufficientPrestigePoints_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockPresenceOfGod(ascension);
        // La première manifestation étant gratuite, il faut en avoir déjà consommé une pour que
        // l'absence de points de prestige bloque le pouvoir.
        godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige = 1;
        var hex = ascension.GetPresenceOfGodTargetHexes()[0];

        Assert.False(ascension.ApplyPresenceOfGod(hex));

        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
        Assert.Empty(state.Features.OfType<Dominion>());
    }

    [Fact]
    public void GetPresenceOfGodCost_ReadsDirectlyFromPresenceOfGodUsesSinceLastPrestige()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige = 4;

        Assert.Equal(4, ascension.GetPresenceOfGodCost());
    }

    [Fact]
    public void GetPresenceOfGodTargetHexes_IncludesWater()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var west = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(west)!.GetTile(west)!.TerrainType = SettlersOfIdlestan.Model.IslandMap.TerrainType.Water;
        int tileCount = state.GetMapForZ(SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer)!.Tiles.Count;

        var targets = ascension.GetPresenceOfGodTargetHexes();
        Assert.Contains(west, targets);
        Assert.Equal(tileCount, targets.Count);
    }

    [Fact]
    public void ApplyPresenceOfGod_OnWaterHex_SeedsDominion()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var west = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(west)!.GetTile(west)!.TerrainType = SettlersOfIdlestan.Model.IslandMap.TerrainType.Water;

        Assert.True(ascension.ApplyPresenceOfGod(west));

        // L'eau est un hex valide : le Dominion y naît comme sur la terre (prélude à Marche de Dieu).
        Assert.Equal(5, state.GetFeaturesAt(west).OfType<Dominion>().Single().Level);
    }

    [Fact]
    public void GetModifiers_ArmOfGod_GrantsSoldierAttackDamageBonus()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.DoesNotContain(ascension.GetModifiers(), m => m.Category == Modifier.ECategory.SOLDIER_ATTACK_DAMAGE);

        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));

        var modifier = Assert.Single(ascension.GetModifiers()
            .Where(m => m.Category == Modifier.ECategory.SOLDIER_ATTACK_DAMAGE));
        Assert.Equal(Modifier.EType.ADDITIVE, modifier.Type);
        Assert.Equal(1, modifier.Value);
    }

    // ── Poing de Dieu ────────────────────────────────────────────────────────

    private static void UnlockFistOfGod(AscensionController ascension)
    {
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.PurchasePower(AscensionPowerId.FistOfGod));
    }

    private static readonly HexCoord Center = new(0, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);

    /// <summary>Ville ennemie posée sur un vertex adjacent au hex central, avec son Hôtel de ville.</summary>
    private static City AddEnemyCityAdjacentToCenter(WorldState state, int townHallLevel = 2, int soldiers = 0, int defense = 0)
    {
        var enemyCiv = new Civilization { Index = 1 };
        var vertex = Vertex.Create(
            Center,
            new HexCoord(1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer),
            new HexCoord(1, -1, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer));
        var city = new City(vertex) { CivilizationIndex = enemyCiv.Index, Soldiers = soldiers, CurrentDefense = defense };
        city.AddBuilding(new TownHall { Level = townHallLevel });
        enemyCiv.AddCity(city);
        state.Civilizations.Add(enemyCiv);
        return city;
    }

    [Fact]
    public void FistOfGod_RequiresArmOfGodFirstInColumn()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100);
        Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));

        Assert.False(ascension.CanPurchasePower(AscensionPowerId.FistOfGod));

        Assert.True(ascension.PurchasePower(AscensionPowerId.ArmOfGod));
        Assert.True(ascension.CanPurchasePower(AscensionPowerId.FistOfGod));
    }

    [Fact]
    public void GetFistOfGodCost_FirstUseIsFreeThenEscalatesByOne()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        Assert.Equal(0, ascension.GetFistOfGodCost());
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(10, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(1, ascension.GetFistOfGodCost());
        Assert.True(ascension.ApplyFistOfGod(Center));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(2, ascension.GetFistOfGodCost());
    }

    [Fact]
    public void ApplyFistOfGod_DamagesMonsterOnTargetedHex()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        // Rats niveau 30 : 5 + 5 × 29 = 150 PV, sans armure.
        var rats = new Rats(Center, level: 30);
        state.AddFeature(rats);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(150 - AscensionController.FistOfGodDamage, rats.Hp);
        Assert.Contains(rats, state.Features);
    }

    [Fact]
    public void ApplyFistOfGod_KillsAndRemovesMonsterItBringsToZero()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        var rats = new Rats(Center);
        state.AddFeature(rats);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.DoesNotContain(rats, state.Features);
        Assert.Equal(state.PlayerCivilization.Index, rats.KilledByCivilizationIndex);
    }

    [Fact]
    public void ApplyFistOfGod_SparesFriendlyMonsters()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        // L'Aventurier (AttacksOtherMonsters) est un allié : jamais ciblé, ici comme ailleurs.
        var adventurer = new Adventurer(Center);
        state.AddFeature(adventurer);
        int hpBefore = adventurer.Hp;

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(hpBefore, adventurer.Hp);
        Assert.Contains(adventurer, state.Features);
    }

    [Fact]
    public void ApplyFistOfGod_CascadesDamageOnAdjacentEnemyCity()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        // 100 dégâts : 60 soldats, puis 38 de défense, puis les 2 restants sur l'Hôtel de ville.
        var enemyCity = AddEnemyCityAdjacentToCenter(state, townHallLevel: 4, soldiers: 60, defense: 38);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(0, enemyCity.Soldiers);
        Assert.Equal(0, enemyCity.CurrentDefense);
        Assert.Equal(2, enemyCity.Buildings.OfType<TownHall>().Single().Level);
    }

    [Fact]
    public void ApplyFistOfGod_LeavesEnemyCityStandingWhenDamageIsAbsorbed()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        var enemyCity = AddEnemyCityAdjacentToCenter(state, townHallLevel: 3, soldiers: 200, defense: 50);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(100, enemyCity.Soldiers);
        Assert.Equal(50, enemyCity.CurrentDefense);
        Assert.Equal(3, enemyCity.Buildings.OfType<TownHall>().Single().Level);
    }

    [Fact]
    public void ApplyFistOfGod_DestroysEnemyCityThatLosesItsTownHall()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock: null, new GamePRNG(1));
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), godState, cityBuilder);
        UnlockFistOfGod(ascension);

        var enemyCity = AddEnemyCityAdjacentToCenter(state, townHallLevel: 2);

        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.DoesNotContain(enemyCity, state.Civilizations[1].Cities);
    }

    [Fact]
    public void ApplyFistOfGod_NeverDamagesPlayerCities()
    {
        var (state, city, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        city.AddBuilding(new TownHall { Level = 3 });
        city.Soldiers = 5;
        city.CurrentDefense = 7;

        // La ville du joueur est elle aussi adjacente au hex central.
        Assert.True(city.Position.IsAdjacentTo(Center));
        Assert.True(ascension.ApplyFistOfGod(Center));

        Assert.Equal(5, city.Soldiers);
        Assert.Equal(7, city.CurrentDefense);
        Assert.Equal(3, city.Buildings.OfType<TownHall>().Single().Level);
    }

    [Fact]
    public void ApplyFistOfGod_InsufficientPrestigePoints_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockFistOfGod(ascension);
        // Premier coup gratuit : il faut en avoir déjà porté un pour que le manque de points bloque.
        godState.PrestigeState!.FistOfGodUsesSinceLastPrestige = 1;

        var rats = new Rats(Center);
        state.AddFeature(rats);

        Assert.False(ascension.ApplyFistOfGod(Center));

        Assert.Equal(rats.MaxHp, rats.Hp);
        Assert.Equal(1, godState.PrestigeState!.FistOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void GetFistOfGodTargetHexes_CoversTheWholeViewedLayer()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockFistOfGod(ascension);

        var targets = ascension.GetFistOfGodTargetHexes();

        Assert.Equal(state.GetMapForZ(state.CurrentViewedLayer)!.Tiles.Count, targets.Count);
        Assert.Contains(Center, targets);
    }
}
