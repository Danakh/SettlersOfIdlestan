using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class BuildingControllerTests
{
    private static (IslandState state, BuildingController controller, Vertex cityVertex) CreateTestSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var controller = new BuildingController(state);
        var cityVertex = state.Civilizations[0].Cities[0].Position;
        return (state, controller, cityVertex);
    }

    [Fact]
    public void GetBuildingsAndBuildables_NewCity_ReturnsTownHall()
    {
        var (state, controller, cityVertex) = CreateTestSetup();

        var buildings = controller.GetBuildingsAndBuildables(0, cityVertex);

        // A new city (level 0) should at least have the TownHall available (AvailableAtLevel = 0)
        Assert.Contains(buildings, b => b.Type == BuildingType.TownHall);
    }

    [Fact]
    public void GetBuildingsAndBuildables_NewCity_DoesNotReturnHighLevelBuildings()
    {
        var (state, controller, cityVertex) = CreateTestSetup();

        var buildings = controller.GetBuildingsAndBuildables(0, cityVertex);

        // Buildings with AvailableAtLevel > 0 should not appear when city level is 0
        Assert.DoesNotContain(buildings, b => b.Type == BuildingType.Library);
        Assert.DoesNotContain(buildings, b => b.Type == BuildingType.Forge);
    }

    [Fact]
    public void GetBuildingsAndBuildables_WithTownHall_ReturnsMoreBuildings()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var city = state.Civilizations[0].Cities[0];

        // Add a TownHall at level 1 so city.Level becomes 1
        var townHall = new TownHall { Level = 1 };
        city.Buildings.Add(townHall);

        var buildings = controller.GetBuildingsAndBuildables(0, cityVertex);

        // TownHall should be the existing one
        Assert.Contains(buildings, b => b.Type == BuildingType.TownHall && b.Level == 1);
        // Level-1 buildings that match terrain should now appear
        // City vertex is at (center=Plain, ne=Plain, e=Forest) so Mill (needs Plain) should be available
        Assert.Contains(buildings, b => b.Type == BuildingType.Mill);
    }

    [Fact]
    public void GetBuildingsAndBuildables_ResultIsSortedByAvailableAtLevel()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var city = state.Civilizations[0].Cities[0];

        // Set city to level 2 so more buildings appear
        var townHall = new TownHall { Level = 2 };
        city.Buildings.Add(townHall);

        var buildings = controller.GetBuildingsAndBuildables(0, cityVertex);

        for (int i = 1; i < buildings.Count; i++)
        {
            Assert.True(buildings[i - 1].AvailableAtLevel <= buildings[i].AvailableAtLevel,
                "Buildings should be sorted by AvailableAtLevel");
        }
    }

    [Fact]
    public void BuildBuilding_TownHall_ConsumesResourcesAndAddsBuilding()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var civ = state.Civilizations[0];

        // TownHall costs: 2 Food, 2 Wood, 2 Brick
        civ.AddResource(Resource.Food, 2);
        civ.AddResource(Resource.Wood, 2);
        civ.AddResource(Resource.Brick, 2);

        var result = controller.BuildBuilding(0, cityVertex, BuildingType.TownHall);

        Assert.True(result);
        var city = civ.Cities[0];
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.TownHall && b.Level == 1);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
    }

    [Fact]
    public void BuildBuilding_NotEnoughResources_ReturnsFalse()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var civ = state.Civilizations[0];

        // Don't add any resources
        var result = controller.BuildBuilding(0, cityVertex, BuildingType.TownHall);

        Assert.False(result);
        Assert.Empty(civ.Cities[0].Buildings);
    }

    [Fact]
    public void BuildBuilding_UpgradeExistingBuilding_IncrementsLevel()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        // Add a TownHall at level 1
        var townHall = new TownHall { Level = 1 };
        city.Buildings.Add(townHall);

        // Upgrade cost for level 2: 2*(4+1)=10 each for Food/Wood/Brick/Stone
        civ.AddResource(Resource.Food, 10);
        civ.AddResource(Resource.Wood, 10);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Stone, 10);

        var result = controller.BuildBuilding(0, cityVertex, BuildingType.TownHall);

        Assert.True(result);
        Assert.Equal(2, townHall.Level);
    }

    [Fact]
    public void BuildBuilding_AtMaxLevel_ReturnsFalse()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        var townHall = new TownHall { Level = 4 }; // max level is 4
        city.Buildings.Add(townHall);

        // Give plenty of resources
        civ.AddResource(Resource.Food, 1000);
        civ.AddResource(Resource.Wood, 1000);
        civ.AddResource(Resource.Brick, 1000);
        civ.AddResource(Resource.Stone, 1000);
        civ.AddResource(Resource.Gold, 1000);
        civ.AddResource(Resource.Glass, 1000);

        var result = controller.BuildBuilding(0, cityVertex, BuildingType.TownHall);

        Assert.False(result);
        Assert.Equal(4, townHall.Level);
    }

    [Fact]
    public void BuildBuilding_UnavailableBuilding_ReturnsFalse()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var civ = state.Civilizations[0];

        // City level is 0, Mill requires level 1
        civ.AddResource(Resource.Wood, 100);
        civ.AddResource(Resource.Brick, 100);

        var result = controller.BuildBuilding(0, cityVertex, BuildingType.Mill);

        Assert.False(result);
    }

    [Fact]
    public void GetMaxLevel_WithTechTreeModifier_IncreasesLibraryMaxLevel()
    {
        var (state, controller, cityVertex) = CreateTestSetup();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        // Add a TownHall at level 2 to unlock Library (AvailableAtLevel = 2)
        var townHall = new TownHall { Level = 2 };
        city.Buildings.Add(townHall);

        // Create a Library building
        var library = new Library { Level = 0 };
        city.Buildings.Add(library);

        // Check initial max level (Library.GetDefaultMaxLevel() returns 4)
        int maxLevelBefore = controller.GetMaxLevel(library, 0);
        Assert.Equal(1, maxLevelBefore);

        // Add a modifier to the TechnologyTree that increases Library max level by 3
        var modifier = new SettlersOfIdlestan.Model.GameplayModifier.Modifier(
            SettlersOfIdlestan.Model.GameplayModifier.Modifier.ECategory.BUILDING_MAX_LEVEL,
            BuildingType.Library.ToString(),
            SettlersOfIdlestan.Model.GameplayModifier.Modifier.EType.ADDITIVE,
            3);
        civ.TechnologyTree.Modifiers.Add(modifier);

        // Check max level after modifier (should be 4: 1 + 3)
        int maxLevelAfter = controller.GetMaxLevel(library, 0);
        Assert.Equal(4, maxLevelAfter);
    }
}
