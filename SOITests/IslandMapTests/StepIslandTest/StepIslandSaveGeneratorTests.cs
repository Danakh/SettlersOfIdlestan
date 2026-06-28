using System.Linq;
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
            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (StepIslandScenarios.Island1, 0), (StepIslandScenarios.Island1, 1), (StepIslandScenarios.Island1, 2), (StepIslandScenarios.Island1, 3), (StepIslandScenarios.Island1, 4),
                (StepIslandScenarios.Island2, 0), (StepIslandScenarios.Island2, 1), (StepIslandScenarios.Island2, 2), (StepIslandScenarios.Island2, 3), (StepIslandScenarios.Island2, 4), (StepIslandScenarios.Island2, 5), (StepIslandScenarios.Island2, 6), (StepIslandScenarios.Island2, 7),
                (StepIslandScenarios.Island3, 0), (StepIslandScenarios.Island3, 1), (StepIslandScenarios.Island3, 2), (StepIslandScenarios.Island3, 3), (StepIslandScenarios.Island3, 4), (StepIslandScenarios.Island3, 5), (StepIslandScenarios.Island3, 6), (StepIslandScenarios.Island3, 7), (StepIslandScenarios.Island3, 8), (StepIslandScenarios.Island3, 9),
                (StepIslandScenarios.Island4, 0), (StepIslandScenarios.Island4, 1), (StepIslandScenarios.Island4, 2), (StepIslandScenarios.Island4, 3), (StepIslandScenarios.Island4, 4), (StepIslandScenarios.Island4, 5), (StepIslandScenarios.Island4, 6), (StepIslandScenarios.Island4, 7), (StepIslandScenarios.Island4, 8), (StepIslandScenarios.Island4, 9),
                (StepIslandScenarios.Island5, 0),
            ];
            foreach (var (scenario, stepIndex) in steps)
                IslandScenarioRunner.RunStep(scenario, stepIndex, "current", saveFinal: true);

            // Nettoie les fichiers obsolètes (ex: ancien format JSON non-chiffré) seulement après
            // la reconstruction : chaque save régénérée est remplacée de façon atomique pendant la
            // boucle ci-dessus, donc elle reste lisible par StepIslandCurrentTests/FullIslandTest
            // tout le temps du rebuild — seul ce nettoyage final retire les fichiers qui ne
            // correspondent plus à aucun step.
            var expectedNames = steps.Select(s => s.scenario.Steps[s.stepIndex].SaveName);
            SaveUtils.PruneFolder("current", expectedNames);
        }

        [Fact]
        public void Rebuild_Release_Summary()
        {
            // Mirrors the step indices exercised by StepIslandReleaseTests — release saves are
            // immutable frozen fixtures checked into saves/release-1.0/, so any step here whose
            // predecessor save isn't itself one of those checked-in fixtures now fails outright
            // (IslandScenarioRunner.RunStep no longer skips silently). No NoMonsters (extermination)
            // entry — the frozen release-1.0 lineage predates that requirement (see
            // StepIslandReleaseTests for details); Points70 onward beyond it need their expected
            // "previous step" fixture checked in or they fail.
            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (StepIslandScenarios.Island1, 1), (StepIslandScenarios.Island1, 2), (StepIslandScenarios.Island1, 3), (StepIslandScenarios.Island1, 4),
                (StepIslandScenarios.Island2, 0), (StepIslandScenarios.Island2, 1), (StepIslandScenarios.Island2, 2), (StepIslandScenarios.Island2, 3), (StepIslandScenarios.Island2, 4), (StepIslandScenarios.Island2, 6), (StepIslandScenarios.Island2, 7),
                (StepIslandScenarios.Island3, 0), (StepIslandScenarios.Island3, 1), (StepIslandScenarios.Island3, 2), (StepIslandScenarios.Island3, 3), (StepIslandScenarios.Island3, 5), (StepIslandScenarios.Island3, 6), (StepIslandScenarios.Island3, 7), (StepIslandScenarios.Island3, 8),
                (StepIslandScenarios.Island4, 0), (StepIslandScenarios.Island4, 1), (StepIslandScenarios.Island4, 2), (StepIslandScenarios.Island4, 3), (StepIslandScenarios.Island4, 6),
            ];
            foreach (var (scenario, stepIndex) in steps)
                IslandScenarioRunner.RunStep(scenario, stepIndex, "release-1.0", saveFinal: false);
        }
    }
}
