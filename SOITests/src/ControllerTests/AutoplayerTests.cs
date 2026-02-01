using System;
using System.Linq;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.City;

namespace SOITests.ControllerTests
{
    public class AutoplayerTests
    {
        [Fact]
        public void Autoplayer_BuildsTwoRoadsByHarvestingAndAdvancingClock()
        {
            // Create a small map with 4 tiles around the initial area
            var a = new HexCoord(0, 0);
            var b = new HexCoord(1, 0);
            var c = new HexCoord(0, 1);
            var d = new HexCoord(1, 1);

            // Assign terrains so harvesting will provide wood and brick over multiple waves
            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest), // wood
                new HexTile(b, TerrainType.Hill),   // brick
                new HexTile(c, TerrainType.Forest), // wood
                new HexTile(d, TerrainType.Hill),   // brick
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new System.Collections.Generic.List<Civilization> { civ };
            var state = new IslandState(map, civs);

            // Place the initial city at the vertex formed by a,b,c
            var vertex = Vertex.Create(a, b, c);
            civ.Cities.Add(new City(vertex) { CivilizationIndex = 0 });

            var clock = new SettlersOfIdlestan.Model.Game.GameClock();
            clock.Start();

            var roadController = new RoadController(state);
            var harvestController = new HarvestController(state, clock);
            var auto = new CivilizationAutoplayer(civ, map, roadController, harvestController);

            // Helper to repeatedly attempt building an edge with the autoplayer while advancing the clock
            bool TryBuildWithAuto(Edge edge)
            {
                const int maxIterations = 500; // safety to avoid infinite loops
                for (int i = 0; i < maxIterations; i++)
                {
                    if (auto.AutoBuildRoad(edge)) return true;
                    // advance by 0.1 seconds of real time -> scaled by GameClock.Speed (1.0 by default)
                    clock.Advance(TimeSpan.FromSeconds(0.1));
                }
                return false;
            }

            // Pick a first buildable edge adjacent to the city
            var firstBuildable = roadController.GetBuildableRoads(0).First().Position;
            var firstBuilt = TryBuildWithAuto(firstBuildable);
            Assert.True(firstBuilt, "First road should eventually be built by the autoplayer");
            Assert.Contains(civ.Roads, r => r.Position.Equals(firstBuildable));

            // Pick a second buildable road that is not the first one (should extend away from the city)
            var secondBuildable = roadController.GetBuildableRoads(0).Select(r => r.Position).First(e => !e.Equals(firstBuildable));
            var secondBuilt = TryBuildWithAuto(secondBuildable);
            Assert.True(secondBuilt, "Second road should eventually be built by the autoplayer");
            Assert.Contains(civ.Roads, r => r.Position.Equals(secondBuildable));

            // Verify that at least 20 seconds of in-game time have passed
            Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(18), $"Expected at least 18s elapsed in the GameClock, was {clock.Elapsed}");
        }
    }
}
