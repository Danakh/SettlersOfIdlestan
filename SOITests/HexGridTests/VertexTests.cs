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
    public void Create_WithDifferentZ_ThrowsArgumentException()
    {
        var hex1 = new HexCoord(0, 0, HexCoord.SurfaceZ);
        var hex2 = new HexCoord(1, 0, HexCoord.SurfaceZ);
        var hex3 = new HexCoord(0, 1, HexCoord.UnderworldZ);
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
        Assert.Equal("Vertex((0, 0, z=0), (0, 1, z=0), (1, 0, z=0))", vertex.ToString());
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
        Assert.Contains(new[] { 0, 0, 0 }, serialized);
        Assert.Contains(new[] { 1, 0, 0 }, serialized);
        Assert.Contains(new[] { 0, 1, 0 }, serialized);
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

    [Fact]
    public void Deserialize_WithZ_ReturnsLayeredVertex()
    {
        var data = new[] { new[] { 0, 0, 1 }, new[] { 1, 0, 1 }, new[] { 0, 1, 1 } };
        var vertex = Vertex.Deserialize(data);
        Assert.Equal(HexCoord.UnderworldZ, vertex.Z);
        Assert.Equal(new HexCoord(0, 0, 1), vertex.Hex1);
        Assert.Equal(new HexCoord(0, 1, 1), vertex.Hex2);
        Assert.Equal(new HexCoord(1, 0, 1), vertex.Hex3);
    }

    [Fact]
    public void EdgeDistanceTo_WithDifferentZ_ThrowsArgumentException()
    {
        var surface = Vertex.Create(new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(0, 1));
        var underworld = Vertex.Create(
            new HexCoord(0, 0, HexCoord.UnderworldZ),
            new HexCoord(1, 0, HexCoord.UnderworldZ),
            new HexCoord(0, 1, HexCoord.UnderworldZ));

        Assert.Throws<ArgumentException>(() => surface.EdgeDistanceTo(underworld));
    }

    [Fact]
    public void EdgeDistanceTests()
    {
        var hex0 = new HexCoord(0, 0);
        var hexNE = hex0.Neighbor(HexDirection.NE);
        var hexNW = hex0.Neighbor(HexDirection.NW);
        var hexE = hex0.Neighbor(HexDirection.E);
        var hexW = hex0.Neighbor(HexDirection.W);
        var hexSE = hex0.Neighbor(HexDirection.SE);
        var hexSW = hex0.Neighbor(HexDirection.SW);

        var vertexN = hex0.Vertex(SecondaryHexDirection.N);
        var vertexEN = hex0.Vertex(SecondaryHexDirection.EN);
        var vertexES = hex0.Vertex(SecondaryHexDirection.ES);
        var vertexS = hex0.Vertex(SecondaryHexDirection.S);
        var vertexWS = hex0.Vertex(SecondaryHexDirection.WS);
        var vertexWN = hex0.Vertex(SecondaryHexDirection.WN);
        Assert.Equal(0, vertexN.EdgeDistanceTo(vertexN));
        Assert.Equal(1, vertexN.EdgeDistanceTo(vertexEN));
        Assert.Equal(2, vertexN.EdgeDistanceTo(vertexES));
        Assert.Equal(3, vertexN.EdgeDistanceTo(vertexS));
        Assert.Equal(2, vertexN.EdgeDistanceTo(vertexWS));
        Assert.Equal(1, vertexN.EdgeDistanceTo(vertexWN));

        Assert.Equal(4, vertexN.EdgeDistanceTo(hexSW.Vertex(SecondaryHexDirection.ES)));
        Assert.Equal(5, vertexN.EdgeDistanceTo(hexSW.Vertex(SecondaryHexDirection.S)));
        Assert.Equal(4, vertexN.EdgeDistanceTo(hexSW.Vertex(SecondaryHexDirection.WS)));
        Assert.Equal(3, vertexN.EdgeDistanceTo(hexSW.Vertex(SecondaryHexDirection.WN)));

        Assert.Equal(5, vertexEN.EdgeDistanceTo(hexW.Vertex(SecondaryHexDirection.WS)));
        Assert.Equal(5, vertexWN.EdgeDistanceTo(hexE.Vertex(SecondaryHexDirection.ES)));
        Assert.Equal(5, vertexES.EdgeDistanceTo(hexW.Vertex(SecondaryHexDirection.WN)));
        Assert.Equal(5, vertexWS.EdgeDistanceTo(hexE.Vertex(SecondaryHexDirection.EN)));
        Assert.Equal(5, vertexS.EdgeDistanceTo(hexNE.Vertex(SecondaryHexDirection.N)));

        Assert.Equal(7, hexW.Vertex(SecondaryHexDirection.WS).EdgeDistanceTo(hexE.Vertex(SecondaryHexDirection.EN)));
        Assert.Equal(7, hexNW.Vertex(SecondaryHexDirection.WN).EdgeDistanceTo(hexSE.Vertex(SecondaryHexDirection.ES)));
        Assert.Equal(7, hexNE.Vertex(SecondaryHexDirection.N).EdgeDistanceTo(hexSW.Vertex(SecondaryHexDirection.S)));
        Assert.Equal(7, hexE.Vertex(SecondaryHexDirection.EN).EdgeDistanceTo(hexW.Vertex(SecondaryHexDirection.WS)));
        Assert.Equal(7, hexSE.Vertex(SecondaryHexDirection.ES).EdgeDistanceTo(hexNW.Vertex(SecondaryHexDirection.WN)));
        Assert.Equal(7, hexSW.Vertex(SecondaryHexDirection.S).EdgeDistanceTo(hexNE.Vertex(SecondaryHexDirection.N)));
    }
}
