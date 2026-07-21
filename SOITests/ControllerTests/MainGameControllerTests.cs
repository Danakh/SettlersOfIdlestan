using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System.Linq;
using System.Text.Json;
using Xunit;

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
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            var imported = controller.ImportMainState(json);

            Assert.NotNull(imported);
            var importedIsland = imported.CurrentWorldState;
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
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            // L'export est chiffré — on passe par ImportMainState pour le round-trip
            var controller2 = new MainGameController();
            var round = controller2.ImportMainState(exported);

            Assert.NotNull(round);
            var island = round.CurrentWorldState;
            Assert.NotNull(island);
            Assert.NotEmpty(island.Civilizations);
            var civ = island.Civilizations[0];
            Assert.NotEmpty(civ.Cities);
        }

        [Fact]
        public void ExportMainState_RoundtripPreservesAutomationSettings()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));
            mainState.CurrentWorldState!.AutomationSettings.MilitaryReinforcementAutomationEnabled = true;
            mainState.Settings.PinnedCivPanelKeys.Add("MilitaryReinforcement");

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            var controller2 = new MainGameController();
            var round = controller2.ImportMainState(exported);

            Assert.True(round.CurrentWorldState!.AutomationSettings.MilitaryReinforcementAutomationEnabled);
            Assert.Contains("MilitaryReinforcement", round.Settings.PinnedCivPanelKeys);
        }

        [Fact]
        public void ImportMainState_MigratesLegacyPerIslandPinsIntoPersistentSettings()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));
            // Ancien format (pré-migration) : les épingles étaient stockées par île.
            mainState.CurrentWorldState!.AutomationSettings.PinnedToCivPanel.Add("MilitaryReinforcement");

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            var imported = controller.ImportMainState(json);

            Assert.Contains("MilitaryReinforcement", imported.Settings.PinnedCivPanelKeys);
        }

        [Fact]
        public void ExportMainState_RoundtripPreservesNpcAggressivityEscalation()
        {
            var WorldState = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new GameClock();
            var mainState = new MainGameState(WorldState, clock, new GamePRNG(42));

            var npcCiv = new SettlersOfIdlestan.Model.Civilization.Civilization
            {
                Index = mainState.CurrentWorldState!.Civilizations.Max(c => c.Index) + 1,
                IsNpc = true,
                NpcParameters = new SettlersOfIdlestan.Model.Civilization.NpcParameters
                {
                    // État tel que laissé par NpcGameController.OnCityAttacked après une attaque du joueur.
                    AggressivityLevel = SettlersOfIdlestan.Model.Civilization.NpcAggressivityLevel.Warlike,
                },
                WarEnemyCivIndices = { 0 },
            };
            mainState.CurrentWorldState.Civilizations.Add(npcCiv);

            var json = JsonSerializer.Serialize(mainState, SaveController.SerializationOptions());

            var controller = new MainGameController();
            controller.ImportMainState(json);

            var exported = controller.ExportMainState();

            var controller2 = new MainGameController();
            var round = controller2.ImportMainState(exported);

            var roundNpc = round.CurrentWorldState!.Civilizations.Single(c => c.IsNpc);
            Assert.NotNull(roundNpc.NpcParameters);
            Assert.Equal(SettlersOfIdlestan.Model.Civilization.NpcAggressivityLevel.Warlike, roundNpc.NpcParameters!.AggressivityLevel);
            Assert.Contains(0, roundNpc.WarEnemyCivIndices);
        }
    }
}
