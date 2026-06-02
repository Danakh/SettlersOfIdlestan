using System.Linq;
using Xunit;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;

namespace SOITests.HexGridTests;

public class HexGridPathfinderTests
{
    // Vertex bien connu : (0,0)-(0,1)-(1,0)
    private static readonly Vertex V0 = Vertex.Create(new HexCoord(0, 0, IslandMap.SurfaceLayer), new HexCoord(0, 1, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer));

    [Fact]
    public void FindVertexPath_SameVertex_ReturnsLengthOne()
    {
        var path = HexGridPathfinder.FindVertexPath(V0, V0);
        Assert.Single(path);
        Assert.Equal(V0, path[0]);
    }

    [Fact]
    public void FindVertexPath_AdjacentVertex_ReturnsLengthTwo()
    {
        var neighbor = V0.GetAdjacentVertices()[0];
        Assert.Equal(1, V0.EdgeDistanceTo(neighbor));

        var path = HexGridPathfinder.FindVertexPath(V0, neighbor);

        Assert.Equal(2, path.Count);
        Assert.Equal(V0, path[0]);
        Assert.Equal(neighbor, path[^1]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void FindVertexPath_LengthEqualsEdgeDistancePlusOne(int targetEdgeDistance)
    {
        var target = WalkToDistance(V0, targetEdgeDistance);
        int edgeDist = V0.EdgeDistanceTo(target);

        var path = HexGridPathfinder.FindVertexPath(V0, target);

        Assert.Equal(edgeDist + 1, path.Count);
    }

    [Fact]
    public void FindVertexPath_ThreeEdges_ReturnsFourVertices()
    {
        var target = WalkToDistance(V0, 3);
        Assert.Equal(3, V0.EdgeDistanceTo(target));

        var path = HexGridPathfinder.FindVertexPath(V0, target);

        Assert.Equal(4, path.Count);
    }

    [Fact]
    public void FindVertexPath_StartsAtFromAndEndsAtTo()
    {
        var target = WalkToDistance(V0, 4);
        var path = HexGridPathfinder.FindVertexPath(V0, target);

        Assert.Equal(V0, path[0]);
        Assert.Equal(target, path[^1]);
    }

    [Fact]
    public void FindVertexPath_WithDifferentZ_ThrowsArgumentException()
    {
        var target = Vertex.Create(
            new HexCoord(0, 0, UnderworldState.Layer),
            new HexCoord(0, 1, UnderworldState.Layer),
            new HexCoord(1, 0, UnderworldState.Layer));

        Assert.Throws<ArgumentException>(() => HexGridPathfinder.FindVertexPath(V0, target));
    }

    [Fact]
    public void FindVertexPath_EachStepIsAdjacentVertex()
    {
        var target = WalkToDistance(V0, 5);
        var path = HexGridPathfinder.FindVertexPath(V0, target);

        for (int i = 0; i < path.Count - 1; i++)
        {
            Assert.Equal(1, path[i].EdgeDistanceTo(path[i + 1]));
        }
    }

    /// <summary>
    /// Marche n pas depuis start en choisissant toujours le premier voisin qui augmente
    /// la distance au point de dÃ©part. Produit un sommet Ã  distance exactement n.
    /// </summary>
    private static Vertex WalkToDistance(Vertex start, int steps)
    {
        var current = start;
        for (int i = 0; i < steps; i++)
        {
            // Choisir un voisin Ã  distance i+1 depuis start
            current = current.GetAdjacentVertices()
                .First(v => start.EdgeDistanceTo(v) == i + 1);
        }
        return current;
    }
}
