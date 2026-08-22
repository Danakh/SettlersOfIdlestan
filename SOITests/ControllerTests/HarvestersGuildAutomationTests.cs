using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class HarvestersGuildAutomationTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → Sawmill available (E hex = Forest)

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void HarvestersGuild_AutomationBuildsSawmillInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new HarvestersGuild { Level = 4 }); // niveau max absolu (voir HarvestersGuild.GetAbsoluteMaxLevel)

        state.AutomationSettings.ProductionBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 20);
        civ.AddResource(Resource.Brick, 10);

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Sawmill);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Sawmill && b.Level == 1);
    }

    [Fact]
    public void HarvestersGuild_AutomationDisabled_DoesNotBuildSawmill()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new HarvestersGuild { Level = 4 });

        state.AutomationSettings.ProductionBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 20);
        civ.AddResource(Resource.Brick, 10);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Sawmill);
    }
}
