using System.Collections.Generic;
using System.Linq;
using Xunit;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SOITests.HexGridTests;

public class RoadPathfinderTests
{
    private const int Z = IslandMap.SurfaceLayer;

    private static readonly HexCoord Center = new(0, 0, Z);

    // Les six sommets d'un hexagone, dans l'ordre cyclique : chaque paire consecutive est adjacente.
    private static readonly SecondaryHexDirection[] Ring =
    [
        SecondaryHexDirection.N,
        SecondaryHexDirection.EN,
        SecondaryHexDirection.ES,
        SecondaryHexDirection.S,
        SecondaryHexDirection.WS,
        SecondaryHexDirection.WN,
    ];

    private static Vertex RingVertex(int i) => Center.Vertex(Ring[i % 6]);

    private static Dictionary<Vertex, List<Vertex>> GraphOf(params (Vertex a, Vertex b)[] segments)
    {
        var roads = segments.Select(s => new Road(Edge.Between(s.a, s.b)!)).ToList();
        return RoadPathfinder.BuildAdjacency(roads, Z);
    }

    /// <summary>Chaine de vertices adjacents de longueur donnee (en segments).</summary>
    private static List<Vertex> Chain(int segments)
    {
        var chain = new List<Vertex> { RingVertex(0) };
        for (int i = 0; i < segments; i++)
        {
            var last = chain[^1];
            // On avance toujours vers le voisin le plus eloigne du depart : trajet rectiligne.
            chain.Add(last.GetAdjacentVertices()
                .Where(v => !chain.Contains(v))
                .OrderByDescending(v => v.EdgeDistanceTo(chain[0]))
                .First());
        }
        return chain;
    }

    private static Dictionary<Vertex, List<Vertex>> GraphOfChain(List<Vertex> chain)
        => GraphOf(chain.Zip(chain.Skip(1)).ToArray());

    [Fact]
    public void FindPathInGraph_SameVertex_ReturnsSingleVertex()
    {
        var path = RoadPathfinder.FindPathInGraph(GraphOf(), RingVertex(0), RingVertex(0));

        Assert.NotNull(path);
        Assert.Single(path);
    }

    [Fact]
    public void FindPathInGraph_AlongChain_ReturnsShortestPath()
    {
        var chain = Chain(4);
        var path = RoadPathfinder.FindPathInGraph(GraphOfChain(chain), chain[0], chain[^1]);

        Assert.NotNull(path);
        Assert.Equal(chain, path);
    }

    [Fact]
    public void FindPathInGraph_NoRoad_ReturnsNull()
    {
        var chain = Chain(2);
        // Une seule route : le dernier vertex de la chaine reste isole.
        var graph = GraphOf((chain[0], chain[1]));

        Assert.Null(RoadPathfinder.FindPathInGraph(graph, chain[0], chain[2]));
    }

    [Theory]
    [InlineData(4, true)]   // portee exactement egale a la longueur du chemin
    [InlineData(5, true)]
    [InlineData(3, false)]  // chemin trop long pour la portee
    [InlineData(0, false)]
    public void FindPathInGraph_RespectsMaxDepth(int maxDepth, bool expectPath)
    {
        var chain = Chain(4);
        var path = RoadPathfinder.FindPathInGraph(GraphOfChain(chain), chain[0], chain[^1], maxDepth);

        Assert.Equal(expectPath, path != null);
        if (expectPath) Assert.Equal(5, path!.Count);
    }

    /// <summary>
    /// L'elagage par distance geometrique doit rester admissible : ici la cible est adjacente
    /// (distance 1) mais la seule route disponible fait le tour de l'hexagone (5 segments).
    /// </summary>
    [Fact]
    public void FindPathInGraph_DetourLongerThanGeometricDistance_IsStillFound()
    {
        var segments = Enumerable.Range(0, 5).Select(i => (RingVertex(i), RingVertex(i + 1))).ToArray();
        var graph = GraphOf(segments);

        var from = RingVertex(5);
        var to = RingVertex(0);
        Assert.Equal(1, from.EdgeDistanceTo(to));

        var path = RoadPathfinder.FindPathInGraph(graph, from, to, maxDepth: 5);
        Assert.NotNull(path);
        Assert.Equal(6, path.Count);

        Assert.Null(RoadPathfinder.FindPathInGraph(graph, from, to, maxDepth: 4));
    }

    [Fact]
    public void HasPathInGraph_MatchesFindPathInGraph()
    {
        var chain = Chain(4);
        var graph = GraphOfChain(chain);

        Assert.True(RoadPathfinder.HasPathInGraph(graph, chain[0], chain[0]));
        Assert.True(RoadPathfinder.HasPathInGraph(graph, chain[0], chain[^1]));
        Assert.True(RoadPathfinder.HasPathInGraph(graph, chain[0], chain[^1], maxDepth: 4));
        Assert.False(RoadPathfinder.HasPathInGraph(graph, chain[0], chain[^1], maxDepth: 3));
        Assert.False(RoadPathfinder.HasPathInGraph(graph, chain[0], Chain(6)[^1]));
    }

    [Fact]
    public void ReachableWithin_ReturnsVerticesUpToDepth()
    {
        var chain = Chain(4);
        var graph = GraphOfChain(chain);

        var reachable = RoadPathfinder.ReachableWithin(graph, chain[0], 2);

        Assert.Equal(3, reachable.Count);
        Assert.Contains(chain[0], reachable);
        Assert.Contains(chain[2], reachable);
        Assert.DoesNotContain(chain[3], reachable);

        Assert.Equal(5, RoadPathfinder.ReachableWithin(graph, chain[0], 10).Count);
        Assert.Single(RoadPathfinder.ReachableWithin(graph, chain[0], 0));
    }

    [Fact]
    public void FindPathInGraph_TargetOnAnotherLayer_ReturnsNull()
    {
        var chain = Chain(2);
        var graph = GraphOfChain(chain);

        var underworld = new HexCoord(0, 0, LayerState.UnderworldZ).Vertex(SecondaryHexDirection.N);

        Assert.Null(RoadPathfinder.FindPathInGraph(graph, chain[0], underworld));
        Assert.False(RoadPathfinder.HasPathInGraph(graph, chain[0], underworld, maxDepth: 5));
    }
}
