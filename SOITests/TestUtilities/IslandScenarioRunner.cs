using System;
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
    public static void RunStep(IslandScenario scenario, int stepIndex, string loadFolder, bool saveFinal)
    {
        if (stepIndex < 0 || stepIndex >= scenario.Steps.Count)
            throw new ArgumentOutOfRangeException(nameof(stepIndex));

        var step = scenario.Steps[stepIndex];

        MainGameController controller;
        if (stepIndex == 0)
        {
            if (scenario.IsInputAvailable != null && !scenario.IsInputAvailable(loadFolder))
            {
                Console.WriteLine($"[SKIP] Input not available in '{loadFolder}' for scenario '{scenario.Name}' step 0.");
                return;
            }
            controller = scenario.CreateFreshController(loadFolder);
        }
        else
        {
            var prevSaveName = scenario.Steps[stepIndex - 1].SaveName;
            if (!SaveUtils.SaveExists(loadFolder, prevSaveName))
            {
                // Release saves not yet created — silently skip rather than fail.
                Console.WriteLine($"[SKIP] saves/{loadFolder}/{prevSaveName}.json not found — skipping release regression step.");
                return;
            }
            controller = SaveUtils.LoadSave(loadFolder, prevSaveName);
        }

        var worldState = controller.CurrentMainState!.CurrentWorldState!;
        var civ = worldState.Civilizations.First();
        var autoplayer = new CivilizationAutoplayer(
            civ,
            worldState.GetMapForZ(IslandMap.SurfaceLayer),
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
            controller.PerformPrestige);
        var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

        step.RunAction(runner, () => step.Condition(controller));

        Assert.True(step.Condition(controller), step.AssertFailMessage(controller));

        if (saveFinal)
            SaveUtils.SaveAndReloadAndAssertEqual(controller, loadFolder, step.SaveName);
    }
}
