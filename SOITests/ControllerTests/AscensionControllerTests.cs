using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
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
        foreach (var type in AscensionController.PermanentUniqueBuildingChoices)
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
        Assert.DoesNotContain(BuildingType.ImperialPort, AscensionController.PermanentUniqueBuildingChoices);
        Assert.DoesNotContain(BuildingType.BuildersGuild, AscensionController.PermanentUniqueBuildingChoices);
        Assert.DoesNotContain(BuildingType.AdventurersGuild, AscensionController.PermanentUniqueBuildingChoices);
        Assert.DoesNotContain(BuildingType.MilitaryAcademy, AscensionController.PermanentUniqueBuildingChoices);
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
        city.Buildings.Add(academy);
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

        // DivineInventory (colonne 0, coût 6) nécessite HandOfGod (colonne 0, coût 3) déjà débloqué,
        // même avec largement assez de points divins.
        Assert.False(ascension.CanPurchasePower(AscensionPowerId.DivineInventory));

        ascension.PurchasePower(AscensionPowerId.Faith);
        ascension.PurchasePower(AscensionPowerId.HandOfGod);

        Assert.True(ascension.CanPurchasePower(AscensionPowerId.DivineInventory));
        Assert.True(ascension.PurchasePower(AscensionPowerId.DivineInventory));
        Assert.Equal(100 - 1 - 3 - 6, godState.GodPoints);
    }

    [Fact]
    public void GetWalkOfGodCost_EscalatesByOneOnEachUse()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);
        var hex = ascension.GetWalkOfGodTargetHexes()[0];

        Assert.Equal(1, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ChangeTerrainRandomly(hex));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(2, ascension.GetWalkOfGodCost());
        Assert.True(ascension.ChangeTerrainRandomly(hex));
        Assert.Equal(7, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(3, ascension.GetWalkOfGodCost());
    }

    [Fact]
    public void ChangeTerrainRandomly_InsufficientPrestigePoints_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockWalkOfGod(ascension);
        var hex = ascension.GetWalkOfGodTargetHexes()[0];
        var terrainBefore = state.GetMapFor(hex)!.GetTile(hex)!.TerrainType;

        var result = ascension.ChangeTerrainRandomly(hex);

        Assert.False(result);
        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(0, godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige);
        Assert.Equal(terrainBefore, state.GetMapFor(hex)!.GetTile(hex)!.TerrainType);
    }

    [Fact]
    public void ChangeTerrainRandomly_NoPrestigeState_Fails()
    {
        var (_, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: null);
        UnlockWalkOfGod(ascension);
        var hex = ascension.GetWalkOfGodTargetHexes()[0];

        Assert.False(ascension.ChangeTerrainRandomly(hex));
    }

    [Fact]
    public void GetWalkOfGodCost_ReadsDirectlyFromWalkOfGodUsesSinceLastPrestige()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockWalkOfGod(ascension);

        godState.PrestigeState!.WalkOfGodUsesSinceLastPrestige = 4;

        Assert.Equal(5, ascension.GetWalkOfGodCost());
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

        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(1, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
    }

    [Fact]
    public void GetPresenceOfGodCost_EscalatesByOneOnEachUse()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);
        var hex = ascension.GetPresenceOfGodTargetHexes()[0];

        Assert.Equal(1, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(9, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(2, ascension.GetPresenceOfGodCost());
        Assert.True(ascension.ApplyPresenceOfGod(hex));
        Assert.Equal(7, godState.PrestigeState!.PrestigePoints);

        Assert.Equal(3, ascension.GetPresenceOfGodCost());
    }

    [Fact]
    public void ApplyPresenceOfGod_InsufficientPrestigePoints_FailsAndLeavesStateUntouched()
    {
        var (state, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 0);
        UnlockPresenceOfGod(ascension);
        var hex = ascension.GetPresenceOfGodTargetHexes()[0];

        Assert.False(ascension.ApplyPresenceOfGod(hex));

        Assert.Equal(0, godState.PrestigeState!.PrestigePoints);
        Assert.Equal(0, godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige);
        Assert.Empty(state.Features.OfType<Dominion>());
    }

    [Fact]
    public void GetPresenceOfGodCost_ReadsDirectlyFromPresenceOfGodUsesSinceLastPrestige()
    {
        var (_, _, _, ascension, godState) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        godState.PrestigeState!.PresenceOfGodUsesSinceLastPrestige = 4;

        Assert.Equal(5, ascension.GetPresenceOfGodCost());
    }

    [Fact]
    public void GetPresenceOfGodTargetHexes_ExcludesWater()
    {
        var (state, _, _, ascension, _) = CreateTestSetup(godPoints: 100, prestigePoints: 10);
        UnlockPresenceOfGod(ascension);

        var west = new HexCoord(-1, 0, SettlersOfIdlestan.Model.IslandMap.IslandMap.SurfaceLayer);
        state.GetMapFor(west)!.GetTile(west)!.TerrainType = SettlersOfIdlestan.Model.IslandMap.TerrainType.Water;

        var targets = ascension.GetPresenceOfGodTargetHexes();
        Assert.DoesNotContain(west, targets);
        Assert.Equal(6, targets.Count);
    }
}
