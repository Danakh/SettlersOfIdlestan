using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.StepIslandTest
{
    /// <summary>
    /// Rebuilds every "current" save used by StepIslandCurrentTests, in guaranteed order.
    /// </summary>
    [Collection(StepIslandTestCollection.Name)]
    public class StepIslandSaveGeneratorTests
    {
        [Fact]
        public void Rebuild_All_Current_Saves()
        {
            // Vider le dossier current avant de reconstruire pour garantir
            // qu'aucun fichier obsolète (ex: ancien format JSON non-chiffré) ne subsiste.
            SaveUtils.ClearFolder("current");

            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (StepIslandScenarios.Island1, 0), (StepIslandScenarios.Island1, 1), (StepIslandScenarios.Island1, 2), (StepIslandScenarios.Island1, 3), (StepIslandScenarios.Island1, 4),
                (StepIslandScenarios.Island2, 0), (StepIslandScenarios.Island2, 1), (StepIslandScenarios.Island2, 2), (StepIslandScenarios.Island2, 3), (StepIslandScenarios.Island2, 4), (StepIslandScenarios.Island2, 5), (StepIslandScenarios.Island2, 6), (StepIslandScenarios.Island2, 7),
                (StepIslandScenarios.Island3, 0), (StepIslandScenarios.Island3, 1), (StepIslandScenarios.Island3, 2), (StepIslandScenarios.Island3, 3), (StepIslandScenarios.Island3, 4), (StepIslandScenarios.Island3, 5), (StepIslandScenarios.Island3, 6), (StepIslandScenarios.Island3, 7), (StepIslandScenarios.Island3, 8), (StepIslandScenarios.Island3, 9),
                (StepIslandScenarios.Island4, 0), (StepIslandScenarios.Island4, 1), (StepIslandScenarios.Island4, 2), (StepIslandScenarios.Island4, 3), (StepIslandScenarios.Island4, 4), (StepIslandScenarios.Island4, 5), (StepIslandScenarios.Island4, 6), (StepIslandScenarios.Island4, 7), (StepIslandScenarios.Island4, 8),
                (StepIslandScenarios.Island5, 0),
            ];
            foreach (var (scenario, stepIndex) in steps)
                IslandScenarioRunner.RunStep(scenario, stepIndex, "current", saveFinal: true);
        }

        [Fact]
        public void Rebuild_Release_Summary()
        {
            // Mirrors the step indices exercised by StepIslandReleaseTests — release saves are
            // immutable frozen fixtures, so steps with no save chained from them are silently skipped.
            // No NoMonsters (extermination) entry — the frozen release-1.0 lineage predates that
            // requirement (see StepIslandReleaseTests for details); Points70 onward beyond it will
            // silently skip once their expected "previous step" save is missing.
            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (StepIslandScenarios.Island1, 1), (StepIslandScenarios.Island1, 2), (StepIslandScenarios.Island1, 3), (StepIslandScenarios.Island1, 4),
                (StepIslandScenarios.Island2, 0), (StepIslandScenarios.Island2, 1), (StepIslandScenarios.Island2, 2), (StepIslandScenarios.Island2, 3), (StepIslandScenarios.Island2, 4), (StepIslandScenarios.Island2, 6), (StepIslandScenarios.Island2, 7),
                (StepIslandScenarios.Island3, 0), (StepIslandScenarios.Island3, 1), (StepIslandScenarios.Island3, 2), (StepIslandScenarios.Island3, 3), (StepIslandScenarios.Island3, 5), (StepIslandScenarios.Island3, 6), (StepIslandScenarios.Island3, 7), (StepIslandScenarios.Island3, 8),
                (StepIslandScenarios.Island4, 0), (StepIslandScenarios.Island4, 1), (StepIslandScenarios.Island4, 2), (StepIslandScenarios.Island4, 3), (StepIslandScenarios.Island4, 5),
            ];
            foreach (var (scenario, stepIndex) in steps)
                IslandScenarioRunner.RunStep(scenario, stepIndex, "release-1.0", saveFinal: false);
        }
    }
}
