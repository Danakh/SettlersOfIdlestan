using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
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

        [Fact]
        public void SecondColony_FromFirstColony_ExpandAndBuild()
        {
            var controller = SaveUtils.LoadSave("FulIslandTestFirstColony");

            var civ = controller.CurrentMainState?.CurrentIslandState?.Civilizations.FirstOrDefault();
            Assert.NotNull(civ);

            var originalCity = civ.Cities.FirstOrDefault();
            Assert.NotNull(originalCity);

            var autoplayer = new CivilizationAutoplayer(civ, controller.CurrentMainState!.CurrentIslandState!.Map, controller);

            // Build roads until we have a buildable road at distance 2
            autoplayer.AutoBuildRoadToDistance(2);

            // Find a buildable vertex for a new outpost (different from original city)
            var buildableVerts = controller.CityBuilderController.GetBuildableVertices(civ.Index);
            var newVertex = buildableVerts.FirstOrDefault(v => !v.Equals(originalCity.Position));
            Assert.NotNull(newVertex);

            // Build outpost, then TownHall
            var outpostOk = autoplayer.AutoBuildOutpost(newVertex);
            Assert.True(outpostOk, "Failed to build outpost at the new vertex");

            var townOk = autoplayer.AutoBuildBuilding(newVertex, BuildingType.TownHall);
            Assert.True(townOk, "Failed to build TownHall in the new outpost");

            // Build all available production buildings for the new city
            // Repeat until no more production buildings are buildable
            for (int i = 0; i < 20; i++)
            {
                var candidates = controller.BuildingController.GetBuildableBuildings(civ.Index, newVertex)
                                 .Where(b => b.Production != null && b.Production.Any())
                                 .ToList();
                if (!candidates.Any()) break;

                foreach (var cand in candidates)
                {
                    autoplayer.AutoBuildBuilding(newVertex, cand.Type);
                }
            }

            // Verify the new city has a TownHall and at least the production buildings we attempted to build
            var createdCity = civ.Cities.FirstOrDefault(c => c.Position.Equals(newVertex));
            Assert.NotNull(createdCity);

            Assert.True(createdCity.Buildings.Any(b => b.Type == BuildingType.TownHall), "TownHall not found in created city");

            var productionBuilt = createdCity.Buildings.Where(b => b.Production != null && b.Production.Any()).Select(b => b.Type).ToHashSet();
            // Ensure we built at least one production building
            Assert.True(productionBuilt.Count >= 1, "Expected at least one production building in the new city");

            // Save final state for inspection if needed
            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FulIslandTestSecondColony");
        }
    }
}
