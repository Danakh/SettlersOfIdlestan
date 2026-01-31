using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.Road;
using System.Linq;
using System.Collections.Generic;

namespace SOITests.ControllerTests;

public class RoadControllerTests
{
    [Fact]
    public void GetBuildableRoads_CityEnablesAdjacentEdges()
    {
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Field),
            new HexTile(b, TerrainType.Field),
            new HexTile(c, TerrainType.Field),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs);

        var vertex = Vertex.Create(a, b, c);
        civ.Cities.Add(new City(vertex) { CivilizationIndex = 0 });

        var controller = new RoadController(state);
        var buildable = controller.GetBuildableRoads(0);

        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(a, b)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(a, c)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(b, c)));
    }

    [Fact]
    public void GetBuildableRoads_OccupiedEdgeNotReturned()
    {
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Field),
            new HexTile(b, TerrainType.Field),
            new HexTile(c, TerrainType.Field),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs);

        var vertex = Vertex.Create(a, b, c);
        civ.Cities.Add(new City(vertex) { CivilizationIndex = 0 });

        // Occupy edge a-b
        civ.Roads.Add(new Road(Edge.Create(a, b)) { CivilizationIndex = 0 });

        var controller = new RoadController(state);
        var buildable = controller.GetBuildableRoads(0);

        Assert.DoesNotContain(buildable, r => r.Position.Equals(Edge.Create(a, b)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(a, c)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(b, c)));
    }

    [Fact]
    public void BuildRoad_AdjacentToCity_ConsumesResourcesAndSetsDistance()
    {
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Field),
            new HexTile(b, TerrainType.Field),
            new HexTile(c, TerrainType.Field),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        // give enough resources: 2 wood and 2 brick
        civ.Resources.Add(Resource.Wood);
        civ.Resources.Add(Resource.Wood);
        civ.Resources.Add(Resource.Brick);
        civ.Resources.Add(Resource.Brick);

        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs);

        var vertex = Vertex.Create(a, b, c);
        civ.Cities.Add(new City(vertex) { CivilizationIndex = 0 });

        var controller = new RoadController(state);
        var edge = Edge.Create(a, b);
        var road = controller.BuildRoad(0, edge);

        Assert.Contains(civ.Roads, r => r.Position.Equals(edge));
        Assert.Equal(1, road.DistanceToNearestCity);
        Assert.Equal(0, civ.Resources.Count(r => r == Resource.Wood));
        Assert.Equal(0, civ.Resources.Count(r => r == Resource.Brick));
    }

    [Fact]
    public void BuildRoad_ExtendRoad_CalculatesDistanceAndConsumesCost()
    {
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Field),
            new HexTile(b, TerrainType.Field),
            new HexTile(c, TerrainType.Field),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        // give enough resources for two roads: first costs 2 each, second costs 8 each => total 10 each
        for (int i = 0; i < 10; i++) civ.Resources.Add(Resource.Wood);
        for (int i = 0; i < 10; i++) civ.Resources.Add(Resource.Brick);

        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs);

        var vertex = Vertex.Create(a, b, c);
        civ.Cities.Add(new City(vertex) { CivilizationIndex = 0 });

        var controller = new RoadController(state);
        var e1 = Edge.Create(a, b);
        var e2 = Edge.Create(b, c);

        var r1 = controller.BuildRoad(0, e1);
        Assert.Contains(civ.Roads, r => r.Position.Equals(e1));
        Assert.Equal(1, r1.DistanceToNearestCity);

        var r2 = controller.BuildRoad(0, e2);
        Assert.Contains(civ.Roads, r => r.Position.Equals(e2));
        Assert.Equal(2, r2.DistanceToNearestCity);

        Assert.Equal(0, civ.Resources.Count(r => r == Resource.Wood));
        Assert.Equal(0, civ.Resources.Count(r => r == Resource.Brick));
    }
}
