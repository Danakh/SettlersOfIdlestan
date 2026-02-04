using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;

namespace SOITests.ControllerTests
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

            // Save the generated game and verify a round-trip load produces identical state
            SaveUtils.SaveAndReloadAndAssertEqual(controller, "FulIslandTestStart");
        }
    }
}
