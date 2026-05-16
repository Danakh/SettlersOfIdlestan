using Xunit;
using SettlersOfIdlestan.Model.HexGrid;

namespace SOITests.HexGridTests;

public class HexTests
{
    [Fact]
    public void Constructor_SetsCoord()
    {
        var coord = new HexCoord(1, 2);
        var hex = new Hex(coord);
        Assert.Equal(coord, hex.Coord);
    }

    [Fact]
    public void Equals_ReturnsTrueForSameCoord()
    {
        var coord = new HexCoord(1, 2);
        var hex1 = new Hex(coord);
        var hex2 = new Hex(coord);
        Assert.True(hex1.Equals(hex2));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentCoord()
    {
        var coord1 = new HexCoord(1, 2);
        var coord2 = new HexCoord(2, 3);
        var hex1 = new Hex(coord1);
        var hex2 = new Hex(coord2);
        Assert.False(hex1.Equals(hex2));
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        var coord = new HexCoord(1, 2);
        var hex = new Hex(coord);
        Assert.Equal("Hex((1, 2))", hex.ToString());
    }

    [Fact]
    public void Serialize_ReturnsCorrectArray()
    {
        var coord = new HexCoord(1, 2);
        var hex = new Hex(coord);
        var serialized = hex.Serialize();
        Assert.Equal(new[] { 1, 2 }, serialized);
    }

    [Fact]
    public void Deserialize_ReturnsCorrectHex()
    {
        var data = new[] { 1, 2 };
        var hex = Hex.Deserialize(data);
        Assert.Equal(1, hex.Coord.Q);
        Assert.Equal(2, hex.Coord.R);
    }

    [Fact]
    public void Neighbor_ReturnsCorrectNeighbor()
    {
        var coord = new HexCoord(0, 0);
        var hex = new Hex(coord);
        var neighbor = hex.Neighbor(HexDirection.E);
        Assert.Equal(new HexCoord(1, 0), neighbor);
    }

    [Fact]
    public void Neighbors_ReturnsAllNeighbors()
    {
        var coord = new HexCoord(0, 0);
        var hex = new Hex(coord);
        var neighbors = hex.Neighbors();
        Assert.Equal(6, neighbors.Length);
    }

    [Fact]
    public void GetEdgeByMainDirection_ReturnsCorrectEdge()
    {
        var coord = new HexCoord(0, 0);
        var hex = new Hex(coord);
        var edge = hex.GetEdgeByMainDirection(HexDirection.E);
        Assert.Equal(coord, edge.Hex1);
        Assert.Equal(new HexCoord(1, 0), edge.Hex2);
    }

    [Fact]
    public void GetVertexBySecondaryDirection_ReturnsCorrectVertex()
    {
        var coord = new HexCoord(0, 0);
        var hex = new Hex(coord);
        var vertex = hex.GetVertexBySecondaryDirection(SecondaryHexDirection.N);
        Assert.Equal(new HexCoord(-1, 1), vertex.Hex1);
        Assert.Equal(new HexCoord(0, 0), vertex.Hex2);
        Assert.Equal(new HexCoord(0, 1), vertex.Hex3);
    }
}