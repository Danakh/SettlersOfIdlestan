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
            secondaryTasks: System.Array.Empty<TutorialTask>()
        ),
    };
}
