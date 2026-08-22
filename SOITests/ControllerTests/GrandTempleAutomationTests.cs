using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class GrandTempleAutomationTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → Temple available

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void GrandTemple_AutomationBuildsTempleInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new GrandTemple { Level = 1 }); // niveau max absolu (voir GrandTemple.GetAbsoluteMaxLevel)

        state.AutomationSettings.TempleAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Brick, 30);
        civ.AddResource(Resource.Stone, 30);

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Temple);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Temple && b.Level == 1);
    }

    [Fact]
    public void GrandTemple_AutomationDisabled_DoesNotBuildTemple()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new GrandTemple { Level = 1 });

        state.AutomationSettings.TempleAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Brick, 30);
        civ.AddResource(Resource.Stone, 30);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Temple);
    }
}
