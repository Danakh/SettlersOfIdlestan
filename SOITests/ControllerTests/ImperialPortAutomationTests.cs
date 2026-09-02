using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class ImperialPortAutomationTests
{
    private static readonly HexCoord EastHex = new(1, 0, IslandMap.SurfaceLayer);

    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];

        // Seaport exige un hex Eau adjacent — on convertit l'hex Est de l'île de test.
        state.GetMapFor(EastHex)!.GetTile(EastHex)!.TerrainType = TerrainType.Water;

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void ImperialPort_AutomationBuildsSeaportInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        // Niveau 2 = niveau max débloqué par le vertex de prestige Port Impérial (BUILDING_MAX_LEVEL),
        // requis pour l'automatisation des ports (voir BuildingController.PerformImperialPortSeaportAutomation).
        city.AddBuilding(new ImperialPort { Level = 2 });

        state.AutomationSettings.SeaportBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 10); // cap = 10 (base, sans TownHall) ; coût exact du Port

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Seaport);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Seaport && b.Level == 1);
    }

    [Fact]
    public void ImperialPort_AutomationDisabled_DoesNotBuildSeaport()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new ImperialPort { Level = 2 });

        state.AutomationSettings.SeaportBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 10);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Seaport);
    }

    [Fact]
    public void ImperialPort_BelowLevel2_DoesNotBuildSeaport()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        state.GetMapFor(EastHex)!.GetTile(EastHex)!.TerrainType = TerrainType.Water;

        // Niveau 1 (avant achat du vertex de prestige Port Impérial) : automatisation non débloquée.
        city.AddBuilding(new ImperialPort { Level = 1 });
        state.AutomationSettings.SeaportBuildingAutomationEnabled = true;

        var controller = new BuildingController(state);
        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 10);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Seaport);
    }
}
