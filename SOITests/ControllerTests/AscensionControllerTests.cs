using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests du bâtiment unique permanent d'Ascension (voir AscensionController.
/// PermanentUniqueBuildingChoices / SelectPermanentUniqueBuilding / ApplyPermanentUniqueBuildingToCivilization) :
/// choix, application à la civilisation sans occuper d'emplacement en ville, blocage de la
/// construction manuelle, survie à la perte de toutes les villes, et cumul avec un bâtiment unique
/// physiquement construit.
/// </summary>
public class AscensionControllerTests
{
    private static (WorldState state, City city, Civilization civ, AscensionController ascension, AscensionState ascensionState) CreateTestSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        var ascensionState = new AscensionState();
        var ascension = new AscensionController();
        ascension.Initialize(state, clock: null, new GamePRNG(1), new HarvestController(), ascensionState);

        return (state, city, civ, ascension, ascensionState);
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
    public void SelectPermanentUniqueBuilding_ValidCandidate_ReturnsTrueAndPersistsToState()
    {
        var (_, _, _, ascension, ascensionState) = CreateTestSetup();

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        Assert.True(result);
        Assert.Equal(BuildingType.WarRoom, ascension.PermanentUniqueBuilding);
        Assert.Equal(BuildingType.WarRoom, ascensionState.PermanentUniqueBuilding);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_NonCandidateType_ReturnsFalseAndLeavesStateUnset()
    {
        var (_, _, _, ascension, ascensionState) = CreateTestSetup();

        var result = ascension.SelectPermanentUniqueBuilding(BuildingType.TownHall);

        Assert.False(result);
        Assert.Null(ascension.PermanentUniqueBuilding);
        Assert.Null(ascensionState.PermanentUniqueBuilding);
    }

    [Fact]
    public void SelectPermanentUniqueBuilding_ChangingSelection_UpdatesToLatestChoice()
    {
        var (_, _, _, ascension, ascensionState) = CreateTestSetup();
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        ascension.SelectPermanentUniqueBuilding(BuildingType.Academy);

        Assert.Equal(BuildingType.Academy, ascension.PermanentUniqueBuilding);
        Assert.Equal(BuildingType.Academy, ascensionState.PermanentUniqueBuilding);
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
        var (_, city, civ, ascension, _) = CreateTestSetup();
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
        var (_, _, civ, ascension, _) = CreateTestSetup();
        ascension.SelectPermanentUniqueBuilding(BuildingType.WarRoom);

        ascension.ApplyPermanentUniqueBuildingToCivilization();

        // WarRoom.GetUniqueBuildingModifiers() : UNIT_PRODUCTION_SPEED +0.5 additif (base 1.0)
        Assert.Equal(1.5, civ.UnitProductionSpeed, precision: 5);
    }

    [Fact]
    public void ApplyPermanentUniqueBuildingToCivilization_BlocksManualConstruction()
    {
        var (state, city, civ, ascension, _) = CreateTestSetup();
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
        var (_, city, civ, ascension, _) = CreateTestSetup();
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
        var (_, city, civ, ascension, _) = CreateTestSetup();
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
}
