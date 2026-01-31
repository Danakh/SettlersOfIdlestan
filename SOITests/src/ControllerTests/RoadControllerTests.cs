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
}
