using System.Text.Json;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.PrestigeMap;
using SettlersOfIdlestan.Model.HexGrid;
using SOITests.TestUtilities;

namespace SOITests.ControllerTests
{
    public class MainGameControllerTests
    {
        private static JsonSerializerOptions CreateOptions(bool writeIndented = false, bool caseInsensitive = false)
        {
            var options = new JsonSerializerOptions { WriteIndented = writeIndented, PropertyNameCaseInsensitive = caseInsensitive };
            options.Converters.Add(new HexCoordJsonConverter());
            options.Converters.Add(new SettlersOfIdlestan.Model.IslandMap.IslandMapJsonConverter());
            options.Converters.Add(new VertexJsonConverter());
            return options;
        }

        [Fact]
        public void ImportMainState_PreservesCities()
        {
            var islandState = IslandTestFactory.CreateSevenHexIslandState();
            var prestige = new PrestigeState(islandState);
            var god = new GodState(prestige);
            var clock = new GameClock();
            var mainState = new MainGameState(god, clock);

            var options = CreateOptions(writeIndented: true);
            var json = JsonSerializer.Serialize(mainState, options);

            var controller = new MainGameController();
            var imported = controller.ImportMainState(json);

            Assert.NotNull(imported);
            var importedIsland = imported.CurrentIslandState;
            Assert.NotNull(importedIsland);
            Assert.NotEmpty(importedIsland.Civilizations);
            var civ = importedIsland.Civilizations[0];
            Assert.NotEmpty(civ.Cities);
            Assert.NotNull(civ.Cities[0].Position);
            Assert.Equal(3, civ.Cities[0].Position.GetHexes().Length);
        }

        [Fact]
        public void ExportMainState_RoundtripPreservesCities()
        {
            var islandState = IslandTestFactory.CreateSevenHexIslandState();
            var prestige = new PrestigeState(islandState);
            var god = new GodState(prestige);
            var clock = new GameClock();
            var mainState = new MainGameState(god, clock);

            var serializeOptions = CreateOptions(writeIndented: true);
            var json = JsonSerializer.Serialize(mainState, serializeOptions);

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            var deserializeOptions = CreateOptions(caseInsensitive: true);
            var round = JsonSerializer.Deserialize<MainGameState>(exported, deserializeOptions);

            Assert.NotNull(round);
            var island = round.CurrentIslandState;
            Assert.NotNull(island);
            Assert.NotEmpty(island.Civilizations);
            var civ = island.Civilizations[0];
            Assert.NotEmpty(civ.Cities);
        }
    }
}
