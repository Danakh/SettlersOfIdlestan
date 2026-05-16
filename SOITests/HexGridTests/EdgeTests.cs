using Xunit;
using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Linq;

namespace SOITests.HexGridTests;

public class EdgeTests
{
    [Fact]
    public void Create_ReturnsEdgeWithNormalizedOrder()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge = Edge.Create(hex1, hex2);
        Assert.Equal(hex1, edge.Hex1);
        Assert.Equal(hex2, edge.Hex2);
    }

    [Fact]
    public void Create_WithReversedOrder_Normalizes()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge1 = Edge.Create(hex1, hex2);
        var edge2 = Edge.Create(hex2, hex1);
        Assert.True(edge1.Equals(edge2));
    }

    [Fact]
    public void Create_WithNonAdjacentHexes_ThrowsArgumentException()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(2, 0); // Distance 2
        Assert.Throws<ArgumentException>(() => Edge.Create(hex1, hex2));
    }

    [Fact]
    public void Equals_ReturnsTrueForSameEdges()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge1 = Edge.Create(hex1, hex2);
        var edge2 = Edge.Create(hex1, hex2);
        Assert.True(edge1.Equals(edge2));
    }

    [Fact]
    public void IsAdjacentTo_ReturnsTrueForConnectedHex()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge = Edge.Create(hex1, hex2);
        Assert.True(edge.IsAdjacentTo(hex1));
        Assert.True(edge.IsAdjacentTo(hex2));
    }

    [Fact]
    public void IsAdjacentTo_ReturnsFalseForUnconnectedHex()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var edge = Edge.Create(hex1, hex2);
        Assert.False(edge.IsAdjacentTo(hex3));
    }

    [Fact]
    public void OtherHex_ReturnsCorrectOtherHex()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge = Edge.Create(hex1, hex2);
        Assert.Equal(hex2, edge.OtherHex(hex1));
        Assert.Equal(hex1, edge.OtherHex(hex2));
    }

    [Fact]
    public void OtherHex_WithUnconnectedHex_ThrowsArgumentException()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var hex3 = new HexCoord(0, 1);
        var edge = Edge.Create(hex1, hex2);
        Assert.Throws<ArgumentException>(() => edge.OtherHex(hex3));
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge = Edge.Create(hex1, hex2);
        Assert.Equal("Edge((0, 0) - (1, 0))", edge.ToString());
    }

    [Fact]
    public void Serialize_ReturnsCorrectArray()
    {
        var hex1 = new HexCoord(0, 0);
        var hex2 = new HexCoord(1, 0);
        var edge = Edge.Create(hex1, hex2);
        var serialized = edge.Serialize();
        Assert.Equal(new[] { new[] { 0, 0 }, new[] { 1, 0 } }, serialized);
    }

    [Fact]
    public void Deserialize_ReturnsCorrectEdge()
    {
        var data = new[] { new[] { 0, 0 }, new[] { 1, 0 } };
        var edge = Edge.Deserialize(data);
        Assert.Equal(new HexCoord(0, 0), edge.Hex1);
        Assert.Equal(new HexCoord(1, 0), edge.Hex2);
    }

    [Fact]
    public void GetNeighboringEdges_ReturnsFourEdges()
    {
        var h1 = new HexCoord(0, 0);
        var h2 = new HexCoord(1, 0);
        var edge = Edge.Create(h1, h2);

        var neighbors = edge.GetNeighboringEdges();

        Assert.Equal(4, neighbors.Length);
    }

    [Fact]
    public void GetNeighboringEdges_ReturnsCorrectEdges()
    {
        // Edge(0,0)-(1,0) : voisins communs de (0,0) et (1,0) sont (0,1) et (1,-1)
        var h1 = new HexCoord(0, 0);
        var h2 = new HexCoord(1, 0);
        var h3 = new HexCoord(0, 1);   // premier tiers-hex
        var h4 = new HexCoord(1, -1);  // deuxième tiers-hex

        var edge = Edge.Create(h1, h2);
        var neighbors = edge.GetNeighboringEdges();

        Assert.Contains(neighbors, e => e.Equals(Edge.Create(h1, h3)));
        Assert.Contains(neighbors, e => e.Equals(Edge.Create(h2, h3)));
        Assert.Contains(neighbors, e => e.Equals(Edge.Create(h1, h4)));
        Assert.Contains(neighbors, e => e.Equals(Edge.Create(h2, h4)));
    }

    [Fact]
    public void GetNeighboringEdges_DoesNotContainSelf()
    {
        var h1 = new HexCoord(0, 0);
        var h2 = new HexCoord(1, 0);
        var edge = Edge.Create(h1, h2);

        var neighbors = edge.GetNeighboringEdges();

        Assert.DoesNotContain(neighbors, e => e.Equals(edge));
    }

    [Fact]
    public void GetNeighboringEdges_EachNeighborSharesExactlyOneVertex()
    {
        var h1 = new HexCoord(0, 0);
        var h2 = new HexCoord(1, 0);
        var edge = Edge.Create(h1, h2);
        var edgeVertices = edge.GetVertices();

        foreach (var neighbor in edge.GetNeighboringEdges())
        {
            var neighborVertices = neighbor.GetVertices();
            var sharedCount = edgeVertices.Count(ev => neighborVertices.Any(nv => nv.Equals(ev)));
            Assert.Equal(1, sharedCount);
        }
    }
}