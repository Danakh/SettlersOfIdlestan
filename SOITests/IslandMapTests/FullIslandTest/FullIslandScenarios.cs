using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SOITests.TestUtilities;

namespace SOITests.IslandMapTests.FullIslandTest
{
    /// <summary>
    /// Shared scenario/step definitions for the FullIslandTest suite, consumed by
    /// FullIslandSaveGeneratorTests, FullIslandCurrentTests and FullIslandReleaseTests.
    /// </summary>
    internal static class FullIslandScenarios
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

        private static IslandStepDefinition PrestigePointsStep(string saveName, int requiredPoints = PrestigeController.PrestigeRequiredPoints, bool shouldExpand = false, int maxIterations = 10000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: shouldExpand, maxIterations: maxIterations),
            Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= requiredPoints,
            AssertFailMessage = ctrl =>
                $"Expected at least {requiredPoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
        };

        private static IslandStepDefinition PrestigeAvailableStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: false),
            Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
            AssertFailMessage = ctrl =>
                $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
        };

        /// <summary>
        /// Keeps building Barracks/Palisade and growing the civilization until every surface monster
        /// (Rats, Bandit, BanditHideout, ...) has been destroyed. Requires the Barracks prestige vertex
        /// to already be purchased (see Island2's Step0 priority-vertex purchase) — otherwise no soldiers
        /// are ever produced and the condition can never be met.
        /// </summary>
        private static IslandStepDefinition ExterminateMonstersStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepExterminateMonstersUntil(cond),
            Condition = ctrl => !ctrl.PrestigeController.HasSurfaceMonsters(),
            AssertFailMessage = _ => "Expected all surface monsters to have been exterminated",
        };

        /// <summary>
        /// Keeps building Barracks/Palisade and attacking the nearest enemy city until every NPC
        /// civilization has lost all of its cities. Requires the Barracks prestige vertex to already
        /// be purchased.
        /// </summary>
        private static IslandStepDefinition ExterminateCivilizationsStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepExterminateCivilizationsUntil(cond),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.Where(c => c.IsNpc).All(c => c.Cities.Count == 0),
            AssertFailMessage = ctrl =>
            {
                var remaining = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.Where(c => c.IsNpc).Sum(c => c.Cities.Count);
                return $"Expected all NPC civilizations to have been exterminated, {remaining} NPC cities remain";
            },
        };

        private static IslandStepDefinition WonderPlacedStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepWonderSetupUntil(cond),
            Condition = ctrl =>
            {
                var worldState = ctrl.CurrentMainState?.CurrentWorldState;
                var wonder = worldState?.Features.OfType<Wonder>().FirstOrDefault();
                return wonder != null && wonder.InvestmentEnabled.Count > 0;
            },
            AssertFailMessage = _ => "Expected wonder to be placed with investment enabled",
        };

        private static IslandStepDefinition WonderLevel1Step(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepWaitUntil(cond),
            Condition = ctrl =>
            {
                var worldState = ctrl.CurrentMainState?.CurrentWorldState;
                var wonder = worldState?.Features.OfType<Wonder>().FirstOrDefault();
                return wonder != null && wonder.Level >= 1;
            },
            AssertFailMessage = _ => "Expected wonder to be at level 1",
        };

        // ── Island 1 scenario ────────────────────────────────────────────────

        private const int FixedTestSeed = 42;

        /// <summary>Island 1 is pushed to 35 prestige points before prestiging — exactly enough
        /// (Central=10 + Barracks=25) to deterministically unlock the Barracks for Island 2.</summary>
        private const int Island1RequiredPrestigePoints = 35;

        internal static readonly IslandScenario Island1 = new()
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
                PrestigePointsStep("Island1_Step3", Island1RequiredPrestigePoints, shouldExpand: true, maxIterations: 20000),
                PrestigeAvailableStep("Island1_Step3bis"),
            },
        };

        // ── Island 2 scenario ────────────────────────────────────────────────

        private const int Island2RequiredPrestigePoints = 70;

        internal static readonly IslandScenario Island2 = new()
        {
            Name = "Island2",
            // Start from Island 1's final save in the same folder (current or release-X.Y).
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island1_Step3bis"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island1_Step3bis"),
            Steps = new List<IslandStepDefinition>
            {
                // Step 0: prestige transition. Barracks is purchased first (deterministically, using
                // Island 1's 35 banked points) so monsters can be exterminated below; the remaining
                // balance, if any, is then spent greedily as usual.
                new()
                {
                    SaveName = "Island2_Step0",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond, new[] { PrestigeMap.CentralVertex, PrestigeMap.BarracksVertex }),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 1,
                    AssertFailMessage = _ => "Expected prestige to have been performed (RunHistory is empty)",
                    IsPrestigeStep = true,
                },
                TwoCitiesStep("Island2_Step1"),
                SixCitiesStep("Island2_Step2"),
                TenCitiesStep("Island2_Step2bis"),
                ExterminateMonstersStep("Island2_Step2ter"),
                PrestigePointsStep("Island2_Step3", Island2RequiredPrestigePoints),
                PrestigeAvailableStep("Island2_Step3bis"),
            },
        };

        // ── Island 3 scenario ────────────────────────────────────────────────

        internal static readonly IslandScenario Island3 = new()
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
                    IsPrestigeStep = true,
                },
                TwoCitiesStep("Island3_Step1"),
                SixCitiesStep("Island3_Step2"),
                TenCitiesStep("Island3_Step2bis"),
                ExterminateCivilizationsStep("Island3_Step2ter"),
                PrestigePointsStep("Island3_Step3"),
                PrestigeAvailableStep("Island3_Step3bis"),
                WonderPlacedStep("Island3_Wonder_Step0"),
                WonderLevel1Step("Island3_Wonder_Step1"),
            },
        };

        // ── Island 4 scenario ────────────────────────────────────────────────

        internal static readonly IslandScenario Island4 = new()
        {
            Name = "Island4",
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island3_Wonder_Step1"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island3_Wonder_Step1"),
            Steps = new List<IslandStepDefinition>
            {
                // Step 0: third prestige transition + greedy point distribution.
                new()
                {
                    SaveName = "Island4_Step0",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 3,
                    AssertFailMessage = _ => "Expected third prestige to have been performed (RunHistory.Count < 3)",
                    IsPrestigeStep = true,
                },
                TwoCitiesStep("Island4_Step1"),
                SixCitiesStep("Island4_Step2"),
                TenCitiesStep("Island4_Step2bis"),
                PrestigePointsStep("Island4_Step3"),
            },
        };
    }
}
