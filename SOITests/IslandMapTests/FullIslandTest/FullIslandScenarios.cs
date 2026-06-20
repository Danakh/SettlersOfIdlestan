using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SOITests.IslandMapTests.StepIslandTest;
using SOITests.TestUtilities;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// One continuous, in-memory run per prestige cycle — no per-checkpoint save/reload/assert in
    /// between, unlike StepIslandTest's StepIslandScenarios (whose narrowly-scoped per-stage Facts
    /// (Cities2, Cities6, NoMonsters, ...) invite tuning each stage in isolation rather than the actual
    /// long-horizon goal: reaching the next prestige).
    ///
    /// Reuses StepIslandScenarios' own step definitions (already proven to converge — see
    /// SOIStrategyTester/CLAUDE.md for how they were tuned) chained together on a single controller:
    /// for IslandN this is StepIslandScenarios.IslandN's own steps (minus its own entering-prestige
    /// step, already baked into the loaded save) followed by Island(N+1)'s entering-prestige step,
    /// which performs the actual transition this run stops at.
    ///
    /// Island2/3/4 start from saves/current/IslandN_Prestige.json, produced by
    /// StepIslandSaveGeneratorTests — run that first if these silently skip. Island1 has no such
    /// predecessor: it starts fresh, from the same fixed seed as StepIslandScenarios.
    /// </summary>
    internal static class FullIslandScenarios
    {
        internal static MainGameController? RunIsland1(string loadFolder) =>
            IslandScenarioRunner.RunChained(
                StepIslandScenarios.Island1.CreateFreshController(loadFolder),
                StepIslandScenarios.Island1.Steps.Concat(new[] { StepIslandScenarios.Island2.Steps[0] }));

        internal static MainGameController? RunIsland2(string loadFolder) =>
            RunFromSave(loadFolder, "Island2_Prestige",
                StepIslandScenarios.Island2.Steps.Skip(1).Concat(new[] { StepIslandScenarios.Island3.Steps[0] }));

        internal static MainGameController? RunIsland3(string loadFolder) =>
            RunFromSave(loadFolder, "Island3_Prestige",
                StepIslandScenarios.Island3.Steps.Skip(1).Concat(new[] { StepIslandScenarios.Island4.Steps[0] }));

        internal static MainGameController? RunIsland4(string loadFolder) =>
            RunFromSave(loadFolder, "Island4_Prestige",
                StepIslandScenarios.Island4.Steps.Skip(1).Concat(new[] { StepIslandScenarios.Island5.Steps[0] }));

        private static MainGameController? RunFromSave(string loadFolder, string saveName, IEnumerable<IslandStepDefinition> steps)
        {
            if (!SaveUtils.SaveExists(loadFolder, saveName))
            {
                Console.WriteLine($"[SKIP] saves/{loadFolder}/{saveName}.json not found — run StepIslandSaveGeneratorTests first.");
                return null;
            }
            return IslandScenarioRunner.RunChained(SaveUtils.LoadSave(loadFolder, saveName), steps);
        }
    }
}
