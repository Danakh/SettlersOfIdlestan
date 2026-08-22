using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

public class ArcaneTowerAutomationTests
{
    private static readonly HexCoord CenterHex = new(0, 0, IslandMap.SurfaceLayer);

    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → AlchimistHut available
        city.AddBuilding(new Warehouse { Level = 10 }); // AlchimistHut coûte du Verre/Or (ressources avancées)

        // AlchimistHut exige un Cercle de Fées découvert adjacent.
        state.AddFeature(new FairyCircle(CenterHex) { Found = true });

        // AlchimistHut est verrouillée par défaut (GetDefaultMaxLevel() = 0) tant qu'aucun vertex de
        // prestige ne relève son plafond — on simule ce déblocage pour isoler le test sur l'automatisation.
        civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
        {
            new(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.AlchimistHut), EType.ADDITIVE, 1),
        }));

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void ArcaneTower_AutomationBuildsAlchimistHutInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new ArcaneTower { Level = 1 }); // niveau max absolu (voir ArcaneTower.GetAbsoluteMaxLevel)

        state.AutomationSettings.ArcaneTowerBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Stone, 70);
        civ.AddResource(Resource.Glass, 20);
        civ.AddResource(Resource.Gold, 70);

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.AlchimistHut);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.AlchimistHut && b.Level == 1);
    }

    [Fact]
    public void ArcaneTower_AutomationDisabled_DoesNotBuildAlchimistHut()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new ArcaneTower { Level = 1 });

        state.AutomationSettings.ArcaneTowerBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Stone, 70);
        civ.AddResource(Resource.Glass, 20);
        civ.AddResource(Resource.Gold, 70);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.AlchimistHut);
    }
}
