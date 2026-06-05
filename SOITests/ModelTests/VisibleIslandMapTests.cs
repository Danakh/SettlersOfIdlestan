using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.ModelTests;

public class VisibleIslandMapTests
{
    [Fact]
    public void Constructor_WithCity_ExposesHexesTouchingCity()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var hidden = new HexCoord(2, 2, IslandMap.SurfaceLayer);
        var map = CreateMap(a, b, c, hidden);
        var civilization = new Civilization();
        civilization.AddCity(new City(Vertex.Create(a, b, c)));

        var visibleMap = new VisibleIslandMap(map, civilization);

        AssertVisibleHexes(visibleMap, a, b, c);
        Assert.False(visibleMap.HasTile(hidden));
    }

    [Fact]
    public void Constructor_WithRoad_ExposesHexesTouchingRoadEndpoints()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var endOnly1 = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var endOnly2 = new HexCoord(1, -1, IslandMap.SurfaceLayer);
        var hidden = new HexCoord(3, 3, IslandMap.SurfaceLayer);
        var map = CreateMap(a, b, endOnly1, endOnly2, hidden);
        var civilization = new Civilization();
        civilization.Roads.Add(new Road(Edge.Create(a, b)));

        var visibleMap = new VisibleIslandMap(map, civilization);

        AssertVisibleHexes(visibleMap, a, b, endOnly1, endOnly2);
        Assert.False(visibleMap.HasTile(hidden));
    }

    [Fact]
    public void Constructor_IgnoresVisibleHexesThatAreOutsideSourceMap()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var map = CreateMap(a);
        var civilization = new Civilization();
        civilization.AddCity(new City(Vertex.Create(a, b, c)));

        var visibleMap = new VisibleIslandMap(map, civilization);

        AssertVisibleHexes(visibleMap, a);
    }

    [Fact]
    public void Constructor_WithNoCitiesOrRoads_ExposesNoHexes()
    {
        var map = CreateMap(new HexCoord(0, 0, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer));
        var civilization = new Civilization();

        var visibleMap = new VisibleIslandMap(map, civilization);

        Assert.Empty(visibleMap.Tiles);
    }

    private static IslandMap CreateMap(params HexCoord[] coords)
    {
        return new IslandMap(coords.Select(coord => new HexTile(coord, TerrainType.Plain)));
    }

    private static void AssertVisibleHexes(VisibleIslandMap map, params HexCoord[] expectedCoords)
    {
        Assert.Equal(expectedCoords.Length, map.Tiles.Count);
        foreach (var coord in expectedCoords)
        {
            Assert.True(map.HasTile(coord), $"Expected {coord} to be visible.");
        }
    }
}
