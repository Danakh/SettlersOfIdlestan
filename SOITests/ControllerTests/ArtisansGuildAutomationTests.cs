using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class ArtisansGuildAutomationTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → Forge available, large basic storage cap
        city.AddBuilding(new Warehouse { Level = 10 }); // Forge coûte du Minerai (ressource avancée, cap 0 sans Entrepôt)

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void ArtisansGuild_AutomationBuildsForgeInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new ArtisansGuild { Level = 1 }); // niveau max absolu (voir ArtisansGuild.GetAbsoluteMaxLevel)

        state.AutomationSettings.ArtisanBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock); // recalcule la capacité de stockage à partir de TownHall/Entrepôt

        civ.AddResource(Resource.Brick, 50);
        civ.AddResource(Resource.Stone, 30);
        civ.AddResource(Resource.Ore, 30);

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Forge);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Forge && b.Level == 1);
    }

    [Fact]
    public void ArtisansGuild_AutomationDisabled_DoesNotBuildForge()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new ArtisansGuild { Level = 1 });

        state.AutomationSettings.ArtisanBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Brick, 50);
        civ.AddResource(Resource.Stone, 30);
        civ.AddResource(Resource.Ore, 30);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Forge);
    }
}
