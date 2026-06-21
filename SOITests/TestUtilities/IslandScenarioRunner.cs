using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.TestUtilities;

public static class IslandScenarioRunner
{
    /// <summary>
    /// Runs a single step of an island scenario.
    /// </summary>
    /// <param name="scenario">Scenario definition.</param>
    /// <param name="stepIndex">0-based index into scenario.Steps.</param>
    /// <param name="loadFolder">Subfolder under saves/ from which to load the previous step's save (e.g. "current", "release-1.0").</param>
    /// <param name="saveFinal">When true, saves the result to saves/{loadFolder}/{step.SaveName}.json after asserting.</param>
    /// <returns>The controller used to run the step.</returns>
    public static MainGameController RunStep(IslandScenario scenario, int stepIndex, string loadFolder, bool saveFinal)
    {
        if (stepIndex < 0 || stepIndex >= scenario.Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(stepIndex));

        var step = scenario.Steps[stepIndex];

        MainGameController controller;
        if (stepIndex == 0)
        {
            if (scenario.IsInputAvailable != null && !scenario.IsInputAvailable(loadFolder))
                Assert.Fail($"Input not available in '{loadFolder}' for scenario '{scenario.Name}' step 0.");
            controller = scenario.CreateFreshController(loadFolder);
        }
        else
        {
            var prevSaveName = scenario.Steps[stepIndex - 1].SaveName;
            if (!SaveUtils.SaveExists(loadFolder, prevSaveName))
                Assert.Fail($"saves/{loadFolder}/{prevSaveName}.json not found.");
            controller = SaveUtils.LoadSave(loadFolder, prevSaveName);
        }

        RunSingleStep(controller, step);

        if (saveFinal)
            SaveUtils.SaveAndReloadAndAssertEqual(controller, loadFolder, step.SaveName);

        return controller;
    }

    /// <summary>
    /// Runs every given step on the SAME controller, in memory, with no save/reload between them —
    /// used by FullIslandTest to collapse StepIslandTest's per-checkpoint Facts (each its own save +
    /// assert) into a single continuous run from one prestige to the next. A fresh
    /// CivilizationAutoplayer is rebuilt before each step regardless, since a Prestige step invalidates
    /// the previous one's civ/map references (a new island gets generated).
    /// </summary>
    public static MainGameController RunChained(MainGameController controller, IEnumerable<IslandStepDefinition> steps)
    {
        foreach (var step in steps)
            RunSingleStep(controller, step);
        return controller;
    }

    private static void RunSingleStep(MainGameController controller, IslandStepDefinition step)
    {
        var worldState = controller.CurrentMainState!.CurrentWorldState!;
        var civ = worldState.Civilizations.First();
        var autoplayer = new CivilizationAutoplayer(
            civ,
            worldState.GetMapForZ(IslandMap.SurfaceLayer)!,
            controller.RoadController,
            controller.HarvestController,
            controller.BuildingController,
            controller.CityBuilderController,
            controller.TradeController,
            controller.ResearchController,
            controller.PrestigeController,
            controller.PrestigeMapController,
            worldState,
            controller.CurrentMainState!.PrestigeState,
            controller.PerformPrestige,
            controller.WonderController);
        var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

        step.RunAction(runner, () => step.Condition(controller));

        Assert.True(step.Condition(controller), step.AssertFailMessage(controller));
    }
}
