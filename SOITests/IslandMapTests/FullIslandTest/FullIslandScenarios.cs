using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
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

        // ── Island 1-specific step definitions ──────────────────────────────────
        // Found via SOIStrategyTester (SOIStrategyTester/Data/Best/island1-step*.best.json) and used
        // only for Island1, whose steps start from a fresh, research-less, un-prestiged game — the
        // priority orderings below are tuned against exactly that starting state and don't generalize
        // to Island2+ (e.g. they skip Library, which TwoCitiesStep/SixCitiesStep above still build once
        // research is unlocked after the first prestige), so Island1 gets its own step definitions
        // instead of reusing the shared ones.

        // Building only the production chain that actually matters (Sawmill/Brickworks/Mill for the
        // road/outpost costs) plus Market/Seaport to unlock trade — needed because the city's terrain
        // doesn't always produce every basic resource automatically, and without trade a missing one
        // stalls forever — reaches 2 cities with TownHall faster than both the full Step1 building list
        // and the high-level Step1 autoplayer.
        private static readonly BuildingType[] Island1Step1PriorityBuildings =
        {
            BuildingType.TownHall, BuildingType.Sawmill, BuildingType.Brickworks,
            BuildingType.Mill, BuildingType.Market, BuildingType.Seaport,
        };

        private static IslandStepDefinition Island1TwoCitiesStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(new[]
            {
                PriorityStage.Buildings(Island1Step1PriorityBuildings, targetLevel: 1),
                PriorityStage.Cities(2),
            }, cond),
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

        // Only TownHall + Sawmill are worth building before expanding — every other Step2 production
        // building (Brickworks, Mill, Market, Seaport, Warehouse, Mine, Forge) costs more in
        // construction time than it saves, and combining several building types in a single
        // BuildingLevel stage causes cross-building trade interference (TryGrindOnce repeatedly
        // chases a different missing resource each call and can churn the stockpile forever — see
        // BuildingLevelObjective's doc comment) — so each stays its own stage.
        private static IslandStepDefinition Island1SixCitiesStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(new[]
            {
                PriorityStage.Buildings(new[] { BuildingType.TownHall }, targetLevel: 1),
                PriorityStage.Buildings(new[] { BuildingType.Sawmill }, targetLevel: 1),
                PriorityStage.Cities(6),
            }, cond),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 6,
            AssertFailMessage = ctrl =>
                $"Expected at least 6 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
        };

        // With 6 cities' worth of economy already in place from Step2, pure expansion (no further
        // production building) reaches 10 cities faster than building Sawmill in each new city first.
        private static IslandStepDefinition Island1TenCitiesStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(new[]
            {
                PriorityStage.Cities(10),
            }, cond),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 10,
            AssertFailMessage = ctrl =>
                $"Expected at least 10 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
        };

        // Temple (1pt) + TownHall level 3 (2pt, the cap — level 4 doesn't add more) tops out at exactly
        // 30 points across 10 cities, and Island1 already has surface monsters at this stage (no 1.2x
        // bonus) so that's short of the 35 required — extra cities close the gap. Building Temple/
        // TownHall in the existing cities FIRST, with city count only as an uncapped (30, well above
        // what's ever needed) topping-up fallback, is ~8% slower than expanding first on this exact
        // seed/save chain, but expanding first is unsafe in general: PriorityAutoplayStrategy never
        // touches a later objective while an earlier one has actionable work, so if CityCount came
        // first and the map can't actually support that many cities (verified against the release-1.0
        // fixture, which plateaus at 13), Temple/TownHall would never even start and points would never
        // grow — confirmed by reproducing that exact deadlock against saves/release-1.0/Island1_Step2bis.
        private static IslandStepDefinition Island1PrestigePointsStep(string saveName, int requiredPoints, int maxIterations) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(new[]
            {
                PriorityStage.Buildings(new[] { BuildingType.Temple }, targetLevel: 1),
                PriorityStage.Buildings(new[] { BuildingType.TownHall }, targetLevel: 3),
                PriorityStage.Cities(30),
            }, cond, maxIterations),
            Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= requiredPoints,
            AssertFailMessage = ctrl =>
                $"Expected at least {requiredPoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
        };

        // ImperialPortObjective wraps CivilizationAutoplayer.TryBuildImperialPortOnce — the unique
        // ImperialPort building is never returned as buildable by BuildingController.GetBuildingOrBuildable,
        // so BuildingLevelObjective can't drive this regardless of which buildings are listed.
        // Temple/TownHall stages come first as a safety net: PrestigeIsAvailable also requires >=20
        // points (not just the Imperial Port), and unlike Island1_Step3 this step's input isn't
        // guaranteed to already clear that bar — confirmed against the release-1.0 fixture, whose frozen
        // Island1_Step3 save only has 11 points under today's CalculatePrestigePoints (game-balance
        // formula changes since that fixture was captured), which an ImperialPort-only strategy can never
        // fix. No CityCount stage here on purpose, unlike Island1PrestigePointsStep: 10+ cities already
        // clears 20 points once Temple/TownHall3 are done, and adding an uncapped expansion stage ahead
        // of ImperialPort risks the same deadlock as Island1PrestigePointsStep if a given map can't
        // support the target city count.
        private static IslandStepDefinition Island1PrestigeAvailableStep(string saveName, int maxIterations = 20000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(new[]
            {
                PriorityStage.Buildings(new[] { BuildingType.Temple }, targetLevel: 1),
                PriorityStage.Buildings(new[] { BuildingType.TownHall }, targetLevel: 3),
                PriorityStage.ImperialPort(),
            }, cond, maxIterations),
            Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
            AssertFailMessage = ctrl =>
                $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
        };

        private static IslandStepDefinition PrestigePointsStep(string saveName, int requiredPoints = PrestigeController.PrestigeRequiredPoints, bool shouldExpand = false, int maxIterations = 10000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: shouldExpand, maxIterations: maxIterations),
            Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= requiredPoints,
            AssertFailMessage = ctrl =>
                $"Expected at least {requiredPoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
        };

        private static IslandStepDefinition PrestigeAvailableStep(string saveName, int maxIterations = 10000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: false, maxIterations),
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
        private static IslandStepDefinition ExterminateCivilizationsStep(string saveName, int maxIterations = 50000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepExterminateCivilizationsUntil(cond, maxIterations),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.Where(c => c.IsNpc).All(c => c.Cities.Count == 0),
            AssertFailMessage = ctrl =>
            {
                var remaining = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.Where(c => c.IsNpc).Sum(c => c.Cities.Count);
                return $"Expected all NPC civilizations to have been exterminated, {remaining} NPC cities remain";
            },
        };

        /// <summary>
        /// Lightweight stand-in for <see cref="ExterminateCivilizationsStep"/>: builds the Barracks to
        /// level 1 in every city without attacking anyone. Used for Island4_Step2ter while the full
        /// extermination loop is disabled there for being too slow (see Rebuild_All_Current_Saves and
        /// FullIslandCurrentTests) — keeps the save chain intact so later steps still have their
        /// expected predecessor save. Swap back to ExterminateCivilizationsStep to re-enable it.
        /// Mirrors BuildingLevelObjective's own leniency: a city for which Barracks is unavailable
        /// (city level too low, prerequisites unmet) counts as satisfied, since the autoplayer can't
        /// do anything more about it — checking city.Buildings directly would never agree with that
        /// and the run would stall forever waiting for an unreachable building.
        /// </summary>
        private static IslandStepDefinition BarracksLevel1Step(string saveName, int maxIterations = 30000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepBuildBarracksUntil(cond, maxIterations),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities
                .All(c => IsBarracksDoneForCity(ctrl, c)),
            AssertFailMessage = ctrl =>
            {
                var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                var missing = civ.Cities.Count(c => !IsBarracksDoneForCity(ctrl, c));
                return $"Expected every city to have Barracks at level >= 1 (or be unable to build it), {missing} cities are missing it";
            },
        };

        private static bool IsBarracksDoneForCity(MainGameController ctrl, City city)
        {
            var building = ctrl.BuildingController.GetBuildingOrBuildable(city, BuildingType.Barracks);
            if (building == null) return true;
            var maxLevel = ctrl.BuildingController.GetMaxLevel(building, city.CivilizationIndex);
            return building.Level >= Math.Min(1, maxLevel);
        }

        private static IslandStepDefinition WonderPlacedStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepWonderUntil(cond),
            Condition = ctrl =>
            {
                var worldState = ctrl.CurrentMainState?.CurrentWorldState;
                var wonder = worldState?.Features.OfType<Wonder>().FirstOrDefault();
                return wonder != null && wonder.InvestmentEnabled.Count > 0;
            },
            AssertFailMessage = _ => "Expected wonder to be placed with investment enabled",
        };

        /// <summary>Waits for the Wonder to reach <paramref name="targetLevel"/>. Reusable for any
        /// level — CivilizationAutoplayer.TryWonderInvestmentOnce re-enables investment for whichever
        /// resources the next level needs each time the previous level-up clears it.</summary>
        private static IslandStepDefinition WonderLevelStep(string saveName, int targetLevel, int maxIterations = 100000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunStepWonderUntil(cond, maxIterations),
            Condition = ctrl =>
            {
                var worldState = ctrl.CurrentMainState?.CurrentWorldState;
                var wonder = worldState?.Features.OfType<Wonder>().FirstOrDefault();
                return wonder != null && wonder.Level >= targetLevel;
            },
            AssertFailMessage = ctrl =>
            {
                var wonder = ctrl.CurrentMainState?.CurrentWorldState?.Features.OfType<Wonder>().FirstOrDefault();
                return $"Expected wonder to be at level {targetLevel} (has {wonder?.Level ?? 0})";
            },
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
                Island1TwoCitiesStep("Island1_Step1"),
                Island1SixCitiesStep("Island1_Step2"),
                Island1TenCitiesStep("Island1_Step2bis"),
                Island1PrestigePointsStep("Island1_Step3", Island1RequiredPrestigePoints, maxIterations: 20000),
                Island1PrestigeAvailableStep("Island1_Step3bis"),
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
                WonderLevelStep("Island3_Wonder_Step1", targetLevel: 1),
                // Pushed well past the default threshold, after the wonder's multiplier is already
                // active: Island 4 (archipelago) needs the Watchtower (100) + MaritimeRoutes (400)
                // chain bought deterministically at its own transition (see Island4's Step0), which
                // requires a healthy banked surplus.
                PrestigePointsStep("Island3_Step4", requiredPoints: 700, shouldExpand: true, maxIterations: 30000),
            },
        };

        // ── Island 4 scenario ────────────────────────────────────────────────

        internal static readonly IslandScenario Island4 = new()
        {
            Name = "Island4",
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island3_Step4"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island3_Step4"),
            Steps = new List<IslandStepDefinition>
            {
                // Step 0: third prestige transition. Island 4 is an archipelago — Watchtower then
                // MaritimeRoutes are purchased first (deterministically) so the player can build roads
                // across water and reach the NPC civilizations on other landmasses; the remaining
                // balance is then spent greedily as usual.
                new()
                {
                    SaveName = "Island4_Step0",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond, new[] { PrestigeMap.WatchtowerVertex, PrestigeMap.MaritimeRoutesVertex }),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 3,
                    AssertFailMessage = _ => "Expected third prestige to have been performed (RunHistory.Count < 3)",
                    IsPrestigeStep = true,
                },
                TwoCitiesStep("Island4_Step1"),
                SixCitiesStep("Island4_Step2"),
                TenCitiesStep("Island4_Step2bis"),
                BarracksLevel1Step("Island4_Step2ter"),
                // Both budgets are pushed well past the usual default: with the extermination loop
                // disabled above, the economy entering this step is much weaker (no incidental growth
                // from a long-running combat/expansion loop), so reaching the Imperial Port's city-level-4
                // requirement takes a lot more grinding than on Island 2/3.
                PrestigePointsStep("Island4_Step3", maxIterations: 100000),
                PrestigeAvailableStep("Island4_Step3bis", maxIterations: 300000),
                WonderPlacedStep("Island4_Wonder_Step0"),
                // One level further than Island 3's target (level 1) — the Wonder is a fresh,
                // per-island feature, so Island 4 re-places it and pushes one level beyond.
                // Weaker economy than usual entering this step (BarracksLevel1Step above skips the
                // long incidental growth the extermination loop used to provide), so the wonder takes
                // longer to fund — same reasoning as the budgets bumped on Island4_Step3/Step3bis.
                WonderLevelStep("Island4_Wonder_Step1", targetLevel: 2, maxIterations: 300000),
            },
        };

        // ── Island 5 scenario ────────────────────────────────────────────────
        // Only goes up to the start of the run — the fourth prestige transition out of Island 4.

        internal static readonly IslandScenario Island5 = new()
        {
            Name = "Island5",
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island4_Wonder_Step1"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island4_Wonder_Step1"),
            Steps = new List<IslandStepDefinition>
            {
                // Step 0: fourth prestige transition + greedy point distribution.
                new()
                {
                    SaveName = "Island5_Step0",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 4,
                    AssertFailMessage = _ => "Expected fourth prestige to have been performed (RunHistory.Count < 4)",
                    IsPrestigeStep = true,
                },
            },
        };
    }
}
