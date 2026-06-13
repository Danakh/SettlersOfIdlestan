using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    public class AutoplayerTests
    {
        [Fact]
        public void Autoplayer_BuildingASecondCity()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
            var d = new HexCoord(1, 1, IslandMap.SurfaceLayer);
            var e = new HexCoord(2, 0, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest),
                new HexTile(b, TerrainType.Hill),
                new HexTile(c, TerrainType.Plain),
                new HexTile(d, TerrainType.Forest),
                new HexTile(e, TerrainType.Hill),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new System.Collections.Generic.List<Civilization> { civ };
            var state = new WorldState(map, civs, AtlasController.InvalidIslandId);

            var vertex = Vertex.Create(a, b, c);
            IslandMapGenerator generator = new IslandMapGenerator(new GamePRNG(42));
            generator.PopulatePlayerCivilization(map, civ, vertex);

            var clock = new GameClock();
            clock.Start();

            var mainController = new MainGameController();
            mainController.SetGame(new MainGameState(state, clock, new GamePRNG(42)));

            var auto = new CivilizationAutoplayer(
                civ, map,
                mainController.RoadController,
                mainController.HarvestController,
                mainController.BuildingController,
                mainController.CityBuilderController,
                mainController.TradeController,
                mainController.ResearchController,
                mainController.PrestigeController,
                mainController.PrestigeMapController,
                state,
                mainController.CurrentMainState?.PrestigeState,
                mainController.PerformPrestige);
            var runner = new CivilizationAutoplayerRunner(auto, civ, mainController);

            var firstBuildable = Edge.Create(b, c);
            Assert.True(runner.AutoBuildRoad(firstBuildable), "First road should eventually be built");
            Assert.Contains(civ.Roads, r => r.Position.Equals(firstBuildable));

            var secondBuildable = Edge.Create(b, d);
            Assert.True(runner.AutoBuildRoad(secondBuildable), "Second road should eventually be built");
            Assert.Contains(civ.Roads, r => r.Position.Equals(secondBuildable));

            var thirdBuildable = Edge.Create(b, e);
            Assert.True(runner.AutoBuildRoad(thirdBuildable), "Third road should eventually be built");
            Assert.Contains(civ.Roads, r => r.Position.Equals(thirdBuildable));

            Assert.True(clock.CurrentTick >= 1800, $"Expected at least 1800 ticks elapsed, was {clock.CurrentTick}");

            City city = civ.Cities.First(c => c.Position.Equals(vertex));
            Assert.True(runner.AutoBuildBuilding(city, BuildingType.Market), "Autoplayer should eventually build a market");
            Assert.Contains(civ.Cities.SelectMany(c => c.Buildings), b => b.Type == BuildingType.Market);

            var cityBuilder = new CityBuilderController(state);
            var buildableVertex = thirdBuildable.GetVertices()
                .Single(v => vertex.EdgeDistanceTo(v) == cityBuilder.MinDistanceBetweenCivilizationCities);
            Assert.Contains(cityBuilder.GetBuildableVertices(0), v => v.Equals(buildableVertex));

            Assert.True(runner.AutoBuildOutpost(buildableVertex), "Autoplayer should eventually build an outpost");
            Assert.Contains(civ.Cities, c => c.Position.Equals(buildableVertex));

            SaveUtils.SaveAndReloadAndAssertEqual(mainController, "5HexsMapWithTwoCities");
        }
    }
}
