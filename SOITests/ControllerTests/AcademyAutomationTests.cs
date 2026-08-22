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

public class AcademyAutomationTests
{
    private static (WorldState state, BuildingController controller, Civilization civ) CreateSetup()
    {
        var state = IslandTestFactory.CreateSevenHexIslandState();
        var civ = state.Civilizations[0];
        var city = civ.Cities[0];
        city.AddBuilding(new TownHall { Level = 20 }); // city.Level = 20 → Library available, large basic storage cap

        // Library est verrouillée par défaut (GetDefaultMaxLevel() = 0) tant qu'aucune recherche ne
        // relève son plafond — on simule ce déblocage pour isoler le test sur l'automatisation.
        civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
        {
            new(ECategory.BUILDING_MAX_LEVEL, nameof(BuildingType.Library), EType.ADDITIVE, 1),
        }));

        var controller = new BuildingController(state);
        return (state, controller, civ);
    }

    [Fact]
    public void Academy_AutomationBuildsLibraryInCity()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new Academy { Level = 5 }); // niveau max absolu (voir Academy.GetAbsoluteMaxLevel)

        state.AutomationSettings.LibraryBuildingAutomationEnabled = true;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock); // recalcule la capacité de stockage à partir de TownHall

        civ.AddResource(Resource.Wood, 50);
        civ.AddResource(Resource.Brick, 30);
        civ.AddResource(Resource.Stone, 30);

        // First advance: sets LastLibraryBuildTick (first-fire guard)
        clock.SimulateAdvance(10);
        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Library);

        // Past cooldown: automation builds the Library
        clock.SimulateAdvance(1000);
        Assert.Contains(city.Buildings, b => b.Type == BuildingType.Library && b.Level == 1);
    }

    [Fact]
    public void Academy_AutomationDisabled_DoesNotBuildLibrary()
    {
        var (state, controller, civ) = CreateSetup();
        var city = civ.Cities[0];

        city.AddBuilding(new Academy { Level = 5 });

        state.AutomationSettings.LibraryBuildingAutomationEnabled = false;

        var clock = new GameClock();
        clock.Start();
        controller.Initialize(state, clock);

        civ.AddResource(Resource.Wood, 50);
        civ.AddResource(Resource.Brick, 30);
        civ.AddResource(Resource.Stone, 30);

        clock.SimulateAdvance(1000);

        Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.Library);
    }
}
