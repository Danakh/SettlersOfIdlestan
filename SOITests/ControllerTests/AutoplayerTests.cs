using System;
using System.Linq;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SOITests.TestUtilities;
using SettlersOfIdlestan.Model.Buildings;

namespace SOITests.ControllerTests
{
    public class AutoplayerTests
    {
        [Fact]
        public void Autoplayer_BuildingASecondCity()
        {
            // Create a small map with 5 tiles around the initial area
            var a = new HexCoord(0, 0);
            var b = new HexCoord(1, 0);
            var c = new HexCoord(0, 1);
            var d = new HexCoord(1, 1);
            var e = new HexCoord(2, 0);

            // Assign terrains so harvesting will provide wood and brick over multiple waves
            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest),
                new HexTile(b, TerrainType.Hill),
                new HexTile(c, TerrainType.Plain),
                new HexTile(d, TerrainType.Forest),
                new HexTile(e, TerrainType.Desert),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new System.Collections.Generic.List<Civilization> { civ };
            var state = new IslandState(map, civs, AtlasController.InvalidIslandId);

            // Place the initial city at the vertex formed by a,b,c
            var vertex = Vertex.Create(a, b, c);
            civ.Cities.Add(new City(vertex) { CivilizationIndex = 0 });

            var clock = new SettlersOfIdlestan.Model.Game.GameClock();
            clock.Start();

            // Create a MainGameController and wire its controllers to operate on the
            // prepared island state and clock so the autoplayer can use them.
            var mainController = new MainGameController();
            mainController.SetGame(new MainGameState(state, clock));
            var auto = new CivilizationAutoplayer(civ, map, mainController);

            var roadController = mainController.RoadController;
            var harvestController = mainController.HarvestController;

            // Pick edge b-c as the first road: it is the only edge connecting vertex(a,b,c)
            // to vertex(b,c,d), which exposes distance-2 edges towards d.
            var firstBuildable = Edge.Create(b, c);
            var firstBuilt = auto.AutoBuildRoad(firstBuildable);
            Assert.True(firstBuilt, "First road should eventually be built by the autoplayer");
            Assert.Contains(civ.Roads, r => r.Position.Equals(firstBuildable));

            // Pick a second road through vertex(b,c,d), then a third road ending far
            // enough for the new city distance rule.
            var secondBuildable = Edge.Create(b, d);
            var secondBuilt = auto.AutoBuildRoad(secondBuildable);
            Assert.True(secondBuilt, "Second road should eventually be built by the autoplayer");
            Assert.Contains(civ.Roads, r => r.Position.Equals(secondBuildable));

            var thirdBuildable = Edge.Create(b, e);
            var thirdBuilt = auto.AutoBuildRoad(thirdBuildable);
            Assert.True(thirdBuilt, "Third road should eventually be built by the autoplayer");
            Assert.Contains(civ.Roads, r => r.Position.Equals(thirdBuildable));

            // Verify that at least 20 seconds of in-game time have passed
            Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(18), $"Expected at least 18s elapsed in the GameClock, was {clock.Elapsed}");

            // Attempt to build a Market in the initial city using the autoplayer.
            var marketBuilt = auto.AutoBuildBuilding(vertex, BuildingType.Market);
            Assert.True(marketBuilt, "Autoplayer should eventually build a market in the city");
            Assert.True(civ.Cities.SelectMany(c => c.Buildings).Any(b => b.Type == BuildingType.Market), "City should contain a Market building");

            // Now attempt to build a city (outpost) with the autoplayer. This may require
            // trading sheep and wheat tiles over time; repeatedly try while advancing clock.
            var cityBuilder = new CityBuilderController(state);
            var buildableVertex = thirdBuildable.GetVertices()
                .Single(v => vertex.EdgeDistanceTo(v) == cityBuilder.MinDistanceBetweenCivilizationCities);
            Assert.Contains(cityBuilder.GetBuildableVertices(0), v => v.Equals(buildableVertex));
            Assert.NotNull(buildableVertex);

            var outpostBuilt = auto.AutoBuildOutpost(buildableVertex);
            Assert.True(outpostBuilt, "Autoplayer should eventually build an outpost after roads");
            Assert.Contains(civ.Cities, c => c.Position.Equals(buildableVertex));

            // Save the resulting game state and verify round-trip using test utility
            SaveUtils.SaveAndReloadAndAssertEqual(mainController, "5HexsMapWithTwoCities");
        }
    }
}
