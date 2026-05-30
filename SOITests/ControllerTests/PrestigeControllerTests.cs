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

        // ── Bandit prestige bonus ────────────────────────────────────────────

        [Fact]
        public void Prestige_BanditBonus_ZeroWhenNoBanditsDefeated()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.RunRecord.BanditsDefeated = 0;
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state);

            Assert.Equal(0, controller.GetBanditBonus());
        }

        [Fact]
        public void Prestige_BanditBonus_TwentyPercentOfBuildingSubtotal()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.RunRecord.BanditsDefeated = 3;
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Temple());
            civ.Cities[0].Buildings.Add(new Temple());
            civ.Cities[0].Buildings.Add(new Temple());
            civ.Cities[0].Buildings.Add(new Temple());
            civ.Cities[0].Buildings.Add(new Temple()); // 5 temples = subtotal 5
            var controller = new PrestigeController();
            controller.Initialize(civ, state);

            Assert.Equal(5, controller.GetBuildingSubtotal());
            Assert.Equal(1, controller.GetBanditBonus()); // 5 / 5 = 1
        }

        // ── Wonder prestige bonus ────────────────────────────────────────────

        [Fact]
        public void Prestige_WonderBonus_ZeroWhenNoWonder()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var clock = new SettlersOfIdlestan.Model.Game.GameClock();
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(0, controller.GetWonderBonus());
        }

        [Fact]
        public void Prestige_WonderBonus_ZeroWhenWonderAtLevel0()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0)) { Level = 0 });
            var clock = new SettlersOfIdlestan.Model.Game.GameClock();
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(0, controller.GetWonderBonus());
        }

        [Fact]
        public void Prestige_WonderBonus_LevelTimesTimeFactor()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0)) { Level = 2 });
            // runTicks = 720001 - 1 = 720000 = 2h exactement → ceil(2) = 2 → timeFactor = 3
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 720001 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(6, controller.GetWonderBonus()); // 2 × (1+2) = 6
        }

        [Fact]
        public void Prestige_WonderBonus_HoursRoundedUp()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0)) { Level = 1 });
            // runTicks = 180001 - 1 = 180000 = 30 min → ceil(0.5) = 1h → timeFactor = 2
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 180001 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            Assert.Equal(2, controller.GetWonderBonus()); // 1 × (1+1) = 2
        }

        [Fact]
        public void Prestige_WonderBonus_CountedInTotal()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0)) { Level = 1 });
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Temple()); // subtotal = 1
            // runTicks = 360001 - 1 = 360000 = 1h → ceil(1) = 1 → timeFactor = 2
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 360001 };
            var controller = new PrestigeController();
            controller.Initialize(civ, state, clock);

            // total = buildingSubtotal(1) × wonderMultiplier(2) + banditBonus(0) = 2
            Assert.Equal(2, controller.CalculatePrestigePoints());
        }

        [Fact]
        public void Prestige_WonderBonusDetails_ReturnsCorrectValues()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.StartTick = 1;
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Wonder(new HexCoord(0, 0)) { Level = 3 });
            // runTicks = 360001 - 1 = 360000 = 1h
            var clock = new SettlersOfIdlestan.Model.Game.GameClock { CurrentTick = 360001 };
            var controller = new PrestigeController();
            controller.Initialize(state.Civilizations[0], state, clock);

            var (level, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            Assert.Equal(3, level);
            Assert.Equal(2, timeFactor); // 1 + ceil(1h) = 2
            Assert.Equal(360000, runTicks);
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
            civ.UniqueBuildings.Add(BuildingType.ImperialPort);
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
