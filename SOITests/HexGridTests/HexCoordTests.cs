using Xunit;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;

namespace SOITests.HexGridTests;

public class HexCoordTests
{
    [Fact]
    public void Constructor_SetsQAndR()
    {
        var coord = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        Assert.Equal(1, coord.Q);
        Assert.Equal(2, coord.R);
        Assert.Equal(IslandMap.SurfaceLayer, coord.Z);
    }

    [Fact]
    public void Constructor_WithZ_SetsLayer()
    {
        var coord = new HexCoord(1, 2, LayerState.UnderworldZ);
        Assert.Equal(LayerState.UnderworldZ, coord.Z);
    }

    [Fact]
    public void S_ReturnsCorrectDerivedCoordinate()
    {
        var coord = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        Assert.Equal(-3, coord.S);
    }

    [Theory]
    [InlineData(HexDirection.W, -1, 0)]
    [InlineData(HexDirection.E, 1, 0)]
    [InlineData(HexDirection.NE, 0, 1)]
    [InlineData(HexDirection.SE, 1, -1)]
    [InlineData(HexDirection.NW, -1, 1)]
    [InlineData(HexDirection.SW, 0, -1)]
    public void Neighbor_ReturnsCorrectNeighbor(HexDirection direction, int expectedQ, int expectedR)
    {
        var coord = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var neighbor = coord.Neighbor(direction);
        Assert.Equal(expectedQ, neighbor.Q);
        Assert.Equal(expectedR, neighbor.R);
        Assert.Equal(coord.Z, neighbor.Z);
    }

    [Fact]
    public void Neighbors_ReturnsAllNeighbors()
    {
        var coord = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var neighbors = coord.Neighbors();
        Assert.Equal(6, neighbors.Length);
        // Check specific neighbors
        Assert.Contains(neighbors, n => n.Q == -1 && n.R == 0); // W
        Assert.Contains(neighbors, n => n.Q == 1 && n.R == 0);  // E
        Assert.Contains(neighbors, n => n.Q == 0 && n.R == 1);  // NE
        Assert.Contains(neighbors, n => n.Q == 1 && n.R == -1); // SE
        Assert.Contains(neighbors, n => n.Q == -1 && n.R == 1); // NW
        Assert.Contains(neighbors, n => n.Q == 0 && n.R == -1); // SW
    }

    [Fact]
    public void DistanceTo_CalculatesCorrectDistance()
    {
        var coord1 = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var coord2 = new HexCoord(1, 1, IslandMap.SurfaceLayer);
        var distance = coord1.DistanceTo(coord2);
        Assert.Equal(2, distance);
    }

    [Fact]
    public void DistanceTo_WithDifferentZ_ThrowsArgumentException()
    {
        var surface = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var underworld = new HexCoord(0, 0, LayerState.UnderworldZ);

        Assert.Throws<ArgumentException>(() => surface.DistanceTo(underworld));
    }

    [Fact]
    public void Equals_ReturnsTrueForSameCoordinates()
    {
        var coord1 = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        var coord2 = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        Assert.True(coord1.Equals(coord2));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentZ()
    {
        var coord1 = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        var coord2 = new HexCoord(1, 2, LayerState.UnderworldZ);
        Assert.False(coord1.Equals(coord2));
    }

    [Fact]
    public void Equals_ReturnsFalseForDifferentCoordinates()
    {
        var coord1 = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        var coord2 = new HexCoord(2, 3, IslandMap.SurfaceLayer);
        Assert.False(coord1.Equals(coord2));
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        var coord = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        Assert.Equal("(1, 2, z=0)", coord.ToString());
    }

    [Fact]
    public void Serialize_ReturnsCorrectArray()
    {
        var coord = new HexCoord(1, 2, IslandMap.SurfaceLayer);
        var serialized = coord.Serialize();
        Assert.Equal(new[] { 1, 2, IslandMap.SurfaceLayer }, serialized);
    }

    [Fact]
    public void Serialize_WithZ_ReturnsThreeElementArray()
    {
        var coord = new HexCoord(1, 2, LayerState.UnderworldZ);
        var serialized = coord.Serialize();
        Assert.Equal(new[] { 1, 2, LayerState.UnderworldZ }, serialized);
    }

    [Fact]
    public void Deserialize_ReturnsCorrectHexCoord()
    {
        var data = new[] { 1, 2 };
        var coord = HexCoord.Deserialize(data);
        Assert.Equal(1, coord.Q);
        Assert.Equal(2, coord.R);
        Assert.Equal(IslandMap.SurfaceLayer, coord.Z);
    }

    [Fact]
    public void Deserialize_WithZ_ReturnsLayeredHexCoord()
    {
        var data = new[] { 1, 2, LayerState.UnderworldZ };
        var coord = HexCoord.Deserialize(data);
        Assert.Equal(1, coord.Q);
        Assert.Equal(2, coord.R);
        Assert.Equal(LayerState.UnderworldZ, coord.Z);
    }
}
