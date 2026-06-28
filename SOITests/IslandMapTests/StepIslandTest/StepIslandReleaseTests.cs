using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.StepIslandTest
{
    /// <summary>
    /// Plays each scenario step against the "release-1.0" saves (fails if the expected previous-step
    /// save is missing — these frozen fixtures must be checked into saves/release-1.0/).
    /// </summary>
    [Collection(StepIslandTestCollection.Name)]
    public class StepIslandReleaseTests
    {
        // ── Island 1 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island1_Cities6() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island1, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Cities10() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island1, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Points35() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island1, 3, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_PrestigeReady() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island1, 4, "release-1.0", saveFinal: false);

        // ── Island 2 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island2_Prestige() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Cities2() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Cities6() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Cities10() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 3, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Library1() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 4, "release-1.0", saveFinal: false);

        // Note: no Release1_0_Island2_NoMonsters fact — the frozen release-1.0 lineage predates the
        // Barracks-priority-purchase logic, so Barracks is never unlocked there and the extermination
        // condition could never be met. Points70/PrestigeReady below need the Island2_NoMonsters.json
        // fixture checked into saves/release-1.0/ (under the new naming) or they fail outright.

        [Fact]
        public void Release1_0_Island2_Points70() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 6, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_PrestigeReady() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island2, 7, "release-1.0", saveFinal: false);

        // ── Island 3 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island3_Prestige() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Cities2() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Cities6() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Cities10() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 3, "release-1.0", saveFinal: false);

        // Note: no Release1_0_Island3_NoEnemies fact — same reason as Island 2's extermination step
        // (see above); Points20 onward need the Island3_NoEnemies.json fixture checked into
        // saves/release-1.0/ (under the new naming) or they fail outright.

        [Fact]
        public void Release1_0_Island3_Points20() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 5, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_PrestigeReady() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 6, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_WonderPlaced() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 7, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Wonder1() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island3, 8, "release-1.0", saveFinal: false);

        // ── Island 4 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island4_Prestige() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island4, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island4_Cities2() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island4, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island4_Cities6() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island4, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island4_Cities10() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island4, 3, "release-1.0", saveFinal: false);

        // Note: no Release1_0_Island4_Barracks1, ExtermineAndRebuild, PrestigeReady, WonderPlaced or
        // Wonder2 facts — same reason as Island 2/3's extermination steps; Points20 below needs the
        // Island4_ExtermineAndRebuild.json fixture checked into saves/release-1.0/ or it fails outright

        [Fact]
        public void Release1_0_Island4_Points20() =>
            IslandScenarioRunner.RunStep(StepIslandScenarios.Island4, 6, "release-1.0", saveFinal: false);
    }
}
