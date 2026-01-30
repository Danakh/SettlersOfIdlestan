using System;
using Xunit;
using SettlersOfIdlestan.Model.HexGrid;

namespace SOITests.HexGridTests;

public class VertexTests
{
    [Fact]
    public void Create_ReturnsVertexWithNormalizedOrder()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var vertex = Vertex.Create(hex1, hex2, hex3);
        // Assuming normalization sorts by Q then R
        Assert.Equal(new HexCoord(0, 0), vertex.Hex1);
        Assert.Equal(new HexCoord(0, 1), vertex.Hex2);
        Assert.Equal(new HexCoord(1, 0), vertex.Hex3);
    }

    [Fact]
    public void Create_WithInvalidTriangle_ThrowsArgumentException()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(2, 0); // Not adjacent to both
        Assert.Throws<ArgumentException>(() => Vertex.Create(hex1, hex2, hex3));
    }

    [Fact]
    public void Equals_ReturnsTrueForSameVertices()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var vertex1 = Vertex.Create(hex1, hex2, hex3);
        var vertex2 = Vertex.Create(hex1, hex2, hex3);
        Assert.True(vertex1.Equals(vertex2));
    }

    [Fact]
    public void IsAdjacentTo_ReturnsTrueForConnectedHex()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var vertex = Vertex.Create(hex1, hex2, hex3);
        Assert.True(vertex.IsAdjacentTo(hex1));
        Assert.True(vertex.IsAdjacentTo(hex2));
        Assert.True(vertex.IsAdjacentTo(hex3));
    }

    [Fact]
    public void IsAdjacentTo_ReturnsFalseForUnconnectedHex()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var hex4 = new HexCoord(1, 1);
        var vertex = Vertex.Create(hex1, hex2, hex3);
        Assert.False(vertex.IsAdjacentTo(hex4));
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var vertex = Vertex.Create(hex1, hex2, hex3);
        Assert.Equal("Vertex((0, 0), (0, 1), (1, 0))", vertex.ToString());
    }

    [Fact]
    public void Serialize_ReturnsCorrectArray()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var vertex = Vertex.Create(hex1, hex2, hex3);
        var serialized = vertex.Serialize();
        Assert.Equal(3, serialized.Length);
        Assert.Contains(new[] { 0, 0 }, serialized);
        Assert.Contains(new[] { 1, 0 }, serialized);
        Assert.Contains(new[] { 0, 1 }, serialized);
    }

    [Fact]
    public void Deserialize_ReturnsCorrectVertex()
    {
        var data = new[] { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 0, 1 } };
        var vertex = Vertex.Deserialize(data);
        Assert.Equal(new HexCoord(0, 0), vertex.Hex1);
        Assert.Equal(new HexCoord(0, 1), vertex.Hex2);
        Assert.Equal(new HexCoord(1, 0), vertex.Hex3);
    }
}