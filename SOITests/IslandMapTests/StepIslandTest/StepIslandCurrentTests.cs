using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// Plays each scenario step against the "current" saves (creates/overwrites saves/current).
    /// </summary>
    [Collection(FullIslandTestCollection.Name)]
    public class FullIslandCurrentTests
    {
        // ── Island 1 — current mode ───────────────────────────────────────────

        [Fact]
        public void Current_Island1_Cities2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Cities6() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Cities10() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Points35() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 3, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_PrestigeReady() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island1, 4, "current", saveFinal: true);

        // ── Island 2 — current mode ───────────────────────────────────────────

        [Fact]
        public void Current_Island2_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Cities2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Cities6() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Cities10() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 3, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Library1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 4, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_NoMonsters() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 5, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Points70() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 6, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_PrestigeReady() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island2, 7, "current", saveFinal: true);

        // ── Island 3 — current mode ───────────────────────────────────────────

        [Fact]
        public void Current_Island3_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Cities2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Cities6() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Cities10() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 3, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_NoEnemies() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 4, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Points20() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 5, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_PrestigeReady() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 6, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_WonderPlaced() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 7, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Wonder1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 8, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Points700() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island3, 9, "current", saveFinal: true);

        // ── Island 4 — current mode ───────────────────────────────────────────

        [Fact]
        public void Current_Island4_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_Cities2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_Cities6() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_Cities10() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 3, "current", saveFinal: true);

        // Stands in for the full extermination step (too slow to run regularly) — just builds the
        // Barracks to level 1 everywhere. Swap FullIslandScenarios.Island4's Steps[4] back to
        // ExterminateCivilizationsStep to re-enable the real thing.
        [Fact]
        public void Current_Island4_Barracks1() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 4, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_Points20() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 5, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_PrestigeReady() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 6, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_WonderPlaced() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 7, "current", saveFinal: true);

        [Fact]
        public void Current_Island4_Wonder2() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island4, 8, "current", saveFinal: true);

        // ── Island 5 — current mode (start of the run only) ───────────────────

        [Fact]
        public void Current_Island5_Prestige() =>
            IslandScenarioRunner.RunStep(FullIslandScenarios.Island5, 0, "current", saveFinal: true);
    }
}
