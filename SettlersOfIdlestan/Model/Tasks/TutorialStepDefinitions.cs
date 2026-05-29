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
            "tutorial_step_expansion_title",
            "tutorial_step_expansion_desc",
            primaryTasks: new[] { Task(TutorialTaskId.BuildFirstRoad), Task(TutorialTaskId.BuildFirstCity) },
            secondaryTasks: new[] { Task(TutorialTaskId.BuildFirst5Roads), Task(TutorialTaskId.BuildSecondCity) }
        ),

        new TutorialStep(
            "tutorial_step_economy_title",
            "tutorial_step_economy_desc",
            primaryTasks: new[] { Task(TutorialTaskId.BuildFirstSawmill), Task(TutorialTaskId.BuildFirstMarket) },
            secondaryTasks: new[] { Task(TutorialTaskId.BuildFirstWarehouse), Task(TutorialTaskId.Upgrade3Buildings) }
        ),

        new TutorialStep(
            "tutorial_step_research_title",
            "tutorial_step_research_desc",
            primaryTasks: new[] { Task(TutorialTaskId.CompleteFirstResearch) },
            secondaryTasks: new[] { Task(TutorialTaskId.Complete3Research), Task(TutorialTaskId.Complete5Research) }
        ),

        new TutorialStep(
            "tutorial_step_military_title",
            "tutorial_step_military_desc",
            primaryTasks: new[] { Task(TutorialTaskId.BuildFirstBarracks), Task(TutorialTaskId.DefeatFirstBandit) },
            secondaryTasks: new[] { Task(TutorialTaskId.DestroyFirstHideout), Task(TutorialTaskId.BuildThirdCity), Task(TutorialTaskId.BuildFirst10Roads) }
        ),

        new TutorialStep(
            "tutorial_step_prestige_title",
            "tutorial_step_prestige_desc",
            primaryTasks: new[] { Task(TutorialTaskId.BuyFirstPrestigeVertex), Task(TutorialTaskId.PerformFirstPrestige) },
            secondaryTasks: new[] { Task(TutorialTaskId.Buy3PrestigeVertices), Task(TutorialTaskId.PerformThirdPrestige) }
        ),
    };
}
