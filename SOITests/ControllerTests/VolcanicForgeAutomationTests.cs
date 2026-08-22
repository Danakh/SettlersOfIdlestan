using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

public class VolcanicForgeAutomationTests
{
    private static readonly HexCoord EastHex = new(1, 0, IslandMap.SurfaceLayer);

    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → MithrilMine available
        city.AddBuilding(new Warehouse { Level = 10 }); // MithrilMine coûte Acier/Or (ressources avancées)

        // MithrilMine exige un hex Filon de Mithril adjacent — on convertit l'hex Est de l'île de test.
        state.GetMapFor(EastHex)!.GetTile(EastHex)!.TerrainType = TerrainType.MithrilVein;

        // MithrilMine est verrouillée par défaut (GetDefaultMaxLevel() = 0) tant qu'aucun vertex de
        // prestige ne relève son plafond — on simule ce déblocage pour isoler le test sur l'automatisation.
        civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
        {
            new(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.MithrilMine), EType.ADDITIVE, 1),
        }));

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void VolcanicForge_AutomationBuildsMithrilMineInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new VolcanicForge { Level = 3 }); // niveau max absolu (voir VolcanicForge.GetAbsoluteMaxLevel)

        state.AutomationSettings.MithrilMineBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Stone, 100);
        civ.AddResource(Resource.Steel, 20);
        civ.AddResource(Resource.Gold, 70);

        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.MithrilMine);

        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.MithrilMine && b.Level == 1);
    }

    [Fact]
    public void VolcanicForge_AutomationDisabled_DoesNotBuildMithrilMine()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new VolcanicForge { Level = 3 });

        state.AutomationSettings.MithrilMineBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Stone, 100);
        civ.AddResource(Resource.Steel, 20);
        civ.AddResource(Resource.Gold, 70);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.MithrilMine);
    }
}
