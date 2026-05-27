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
        // ── Island 1 scenario ────────────────────────────────────────────────

        private static readonly IslandScenario Island1 = new()
        {
            Name = "Island1",
            CreateFreshController = () =>
            {
                var controller = new MainGameController();
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest,   4),
                    (TerrainType.Hill,     4),
                    (TerrainType.Plain,    4),
                    (TerrainType.Mountain, 4),
                    (TerrainType.Mountain, 1),
                };
                controller.CreateNewGame(new IslandParameters(AtlasController.InvalidIslandId, tileData, 1));
                return controller;
            },
            Steps = new List<IslandStepDefinition>
            {
                new()
                {
                    SaveName = "Island1_Step1",
                    RunAction = (runner, cond) => runner.RunStep1Until(cond),
                    Condition = ctrl =>
                    {
                        var civ = ctrl.CurrentMainState!.CurrentIslandState!.Civilizations[0];
                        return civ.Cities.Count >= 2
                            && civ.Cities.All(c => c.Buildings.Any(b => b.Type == BuildingType.TownHall));
                    },
                    AssertFailMessage = ctrl =>
                    {
                        var civ = ctrl.CurrentMainState!.CurrentIslandState!.Civilizations[0];
                        return $"Expected at least 2 cities with TownHall after Step 1, got {civ.Cities.Count}";
                    },
                },
                new()
                {
                    SaveName = "Island1_Step2",
                    RunAction = (runner, cond) => runner.RunStep2Until(cond),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentIslandState!.Civilizations.First().Cities.Count >= 6,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 6 cities after Step 2, got {ctrl.CurrentMainState!.CurrentIslandState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island1_Step2bis",
                    RunAction = (runner, cond) => runner.RunStep2Until(cond),
                    Condition = ctrl => ctrl.CurrentMainState!.CurrentIslandState!.Civilizations.First().Cities.Count >= 10,
                    AssertFailMessage = ctrl =>
                        $"Expected at least 10 cities after Step 2bis, got {ctrl.CurrentMainState!.CurrentIslandState!.Civilizations.First().Cities.Count}",
                },
                new()
                {
                    SaveName = "Island1_Step3",
                    RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: false),
                    Condition = ctrl => ctrl.PrestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints,
                    AssertFailMessage = ctrl =>
                        $"Expected enough prestige points after Step 3 (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
                },
                new()
                {
                    SaveName = "Island1_Step3bis",
                    RunAction = (runner, cond) => runner.RunStep3Until(cond, shouldExpand: false),
                    Condition = ctrl => ctrl.PrestigeController.PrestigeIsAvailable(),
                    AssertFailMessage = ctrl =>
                        $"Expected prestige to be available after Step 3bis (has {ctrl.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints})",
                },
            },
        };

        // ── Current-mode tests (create/overwrite saves/current) ──────────────

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

        // ── Release-regression tests (load saves/release-1.0, no save) ───────
        // These pass silently when release-1.0 saves are not yet present.

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
    }
}
