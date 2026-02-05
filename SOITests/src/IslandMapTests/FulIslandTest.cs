using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Model.Buildings;

namespace SOITests.src.IslandMapTests
{
    public class FulIslandTest
    {
        [Fact]
        public void GenerateFullIsland_And_SaveStart()
        {
            var controller = new MainGameController();

            // Request 3 tiles of each of the four basic resource terrains
            var tileData = new List<(TerrainType terrainType, int tileCount)>
            {
                (TerrainType.Forest, 3),
                (TerrainType.Hill, 3),
                (TerrainType.Pasture, 3),
                (TerrainType.Field, 3),
                (TerrainType.Mountain, 3),
            };

            var mainState = controller.CreateNewGame(tileData, civilizationCount: 1);
            Assert.NotNull(mainState);

            // Ensure we can grind resources: repeatedly AutoGrind until civ has at least 5 wood and 5 brick
            var civ = controller.CurrentMainState?.CurrentIslandState?.Civilizations[0];
            Assert.NotNull(civ);

            // Start the game clock so harvest cooldowns progress
            controller.Clock?.Start();

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState!.CurrentIslandState!.Map, controller);

            Dictionary<Resource, int> requiredResources = new Dictionary<Resource, int>(); // No specific resource requirements for grinding
            requiredResources.Add(Resource.Wood, 5);
            requiredResources.Add(Resource.Brick, 5);
            autoplayer.AutoGrind(requiredResources);

            Assert.True(civ.GetResourceQuantity(SettlersOfIdlestan.Model.IslandMap.Resource.Wood) >= 5, "Expected at least 5 wood after grinding");
            Assert.True(civ.GetResourceQuantity(SettlersOfIdlestan.Model.IslandMap.Resource.Brick) >= 5, "Expected at least 5 brick after grinding");

            // Verify that at least 8 seconds of in-game time have passed
            Assert.True(controller.Clock != null && controller.Clock.Elapsed >= System.TimeSpan.FromSeconds(8), $"Expected at least 8s elapsed in the GameClock, was {controller.Clock?.Elapsed}");

            // Save the generated game and verify a round-trip load produces identical state
            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FulIslandTestStart");
        }

        [Fact]
        public void LoadStart_BuildFirstColony_And_Save()
        {
            var controller = SaveUtils.LoadSave("FulIslandTestStart");
            // Load the previously saved game state

            var civ = controller.CurrentMainState?.CurrentIslandState?.Civilizations.FirstOrDefault();
            Assert.NotNull(civ);

            var city = civ.Cities.FirstOrDefault();
            Assert.NotNull(city);

            var vertex = city.Position;

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState!.CurrentIslandState!.Map, controller);

            // Build Market, TownHall, then production buildings Brickworks and Sawmill
            autoplayer.AutoBuildBuilding(vertex, BuildingType.Market);
            autoplayer.AutoBuildBuilding(vertex, BuildingType.TownHall);
            autoplayer.AutoBuildBuilding(vertex, BuildingType.Brickworks);
            autoplayer.AutoBuildBuilding(vertex, BuildingType.Sawmill);

            // Verify the colony has the expected buildings
            var expectedBuildings = new HashSet<BuildingType> { BuildingType.Market, BuildingType.TownHall, BuildingType.Brickworks, BuildingType.Sawmill };
            var actualBuildings = city.Buildings.Select(b => b.Type).ToHashSet();
            Assert.Equal(expectedBuildings, actualBuildings);

            // Save the generated game and verify a round-trip load produces identical state
            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FulIslandTestFirstColony");
        }
    }
}
