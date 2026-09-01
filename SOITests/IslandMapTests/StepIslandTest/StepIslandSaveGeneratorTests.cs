using System.Linq;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.StepIslandTest
{
    /// <summary>
    /// Rebuilds every "current" save used by StepIslandCurrentTests, in guaranteed order. A deliberate
    /// action, not something "run all tests" should trigger as a side effect — see ManualFactAttribute.
    /// Run it explicitly (SOI_MANUAL_TESTS=1 dotnet test --filter "FullyQualifiedName~Rebuild_All_Current_Saves")
    /// after any change that could affect these saves, and whenever GameVersion.Current changes:
    /// StepIslandCurrentTests fails if a loaded save's SavedGameVersion doesn't match the current version.
    /// </summary>
    [Collection(StepIslandTestCollection.Name)]
    public class StepIslandSaveGeneratorTests
    {
        [ManualFact]
        public void Rebuild_All_Current_Saves()
        {
            RunSummaryReporter.Reset("current");

            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (StepIslandScenarios.Island1, 0), (StepIslandScenarios.Island1, 1), (StepIslandScenarios.Island1, 2), (StepIslandScenarios.Island1, 3),
                (StepIslandScenarios.Island2, 0), (StepIslandScenarios.Island2, 1), (StepIslandScenarios.Island2, 2), (StepIslandScenarios.Island2, 3), (StepIslandScenarios.Island2, 4), (StepIslandScenarios.Island2, 5), (StepIslandScenarios.Island2, 6),
                (StepIslandScenarios.Island3, 0), (StepIslandScenarios.Island3, 1), (StepIslandScenarios.Island3, 2), (StepIslandScenarios.Island3, 3), (StepIslandScenarios.Island3, 4), (StepIslandScenarios.Island3, 5), (StepIslandScenarios.Island3, 6), (StepIslandScenarios.Island3, 7), (StepIslandScenarios.Island3, 8), (StepIslandScenarios.Island3, 9), (StepIslandScenarios.Island3, 10),
                (StepIslandScenarios.Island4, 0), (StepIslandScenarios.Island4, 1), (StepIslandScenarios.Island4, 2), (StepIslandScenarios.Island4, 3), (StepIslandScenarios.Island4, 4), (StepIslandScenarios.Island4, 5), (StepIslandScenarios.Island4, 6), (StepIslandScenarios.Island4, 7), (StepIslandScenarios.Island4, 8),
                (StepIslandScenarios.Island5, 0),
            ];
            foreach (var (scenario, stepIndex) in steps)
                RunStepAndReport(scenario, stepIndex, "current", saveFinal: true);

            // Nettoie les fichiers obsolètes (ex: ancien format JSON non-chiffré) seulement après
            // la reconstruction : chaque save régénérée est remplacée de façon atomique pendant la
            // boucle ci-dessus, donc elle reste lisible par StepIslandCurrentTests tout le temps du
            // rebuild — seul ce nettoyage final retire les fichiers qui ne correspondent plus à
            // aucun step.
            var expectedNames = steps.Select(s => s.scenario.Steps[s.stepIndex].SaveName);
            SaveUtils.PruneFolder("current", expectedNames);
        }

        [Fact]
        public void Rebuild_Release_Summary()
        {
            RunSummaryReporter.Reset("release-1.0");

            // Mirrors the step indices exercised by StepIslandReleaseTests — release saves are
            // immutable frozen fixtures checked into saves/release-1.0/, so any step here whose
            // predecessor save isn't itself one of those checked-in fixtures now fails outright
            // (IslandScenarioRunner.RunStep no longer skips silently). Points70 onward need their
            // expected "previous step" fixture checked in or they fail.
            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (StepIslandScenarios.Island1, 1), (StepIslandScenarios.Island1, 2), (StepIslandScenarios.Island1, 3),
                (StepIslandScenarios.Island2, 0), (StepIslandScenarios.Island2, 1), (StepIslandScenarios.Island2, 2), (StepIslandScenarios.Island2, 3), (StepIslandScenarios.Island2, 4), (StepIslandScenarios.Island2, 5), (StepIslandScenarios.Island2, 6),
                (StepIslandScenarios.Island3, 0), (StepIslandScenarios.Island3, 1), (StepIslandScenarios.Island3, 2), (StepIslandScenarios.Island3, 3), (StepIslandScenarios.Island3, 6), (StepIslandScenarios.Island3, 7), (StepIslandScenarios.Island3, 8), (StepIslandScenarios.Island3, 9),
                (StepIslandScenarios.Island4, 0), (StepIslandScenarios.Island4, 1), (StepIslandScenarios.Island4, 2), (StepIslandScenarios.Island4, 3), (StepIslandScenarios.Island4, 5),
            ];
            foreach (var (scenario, stepIndex) in steps)
                RunStepAndReport(scenario, stepIndex, "release-1.0", saveFinal: false);
        }

        private static void RunStepAndReport(IslandScenario scenario, int stepIndex, string loadFolder, bool saveFinal)
        {
            var controller = IslandScenarioRunner.RunStep(scenario, stepIndex, loadFolder, saveFinal);
            if (scenario.Steps[stepIndex].IsPrestigeStep)
                RunSummaryReporter.AppendRow(loadFolder, controller);
        }
    }
}
