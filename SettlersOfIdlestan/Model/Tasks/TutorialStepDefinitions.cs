using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Model.Tasks;

public static class TutorialStepDefinitions
{
    private static TutorialTask Task(TutorialTaskId id)
        => TutorialTaskDefinitions.All.First(t => t.Id == id);

    public static readonly IReadOnlyList<TutorialStep> All = new[]
    {
        new TutorialStep(
            "tutorial_step_harvest_title",
            "tutorial_step_harvest_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.Harvest5Wood),
                Task(TutorialTaskId.Harvest5Brick),
            },
            secondaryTasks: System.Array.Empty<TutorialTask>()
        ),

        new TutorialStep(
            "tutorial_step_build_title",
            "tutorial_step_build_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.BuildSeaport),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.BuildSawmill),
                Task(TutorialTaskId.BuildBrickworks),
            }
        ),

        new TutorialStep(
            "tutorial_step_road_title",
            "tutorial_step_road_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.BuildFirstRoad),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.Harvest15Food),
            }
        ),

        new TutorialStep(
            "tutorial_step_expansion_title",
            "tutorial_step_expansion_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.BuildSecondCity),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.UpgradeProductionBuildingsLevel2),
            }
        ),

        new TutorialStep(
            "tutorial_step_empire_title",
            "tutorial_step_empire_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.Build10Cities),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.Build5Palisades),
            }
        ),

        new TutorialStep(
            "tutorial_step_trade_title",
            "tutorial_step_trade_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.Build1Warehouse),
                Task(TutorialTaskId.Trade10Gold),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.Build3Warehouses),
            }
        ),

        new TutorialStep(
            "tutorial_step_capital_title",
            "tutorial_step_capital_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.SeaportLevel4),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.TownHallLevel4),
            }
        ),

        new TutorialStep(
            "tutorial_step_imperial_port_title",
            "tutorial_step_imperial_port_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.BuildImperialPort),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.Build5Libraries),
            }
        ),

        new TutorialStep(
            "tutorial_step_prestige_title",
            "tutorial_step_prestige_desc",
            primaryTasks: new[]
            {
                Task(TutorialTaskId.PerformPrestige),
            },
            secondaryTasks: new[]
            {
                Task(TutorialTaskId.Reach30VictoryPoints),
            }
        ),
    };
}
