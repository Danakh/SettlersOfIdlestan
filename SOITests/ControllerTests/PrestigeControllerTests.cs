using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SOITests.TestUtilities;
using SettlersOfIdlestan.Model;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Expand;

namespace SOITests.ControllerTests
{
    public class PrestigeControllerTests
    {
        [Fact]
        public void Prestige_PrestigePointCount()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            Assert.Equal(0, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings.Add(new TownHall());
            Assert.Equal(1, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings.Add(new Library());
            Assert.Equal(2, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings[0].Level = 2; // raise townhall to level 2 (no change)
            Assert.Equal(2, controller.CalculatePrestigePoints());
            civ.Cities[0].Buildings[0].Level = 3; // raise townhall to level 3 (+1 point)
            Assert.Equal(3, controller.CalculatePrestigePoints());
        }

        [Fact]
        public void Prestige_SourcesMatchCalculatedTotal()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            civ.Cities[0].Buildings.Add(new TownHall { Level = 3 });
            civ.Cities[0].Buildings.Add(new Library());
            civ.Cities[0].Buildings.Add(new Temple());

            Assert.Equal(controller.CalculatePrestigePoints(), controller.GetPrestigePointSources().Sum(source => source.Points));
        }

        [Fact]
        public void Prestige_SourcesAreGroupedBySource()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            civ.Cities[0].Buildings.Add(new Temple());
            civ.Cities[0].Buildings.Add(new Temple());
            civ.Cities[0].Buildings.Add(new Library());

            var sources = controller.GetPrestigePointSources();
            Assert.Equal(2, sources.Count);
            Assert.Equal(2, sources.Single(source => source.LabelKey == "building_temple_name").Points);
            Assert.Equal(1, sources.Single(source => source.LabelKey == "building_library_name").Points);
        }

        // ── No-bandit prestige bonus ─────────────────────────────────────────

        [Fact]
        public void Prestige_NoBanditsOnDesertIsland_AddsNoBanditBonus()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            // Replace the center tile with a desert tile to enable the bandit-free bonus condition.
            // (The state has no bandits and we just need a desert tile to be present.)
            var desertState = CreateDesertIslandState();

            var controller = new PrestigeController();
            controller.Initialize(desertState.Civilizations[0], desertState);

            Assert.Contains(controller.GetPrestigePointSources(), s => s.LabelKey == "prestige_no_bandits" && s.Points == 2);
        }

        [Fact]
        public void Prestige_WithBanditsOnDesertIsland_NoNoBanditBonus()
        {
            var state = CreateDesertIslandState();
            state.Bandits.Add(new SettlersOfIdlestan.Model.Bandits.Bandit(new HexCoord(0, 0)));

            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state);

            Assert.DoesNotContain(controller.GetPrestigePointSources(), s => s.LabelKey == "prestige_no_bandits");
        }

        [Fact]
        public void Prestige_NoBandits_BonusCountedInTotal()
        {
            var state = CreateDesertIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Temple()); // +1 from temple

            var controller = new PrestigeController();
            controller.Initialize(civ, state);

            // +1 from temple + 2 from no-bandit bonus = 3
            Assert.Equal(3, controller.CalculatePrestigePoints());
        }

        private static IslandState CreateDesertIslandState()
        {
            var tiles = new List<HexTile>
            {
                new(new HexCoord(0, 0), TerrainType.Desert),
                new(new HexCoord(1, 0), TerrainType.Plain),
                new(new HexCoord(0, 1), TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new SettlersOfIdlestan.Model.Civilization.Civilization { Index = 0 };
            var vertex = Vertex.Create(new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(0, 1));
            var city = new SettlersOfIdlestan.Model.Civilization.City(vertex) { CivilizationIndex = 0 };
            civ.Cities.Add(city);
            return new IslandState(map, new List<SettlersOfIdlestan.Model.Civilization.Civilization> { civ }, AtlasController.InvalidIslandId);
        }

        [Fact]
        public void MainGameController_PerformPrestige_AddsPointsAndCreatesNextIsland()
        {
            var controller = new MainGameController();
            controller.CreateNewGame();
            var initialIsland = controller.CurrentMainState!.CurrentIslandState!;
            var civ = initialIsland.PlayerCivilization;
            for (int i = 0; i < 20; i++)
            {
                civ.Cities[0].Buildings.Add(new Temple());
            }
            var expectedPrestigePoints = controller.PrestigeController.CalculatePrestigePoints();

            controller.PerformPrestige();

            var newIsland = controller.CurrentMainState!.CurrentIslandState!;
            Assert.Equal(expectedPrestigePoints, controller.CurrentMainState.PrestigeState!.PrestigePoints);
            Assert.NotSame(initialIsland, newIsland);
            Assert.Equal(initialIsland.IslandID + 1, newIsland.IslandID);
            Assert.False(controller.PrestigeController.PrestigeIsVisible());
        }
    }
}
