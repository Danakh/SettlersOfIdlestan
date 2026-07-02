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

namespace SOITests.IslandMapTests.StepIslandTest
{
    /// <summary>
    /// Shared scenario/step definitions for the StepIslandTest suite, consumed by
    /// StepIslandSaveGeneratorTests, StepIslandCurrentTests and StepIslandReleaseTests.
    /// </summary>
    internal static class StepIslandScenarios
    {
        // ── Shared step conditions ────────────────────────────────────────────

        /// <summary>
        /// Keeps building Barracks/production buildings and growing the civilization until every surface
        /// monster (Rats, Bandit, BanditHideout, ...) has been destroyed. Requires the Barracks prestige
        /// vertex to already be purchased (see Island2's Prestige step priority-vertex purchase) — otherwise no
        /// soldiers are ever produced and the condition can never be met. Melee combat resolves
        /// automatically as soon as a city's footprint (the hexes touching its vertex) overlaps a
        /// monster's hex and that city has soldiers — there's no separate "attack" action, which is why
        /// CityCount (not a fixed list of targets) is what actually finds the monsters here: it keeps
        /// expanding the civilization's footprint until it reaches them.
        ///
        /// Found via SOIStrategyTester (SOIStrategyTester/Data/Best/island2-step2ter.best.json) — beats
        /// the high-level TryMilitaryStepOnce+TryStep2Once loop this used to call (~285k ticks) by ~13%
        /// (~249k ticks). TownHall is pushed to level 3 (not just 1) because Mine — the only source of
        /// Ore, which soldier production consumes — is unavailable below city level 3 (Mine.
        /// AvailableAtLevel = 3); without it, Barracks would never produce soldiers and the run would
        /// hang. Brickworks and Forge were raced and dropped: cheaper to let TryGrindOnce's manual
        /// harvest cover Brick on demand than to spend time building Brickworks, and Forge has no
        /// bearing on combat at all. CityCount is capped at 30, well above what's ever needed on this
        /// exact seed/save chain (confirmed against the release-1.0 fixture too) — capping it at 20
        /// instead made the race fail outright (see the gotcha in SOIStrategyTester/CLAUDE.md: once
        /// PriorityAutoplayStrategy considers CityCount "done" it stops expanding for good, even if a
        /// monster's hex was never actually reached).
        ///
        /// Palisade is gated on "a Bandit has been spotted" rather than built unconditionally: once the
        /// (always-present) BanditHideout is found, it periodically spawns Bandits that steal resources
        /// from any city without a Palisade (MonsterCombatEngine/MonsterController — Palisade fully blocks
        /// the attack, not just reduces it). That theft competes directly with this stage's own stockpile
        /// for Mine/Barracks, and is what turns this into a race the civilization can lose: confirmed by
        /// racing the no-Palisade order against FullIslandTest's continuous (no intermediate save/reload)
        /// path via SOIStrategyTester — it reliably times out at 60000 iterations with Bandits stealing
        /// faster than the economy can recover, while adding this stage clears the same race in ~3100.
        /// Moving Mine/Barracks earlier (before Quarry/Market/Seaport) was also tried as a fix and made
        /// things worse instead: both need 40-50 Stone, and without Quarry/Market already up to produce
        /// and trade for it, the grind stalls on Stone — same race, same outcome, with or without Palisade.
        /// </summary>
        private static IslandStepDefinition ExterminateMonstersStep(string saveName) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController),
                cond, maxIterations: 60000),
            Condition = ctrl => !ctrl.PrestigeController.HasSurfaceMonsters(),
            AssertFailMessage = _ => "Expected all surface monsters to have been exterminated",
        };

        /// <summary>
        /// Drives the Unified strategy with neighbor-attacking enabled (see
        /// <see cref="CivilizationAutoplayerPriorities.Unified"/>'s attackNeighborsAtCities parameter)
        /// until every NPC civilization has lost all of its cities. Unified itself guarantees Barracks
        /// level 1 in every city — including ones added later by its own expansion — before
        /// AttackNeighborsObjective is allowed to point flows at an enemy, so no separate
        /// Barracks-building step is needed ahead of this one anymore. Requires the Barracks prestige
        /// vertex to already be purchased.
        /// </summary>
        private static IslandStepDefinition ExterminateCivilizationsStep(string saveName, int maxIterations = 60000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController, attackNeighborsAtCities: 1),
                cond, maxIterations),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.Where(c => c.IsNpc).All(c => c.Cities.Count == 0),
            AssertFailMessage = ctrl =>
            {
                var remaining = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.Where(c => c.IsNpc).Sum(c => c.Cities.Count);
                return $"Expected all NPC civilizations to have been exterminated, {remaining} NPC cities remain";
            },
        };

        /// <summary>
        /// Drives the Unified strategy with neighbor-attacking enabled until 12 cities are reached, or
        /// 10 cities once the territory is fully saturated (no buildable roads and no buildable
        /// vertices remaining). Unified's own CityCountObjective(expansionTarget: 12) handles rebuilding
        /// towards that city count while AttackNeighborsObjective (guarded by the Barracks guarantee
        /// ahead of it, see <see cref="CivilizationAutoplayerPriorities.Unified"/>) handles attacking —
        /// no hand-rolled attack/rebuild alternation is needed here anymore.
        /// </summary>
        private static IslandStepDefinition AttackNeighborsAndExpandStep(string saveName, int maxIterations = 60000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController, attackNeighborsAtCities: 1),
                cond, maxIterations),
            Condition = ctrl =>
            {
                var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                if (civ.Cities.Count >= 12) return true;
                return civ.Cities.Count >= 10
                    && !ctrl.RoadController.GetBuildableRoads(civ.Index).Any()
                    && ctrl.CityBuilderController.GetBuildableVertices(civ.Index).Count == 0;
            },
            AssertFailMessage = ctrl =>
            {
                var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                int buildableRoads = ctrl.RoadController.GetBuildableRoads(civ.Index).Count;
                int buildableVertices = ctrl.CityBuilderController.GetBuildableVertices(civ.Index).Count;
                return $"Expected to reach 10+ cities with saturated territory (12 if possible); got {civ.Cities.Count} cities, {buildableRoads} buildable roads, {buildableVertices} buildable vertices";
            },
        };

        /// <summary>
        /// Builds the Library to level 1 in every city. Library needs city level >= 2 (Library.
        /// AvailableAtLevel) and a BUILDING_MAX_LEVEL("Library") modifier above its default max of 0 —
        /// both already in place by this point in Island2: the CentralVertex purchase in the Prestige step
        /// grants +3 max level and unlocks the research system in the same call. Inserted before the monster
        /// extermination step rather than folded into it: extermination's own priority list (see
        /// ExterminateMonstersStep) is tuned to the bare minimum that gets soldiers killing monsters, and
        /// Library has no bearing on that — keeping it as its own checkpoint avoids re-tuning that race
        /// every time research priorities change.
        /// </summary>
        private static IslandStepDefinition LibraryLevel1Step(string saveName, int maxIterations = 20000) => new()
        {
            SaveName = saveName,
            RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(new[]
            {
                PriorityStage.Buildings(new[] { BuildingType.Library }, targetLevel: 1),
            }, cond, maxIterations),
            Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities
                .All(c => IsBuildingAtLeastLevelForCity(ctrl, c, BuildingType.Library, targetLevel: 1)),
            AssertFailMessage = ctrl =>
            {
                var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                var missing = civ.Cities.Count(c => !IsBuildingAtLeastLevelForCity(ctrl, c, BuildingType.Library, targetLevel: 1));
                return $"Expected every city to have Library at level >= 1 (or be unable to build it), {missing} cities are missing it";
            },
        };

        private static bool IsBuildingAtLeastLevelForCity(MainGameController ctrl, City city, BuildingType type, int targetLevel)
        {
            var building = ctrl.BuildingController.GetBuildingOrBuildable(city, type);
            if (building == null) return true;
            var maxLevel = ctrl.BuildingController.GetMaxLevel(building, city.CivilizationIndex);
            return building.Level >= Math.Min(targetLevel, maxLevel);
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
                new()
                {
                    SaveName = "Island1_Cities2",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 15000),
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
                },
                new()
                {
                    SaveName = "Island1_Cities12",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 12,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 12 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island1_Points35",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 15000),
                    Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= Island1RequiredPrestigePoints,
                    AssertFailMessage = ctrl =>
                        $"Expected at least {Island1RequiredPrestigePoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
                },
                new()
                {
                    SaveName = "Island1_PrestigeReady",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 30000),
                    Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
                    AssertFailMessage = ctrl =>
                        $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
                },
            },
        };

        // ── Island 2 scenario ────────────────────────────────────────────────

        private const int Island2RequiredPrestigePoints = 70;

        internal static readonly IslandScenario Island2 = new()
        {
            Name = "Island2",
            // Start from Island 1's final save in the same folder (current or release-X.Y).
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island1_PrestigeReady"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island1_PrestigeReady"),
            Steps = new List<IslandStepDefinition>
            {
                // Prestige transition. Barracks is purchased first (deterministically, using
                // Island 1's 35 banked points) so monsters can be exterminated below; the remaining
                // balance, if any, is then spent greedily as usual.
                new()
                {
                    SaveName = "Island2_Prestige",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond, new[] { PrestigeMap.CentralVertex, PrestigeMap.BarracksVertex }),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 1,
                    AssertFailMessage = _ => "Expected prestige to have been performed (RunHistory is empty)",
                    IsPrestigeStep = true,
                },
                new()
                {
                    SaveName = "Island2_Cities2",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 15000),
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
                },
                new()
                {
                    SaveName = "Island2_Cities6",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 6,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 6 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island2_Cities10",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 10,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 10 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
                },
                LibraryLevel1Step("Island2_Library1"),
                ExterminateMonstersStep("Island2_NoMonsters"),
                new()
                {
                    SaveName = "Island2_Points70",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 50000),
                    Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= Island2RequiredPrestigePoints,
                    AssertFailMessage = ctrl =>
                        $"Expected at least {Island2RequiredPrestigePoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
                },
                new()
                {
                    SaveName = "Island2_PrestigeReady",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 30000),
                    Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
                    AssertFailMessage = ctrl =>
                        $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
                },
            },
        };

        // ── Island 3 scenario ────────────────────────────────────────────────

        internal static readonly IslandScenario Island3 = new()
        {
            Name = "Island3",
            // Start from Island 2's final save in the same folder (current or release-X.Y).
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island2_PrestigeReady"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island2_PrestigeReady"),
            Steps = new List<IslandStepDefinition>
            {
                // Second prestige transition + greedy point distribution.
                new()
                {
                    SaveName = "Island3_Prestige",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 2,
                    AssertFailMessage = _ => "Expected second prestige to have been performed (RunHistory.Count < 2)",
                    IsPrestigeStep = true,
                },
                new()
                {
                    SaveName = "Island3_Cities2",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 15000),
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
                },
                new()
                {
                    SaveName = "Island3_Cities6",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 6,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 6 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island3_Cities10",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 10,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 10 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island3_Cities15",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl =>
                    {
                        var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                        return !ctrl.RoadController.GetBuildableRoads(civ.Index).Any();
                    },
                    AssertFailMessage = ctrl =>
                    {
                        var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                        int buildableRoads = ctrl.RoadController.GetBuildableRoads(civ.Index).Count;
                        int buildableVertices = ctrl.CityBuilderController.GetBuildableVertices(civ.Index).Count;
                        return $"Expected road network to be saturated (0 buildable roads); got {civ.Cities.Count} cities, {buildableRoads} buildable roads, {buildableVertices} buildable vertices";
                    },
                },
                ExterminateCivilizationsStep("Island3_NoEnemies"),
                new()
                {
                    SaveName = "Island3_Points20",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 15000),
                    Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints,
                    AssertFailMessage = ctrl =>
                        $"Expected at least {PrestigeController.PrestigeRequiredPoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
                },
                new()
                {
                    SaveName = "Island3_PrestigeReady",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 30000),
                    Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
                    AssertFailMessage = ctrl =>
                        $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
                },
                WonderPlacedStep("Island3_WonderPlaced"),
                WonderLevelStep("Island3_Wonder1", targetLevel: 1),
                new()
                {
                    SaveName = "Island3_Points700",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 50000),
                    Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= 700,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 700 prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
                },
            },
        };

        // ── Island 4 scenario ────────────────────────────────────────────────

        internal static readonly IslandScenario Island4 = new()
        {
            Name = "Island4",
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island3_Points700"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island3_Points700"),
            Steps = new List<IslandStepDefinition>
            {
                // Third prestige transition. Island 4 is Lake-shaped (compact with lake) — all
                // civilizations share the same landmass. No maritime routes are needed; the greedy
                // distributor spends the banked surplus on whatever is most cost-effective.
                new()
                {
                    SaveName = "Island4_Prestige",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 3,
                    AssertFailMessage = _ => "Expected third prestige to have been performed (RunHistory.Count < 3)",
                    IsPrestigeStep = true,
                },
                new()
                {
                    SaveName = "Island4_Cities2",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 15000),
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
                },
                new()
                {
                    SaveName = "Island4_Cities6",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count >= 6,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 6 cities, got {ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island4_Cities10",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 40000),
                    Condition = ctrl =>
                    {
                        var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                        return !ctrl.RoadController.GetBuildableRoads(civ.Index).Any();
                    },
                    AssertFailMessage = ctrl =>
                    {
                        var civ = ctrl.CurrentMainState!.CurrentWorldState!.Civilizations.First();
                        int buildableRoads = ctrl.RoadController.GetBuildableRoads(civ.Index).Count;
                        int buildableVertices = ctrl.CityBuilderController.GetBuildableVertices(civ.Index).Count;
                        return $"Expected road network to be saturated (0 buildable roads); got {civ.Cities.Count} cities, {buildableRoads} buildable roads, {buildableVertices} buildable vertices";
                    },
                },
                AttackNeighborsAndExpandStep("Island4_ExtermineAndRebuild"),
                new()
                {
                    SaveName = "Island4_Points20",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 100000),
                    Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints,
                    AssertFailMessage = ctrl =>
                        $"Expected at least {PrestigeController.PrestigeRequiredPoints} prestige points (has {ctrl.PrestigeController.CalculatePrestigePoints()})",
                },
                new()
                {
                    SaveName = "Island4_PrestigeReady",
                    RunAction = (runner, cond) => runner.RunPriorityStrategyUntil(
                        CivilizationAutoplayerPriorities.Unified(runner.Autoplayer, runner.BuildingController), cond, 300000),
                    Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
                    AssertFailMessage = ctrl =>
                        $"Expected prestige to be available (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
                },
                WonderPlacedStep("Island4_WonderPlaced"),
                // One level further than Island 3's target (level 1) — the Wonder is a fresh,
                // per-island feature, so Island 4 re-places it and pushes one level beyond.
                WonderLevelStep("Island4_Wonder2", targetLevel: 2, maxIterations: 300000),
            },
        };

        // ── Island 5 scenario ────────────────────────────────────────────────
        // Only goes up to the start of the run — the fourth prestige transition out of Island 4.

        internal static readonly IslandScenario Island5 = new()
        {
            Name = "Island5",
            CreateFreshController = folder => SaveUtils.LoadSave(folder, "Island4_Wonder2"),
            IsInputAvailable = folder => SaveUtils.SaveExists(folder, "Island4_Wonder2"),
            Steps = new List<IslandStepDefinition>
            {
                // Fourth prestige transition + greedy point distribution.
                new()
                {
                    SaveName = "Island5_Prestige",
                    RunAction = (runner, cond) => runner.RunStepPrestige(cond),
                    Condition = ctrl => ctrl.CurrentMainState?.PrestigeState?.RunHistory.Count >= 4,
                    AssertFailMessage = _ => "Expected fourth prestige to have been performed (RunHistory.Count < 4)",
                    IsPrestigeStep = true,
                },
            },
        };
    }
}
