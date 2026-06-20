using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// Plays each scenario step against the "release-1.0" saves (silent skip if a save is missing).
    /// </summary>
    [Collection(FullIslandTestCollection.Name)]
    public class FullIslandReleaseTests
    {
        // ── Island 1 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island1_Step2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Step2bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Step3() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 3, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Step3bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 4, "release-1.0", saveFinal: false);

        // ── Island 2 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island2_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step2bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 3, "release-1.0", saveFinal: false);

        // Note: no Release1_0_Island2_Step2ter_ExterminateMonsters fact — the frozen release-1.0
        // lineage predates the Barracks-priority-purchase logic, so Barracks is never unlocked
        // there and the extermination condition could never be met. Step3/Step3bis below silently
        // skip too once their expected "previous step" save is missing under the new numbering.

        [Fact]
        public void Release1_0_Island2_Step3() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 5, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step3bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 6, "release-1.0", saveFinal: false);

        // ── Island 3 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island3_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step2bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 3, "release-1.0", saveFinal: false);

        // Note: no Release1_0_Island3_Step2ter_ExterminateCivilizations fact — same reason as
        // Island 2's extermination step (see above); Step3 onward silently skip once their
        // expected "previous step" save is missing under the new numbering.

        [Fact]
        public void Release1_0_Island3_Step3() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 5, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step3bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 6, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Wonder_Step0() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 7, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Wonder_Step1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 8, "release-1.0", saveFinal: false);

        // ── Island 4 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island4_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island4_Step1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island4_Step2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island4_Step2bis() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 3, "release-1.0", saveFinal: false);

        // Note: no Release1_0_Island4_Step2ter_ExterminateCivilizations or Wonder facts — same reason
        // as Island 2/3's extermination steps; Step3 onward silently skip once their expected
        // "previous step" save is missing under the new numbering (and release-1.0 has no frozen
        // saves past Island2 anyway, so these always skip today).

        [Fact]
        public void Release1_0_Island4_Step3() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 5, "release-1.0", saveFinal: false);
    }
}
