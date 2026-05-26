using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Expand;

namespace SOITests.IslandMapTests
{
    public class FullIslandTest
    {
        private static MainGameController CreateFreshController()
        {
            var controller = new MainGameController();
            var tileData = new List<(TerrainType terrainType, int tileCount)>
            {
                (TerrainType.Forest, 4),
                (TerrainType.Hill, 4),
                (TerrainType.Plain, 4),
                (TerrainType.Mountain, 4),
                (TerrainType.Mountain, 1),
            };
            var parameters = new IslandParameters(AtlasController.InvalidIslandId, tileData, 1);
            controller.CreateNewGame(parameters);
            return controller;
        }

        [Fact]
        public void Step1_ReachTwoCities()
        {
            var controller = CreateFreshController();
            var civ = controller.CurrentMainState!.CurrentIslandState!.Civilizations[0];

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState.CurrentIslandState.Map, controller);
            var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

            runner.RunStep1Until(() => civ.Cities.Count >= 2
                && civ.Cities.All(c => c.Buildings.Any(b => b.Type == SettlersOfIdlestan.Model.Buildings.BuildingType.TownHall)));

            Assert.True(civ.Cities.Count >= 2, $"Expected at least 2 cities after Step 1, got {civ.Cities.Count}");
            Assert.True(civ.Cities.All(c => c.Buildings.Any(b => b.Type == SettlersOfIdlestan.Model.Buildings.BuildingType.TownHall)),
                "Every city should have a TownHall");

            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FullIslandTestStep1");
        }

        [Fact]
        public void Step2_ReachSixCities()
        {
            const int expectedCityCount = 6;
            var controller = SaveUtils.LoadSave("FullIslandTestStep1");
            var civ = controller.CurrentMainState!.CurrentIslandState!.Civilizations.First();

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState.CurrentIslandState.Map, controller);
            var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

            runner.RunStep2Until(() => civ.Cities.Count >= expectedCityCount);

            Assert.True(civ.Cities.Count >= expectedCityCount, $"Expected at least {expectedCityCount} cities after Step 2, got {civ.Cities.Count}");

            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FullIslandTestStep2");
        }

        [Fact]
        public void Step2bis_ReachTenCities()
        {
            const int expectedCityCount = 10;
            var controller = SaveUtils.LoadSave("FullIslandTestStep2");
            var civ = controller.CurrentMainState!.CurrentIslandState!.Civilizations.First();

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState.CurrentIslandState.Map, controller);
            var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

            runner.RunStep2Until(() => civ.Cities.Count >= expectedCityCount);

            Assert.True(civ.Cities.Count >= expectedCityCount, $"Expected at least {expectedCityCount} cities after Step 2bis, got {civ.Cities.Count}");

            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FullIslandTestStep2bis");
        }

        [Fact]
        public void Step3_CanPrestige()
        {
            var controller = SaveUtils.LoadSave("FullIslandTestStep2bis");
            var civ = controller.CurrentMainState!.CurrentIslandState!.Civilizations.First();

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState.CurrentIslandState.Map, controller);
            var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

            runner.RunStep3Until(() => controller.PrestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints, false);

            Assert.True(controller.PrestigeController.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints,
                $"Expected enough prestige points after Step 3 (has {controller.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints} points)");

            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FullIslandTestStep3");
        }

        [Fact]
        public void Step3bis_CanPrestige()
        {
            var controller = SaveUtils.LoadSave("FullIslandTestStep3");
            var civ = controller.CurrentMainState!.CurrentIslandState!.Civilizations.First();

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState.CurrentIslandState.Map, controller);
            var runner = new CivilizationAutoplayerRunner(autoplayer, civ, controller);

            runner.RunStep3Until(() => controller.PrestigeController.PrestigeIsAvailable(), false);

            Assert.True(controller.PrestigeController.PrestigeIsAvailable(),
                $"Expected prestige to be available after Step 3 (has {controller.PrestigeController.CalculatePrestigePoints()} / {PrestigeController.PrestigeRequiredPoints} points)");

            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FullIslandTestStep3bis");
        }
    }
}
