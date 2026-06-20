using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// Rebuilds every "current" save used by FullIslandCurrentTests, in guaranteed order, and
    /// produces the saves/run_current.csv and saves/run_release.csv progression recaps.
    /// </summary>
    [Collection(FullIslandTestCollection.Name)]
    public class FullIslandSaveGeneratorTests
    {
        [Fact]
        public void Rebuild_All_Current_Saves()
        {
            // Vider le dossier current avant de reconstruire pour garantir
            // qu'aucun fichier obsolète (ex: ancien format JSON non-chiffré) ne subsiste.
            SaveUtils.ClearFolder("current");
            RunSummaryReporter.Reset("current");

            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (FullIslandScenarios.Island1, 0), (FullIslandScenarios.Island1, 1), (FullIslandScenarios.Island1, 2), (FullIslandScenarios.Island1, 3), (FullIslandScenarios.Island1, 4),
                (FullIslandScenarios.Island2, 0), (FullIslandScenarios.Island2, 1), (FullIslandScenarios.Island2, 2), (FullIslandScenarios.Island2, 3), (FullIslandScenarios.Island2, 4), (FullIslandScenarios.Island2, 5), (FullIslandScenarios.Island2, 6),
                (FullIslandScenarios.Island3, 0), (FullIslandScenarios.Island3, 1), (FullIslandScenarios.Island3, 2), (FullIslandScenarios.Island3, 3), (FullIslandScenarios.Island3, 4), (FullIslandScenarios.Island3, 5), (FullIslandScenarios.Island3, 6), (FullIslandScenarios.Island3, 7), (FullIslandScenarios.Island3, 8), (FullIslandScenarios.Island3, 9),
                (FullIslandScenarios.Island4, 0), (FullIslandScenarios.Island4, 1), (FullIslandScenarios.Island4, 2), (FullIslandScenarios.Island4, 3), (FullIslandScenarios.Island4, 4), (FullIslandScenarios.Island4, 5), (FullIslandScenarios.Island4, 6), (FullIslandScenarios.Island4, 7), (FullIslandScenarios.Island4, 8),
                (FullIslandScenarios.Island5, 0),
            ];
            foreach (var (scenario, stepIndex) in steps)
                RunStepAndReport(scenario, stepIndex, "current", saveFinal: true);
        }

        [Fact]
        public void Rebuild_Release_Summary()
        {
            RunSummaryReporter.Reset("release-1.0");

            // Mirrors the step indices exercised by FullIslandReleaseTests — release saves are
            // immutable frozen fixtures, so steps with no save chained from them are silently skipped.
            // No Step2ter (extermination) entries — the frozen release-1.0 lineage predates that
            // requirement (see FullIslandReleaseTests for details); Step3 onward beyond it will
            // silently skip once their expected "previous step" save is missing.
            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (FullIslandScenarios.Island1, 1), (FullIslandScenarios.Island1, 2), (FullIslandScenarios.Island1, 3), (FullIslandScenarios.Island1, 4),
                (FullIslandScenarios.Island2, 0), (FullIslandScenarios.Island2, 1), (FullIslandScenarios.Island2, 2), (FullIslandScenarios.Island2, 3), (FullIslandScenarios.Island2, 5), (FullIslandScenarios.Island2, 6),
                (FullIslandScenarios.Island3, 0), (FullIslandScenarios.Island3, 1), (FullIslandScenarios.Island3, 2), (FullIslandScenarios.Island3, 3), (FullIslandScenarios.Island3, 5), (FullIslandScenarios.Island3, 6), (FullIslandScenarios.Island3, 7), (FullIslandScenarios.Island3, 8),
                (FullIslandScenarios.Island4, 0), (FullIslandScenarios.Island4, 1), (FullIslandScenarios.Island4, 2), (FullIslandScenarios.Island4, 3), (FullIslandScenarios.Island4, 5),
            ];
            foreach (var (scenario, stepIndex) in steps)
                RunStepAndReport(scenario, stepIndex, "release-1.0", saveFinal: false);
        }

        private static void RunStepAndReport(IslandScenario scenario, int stepIndex, string loadFolder, bool saveFinal)
        {
            var controller = IslandScenarioRunner.RunStep(scenario, stepIndex, loadFolder, saveFinal);
            if (controller != null && scenario.Steps[stepIndex].IsPrestigeStep)
                RunSummaryReporter.AppendRow(loadFolder, controller);
        }
    }
}
