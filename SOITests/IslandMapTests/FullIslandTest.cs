using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.IslandMapTests
{
    public class FullIslandTest
    {
        // ── Shared step conditions ────────────────────────────────────────────

        private static IslandStepDefinition TwoCitiesStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep1Until(cond),
            Condition = ctrl =>
            {
                var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations[0];
                return civ.Cities.Count >= 2
                    && civ.Cities.All(c => c.Buildings.Any(b => b.Type == BuildingType.TownHall));
            },
            AssertFailMessage = ctrl =>
            {
                var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations[0];
                return $"Expected at least 2 cities with TownHall, got {civ.Cities.Count}";
            },
        };

        private static IslandStepDefinition SixCitiesStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep2Until(cond),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 6,
            AssertFailMessage = ctrl =>
                $"Expected at least 6 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
        };

        private static IslandStepDefinition TenCitiesStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep2Until(cond),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 10,
            AssertFailMessage = ctrl =>
                $"Expected at least 10 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
        };

        private static IslandStepDefinition PrestigePointsStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: false),
            Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints,
            AssertFailMessage = ctrl =>
                $"Expected enough prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
        };

        private static IslandStepDefinition PrestigeAvailableStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: false),
            Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
            AssertFailMessage = ctrl =>
                $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
        };

        // ── Island 1 scenario ────────────────────────────────────────────────

        private const int FixedTestSeed = 42;

        private static readonly IslandScenario Island1 = new()
        {
            Name = "Island1",
            CreateFreshController = _ =>
            {
                var controller = new MainGameController();
                AtlasController atlas = new AtlasController();
                controller.CreateNewGame(atlas.GetIslandParameters(atlas.GetFirstWorldId()), FixedTestSeed);
                return controller;
            },
            Steps = new List<IslandStepDefinition>
            {
                TwoCitiesStep("Island1_Step1"),
                SixCitiesStep("Island1_Step2"),
                TenCitiesStep("Island1_Step2bis"),
                PrestigePointsStep("Island1_Step3"),
                PrestigeAvailableStep("Island1_Step3bis"),
            },
        };

        // ── Island 2 scenario ────────────────────────────────────────────────

        private static readonly IslandScenario Island2 = new()
        {
            Name = "Island2",
            // Start from Island 1's final save in the same folder (current or release-X.Y).
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island1_Step3bis"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island1_Step3bis"),
            Steps = new List<IslandStepDefinition>
            {
                // Step 0: prestige transition + greedy point distribution.
                new()
                {
                    SaveName = "Island2_Step0",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 1,
                    AssertFailMessage = _ => "Expected prestige to have been performed (RunHistory is empty)",
                },
                TwoCitiesStep("Island2_Step1"),
                SixCitiesStep("Island2_Step2"),
                TenCitiesStep("Island2_Step2bis"),
                PrestigePointsStep("Island2_Step3"),
                PrestigeAvailableStep("Island2_Step3bis"),
            },
        };

        // ── Island 3 scenario ────────────────────────────────────────────────

        private static readonly IslandScenario Island3 = new()
        {
            Name = "Island3",
            // Start from Island 2's final save in the same folder (current or release-X.Y).
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island2_Step3bis"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island2_Step3bis"),
            Steps = new List<IslandStepDefinition>
            {
                // Step 0: second prestige transition + greedy point distribution.
                new()
                {
                    SaveName = "Island3_Step0",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 2,
                    AssertFailMessage = _ => "Expected second prestige to have been performed (RunHistory.Count < 2)",
                },
                TwoCitiesStep("Island3_Step1"),
                SixCitiesStep("Island3_Step2"),
                TenCitiesStep("Island3_Step2bis"),
                PrestigePointsStep("Island3_Step3"),
                PrestigeAvailableStep("Island3_Step3bis"),
            },
        };

        // ── Rebuild all current saves in guaranteed order ─────────────────────

        [Fact]
        public void Rebuild_All_Current_Saves()
        {
            (IslandScenario scenario, int stepIndex)[] steps =
            [
                (Island1, 0), (Island1, 1), (Island1, 2), (Island1, 3), (Island1, 4),
                (Island2, 0), (Island2, 1), (Island2, 2), (Island2, 3), (Island2, 4), (Island2, 5),
                (Island3, 0), (Island3, 1), (Island3, 2), (Island3, 3), (Island3, 4), (Island3, 5),
            ];
            foreach (var (scenario, stepIndex) in steps)
                IslandScenarioRunner.RunStep(scenario, stepIndex, "current", saveFinal: true);
        }

        // ── Island 1 — current mode (creates/overwrites saves/current) ────────

        [Fact]
        public void Current_Island1_Step1() =>
            IslandScenarioRunner.RunStep(Island1, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Step2() =>
            IslandScenarioRunner.RunStep(Island1, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Step2bis() =>
            IslandScenarioRunner.RunStep(Island1, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Step3() =>
            IslandScenarioRunner.RunStep(Island1, 3, "current", saveFinal: true);

        [Fact]
        public void Current_Island1_Step3bis() =>
            IslandScenarioRunner.RunStep(Island1, 4, "current", saveFinal: true);

        // ── Island 2 — current mode ───────────────────────────────────────────

        [Fact]
        public void Current_Island2_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(Island2, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Step1() =>
            IslandScenarioRunner.RunStep(Island2, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Step2() =>
            IslandScenarioRunner.RunStep(Island2, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Step2bis() =>
            IslandScenarioRunner.RunStep(Island2, 3, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Step3() =>
            IslandScenarioRunner.RunStep(Island2, 4, "current", saveFinal: true);

        [Fact]
        public void Current_Island2_Step3bis() =>
            IslandScenarioRunner.RunStep(Island2, 5, "current", saveFinal: true);

        // ── Island 3 — current mode ───────────────────────────────────────────

        [Fact]
        public void Current_Island3_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(Island3, 0, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Step1() =>
            IslandScenarioRunner.RunStep(Island3, 1, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Step2() =>
            IslandScenarioRunner.RunStep(Island3, 2, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Step2bis() =>
            IslandScenarioRunner.RunStep(Island3, 3, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Step3() =>
            IslandScenarioRunner.RunStep(Island3, 4, "current", saveFinal: true);

        [Fact]
        public void Current_Island3_Step3bis() =>
            IslandScenarioRunner.RunStep(Island3, 5, "current", saveFinal: true);

        // ── Island 1 — from release-1.0 (no save, silent skip if missing) ────

        [Fact]
        public void Release1_0_Island1_Step2() =>
            IslandScenarioRunner.RunStep(Island1, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Step2bis() =>
            IslandScenarioRunner.RunStep(Island1, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Step3() =>
            IslandScenarioRunner.RunStep(Island1, 3, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island1_Step3bis() =>
            IslandScenarioRunner.RunStep(Island1, 4, "release-1.0", saveFinal: false);

        // ── Island 2 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island2_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(Island2, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step1() =>
            IslandScenarioRunner.RunStep(Island2, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step2() =>
            IslandScenarioRunner.RunStep(Island2, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step2bis() =>
            IslandScenarioRunner.RunStep(Island2, 3, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step3() =>
            IslandScenarioRunner.RunStep(Island2, 4, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island2_Step3bis() =>
            IslandScenarioRunner.RunStep(Island2, 5, "release-1.0", saveFinal: false);

        // ── Island 3 — from release-1.0 ──────────────────────────────────────

        [Fact]
        public void Release1_0_Island3_Step0_Prestige() =>
            IslandScenarioRunner.RunStep(Island3, 0, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step1() =>
            IslandScenarioRunner.RunStep(Island3, 1, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step2() =>
            IslandScenarioRunner.RunStep(Island3, 2, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step2bis() =>
            IslandScenarioRunner.RunStep(Island3, 3, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step3() =>
            IslandScenarioRunner.RunStep(Island3, 4, "release-1.0", saveFinal: false);

        [Fact]
        public void Release1_0_Island3_Step3bis() =>
            IslandScenarioRunner.RunStep(Island3, 5, "release-1.0", saveFinal: false);
    }
}
