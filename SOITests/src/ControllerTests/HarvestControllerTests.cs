using System;
using System.Collections.Generic;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;

namespace SOITests.ControllerTests
{
    public class HarvestControllerTests
    {
        [Fact]
        public void AutomaticHarvest_ByProductionBuilding_AddsResourceWithCooldown()
        {
            // Create a small map where one of the hexes produces wood
            var a = new HexCoord(0, 0);
            var b = new HexCoord(1, 0);
            var c = new HexCoord(0, 1);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest), // wood
                new HexTile(b, TerrainType.Field),  // wheat
                new HexTile(c, TerrainType.Field),  // wheat
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new List<Civilization> { civ };
            var state = new IslandState(map, civs);

            // Place a city adjacent to the wood tile and add a Sawmill (produces wood)
            var vertex = Vertex.Create(a, b, c);
            var city = new City(vertex) { CivilizationIndex = 0 };
            city.Buildings.Add(new Sawmill());
            civ.Cities.Add(city);

            var clock = new GameClock();
            clock.Start();

            // Create a harvest controller that listens to the clock
            var harvestController = new HarvestController(state, clock);

            // Initially no wood
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));

            // First small advance should trigger an automatic harvest (first time)
            clock.Advance(TimeSpan.FromSeconds(0.1));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Advance a bit but stay within automatic cooldown -> no new harvest
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Advance beyond the automatic cooldown (5s) -> another harvest should occur
            clock.Advance(TimeSpan.FromSeconds(5));
            Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        }
    }
}
