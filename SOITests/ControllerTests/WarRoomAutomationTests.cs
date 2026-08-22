using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

public class WarRoomAutomationTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → Barracks available

        // Barracks est verrouillée par défaut (GetDefaultMaxLevel() = 0) tant qu'aucun vertex de
        // prestige ne relève son plafond — on simule ce déblocage pour isoler le test sur l'automatisation.
        civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
        {
            new(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Barracks), EType.ADDITIVE, 1),
        }));

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void WarRoom_AutomationBuildsBarracksInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new WarRoom { Level = 1 }); // niveau max absolu (voir WarRoom.GetAbsoluteMaxLevel)

        state.AutomationSettings.MilitaryBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Food, 30);
        civ.AddResource(Resource.Wood, 30);
        civ.AddResource(Resource.Stone, 60);

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Barracks);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Barracks && b.Level == 1);
    }

    [Fact]
    public void WarRoom_AutomationDisabled_DoesNotBuildBarracks()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new WarRoom { Level = 1 });

        state.AutomationSettings.MilitaryBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Food, 30);
        civ.AddResource(Resource.Wood, 30);
        civ.AddResource(Resource.Stone, 60);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Barracks);
    }
}
