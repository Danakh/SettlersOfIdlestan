using System;
using SettlersOfIdlestan.Controller;
using SOITests.IslandMapTests.StepIslandTest;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// Runs all four FullIslandTest prestige cycles in order and produces the saves/run_current.csv
    /// progression recap — the only place in the test suite that still generates this CSV
    /// (StepIslandTest no longer does, see StepIslandSaveGeneratorTests).
    /// </summary>
    [Collection(StepIslandTestCollection.Name)]
    public class FullIslandSaveGeneratorTests
    {
        [Fact]
        public void Run_All_Current_Full_Islands()
        {
            RunSummaryReporter.Reset("current");

            Func<string, MainGameController?>[] runs =
            [
                FullIslandScenarios.RunIsland1,
                FullIslandScenarios.RunIsland2,
                FullIslandScenarios.RunIsland3,
                FullIslandScenarios.RunIsland4,
            ];
            foreach (var run in runs)
            {
                var controller = run("current");
                if (controller != null)
                    RunSummaryReporter.AppendRow("current", controller);
            }
        }
    }
}
