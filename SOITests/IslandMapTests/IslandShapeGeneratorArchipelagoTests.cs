using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.IslandMapTests;

public class IslandShapeGeneratorArchipelagoTests
{
    /// <summary>
    /// Compte les edges maritimes valides dans une forme d'archipel.
    /// Un edge maritime valide est un edge entre deux hexes eau dont les deux
    /// vertices touchent chacun au moins un hex terre.
    /// </summary>
    private static int CountValidMaritimeEdges(IReadOnlyList<HexCoord> landCoords)
    {
        var landSet = new HashSet<HexCoord>(landCoords);

        // Tous les hexes eau adjacents à au moins une tuile terre
        var waterBoundary = new HashSet<HexCoord>();
        foreach (var land in landSet)
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = land.Neighbor(dir);
                if (!landSet.Contains(nb))
                    waterBoundary.Add(nb);
            }

        // Edges candidates : paires de hexes eau adjacents, tous deux en waterBoundary
        var seen = new HashSet<(int, int, int, int)>();
        int count = 0;

        foreach (var w1 in waterBoundary)
        {
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var w2 = w1.Neighbor(dir);
                if (!waterBoundary.Contains(w2)) continue;

                // Déduplique l'edge (w1,w2) == (w2,w1)
                int minQ = w1.Q < w2.Q || (w1.Q == w2.Q && w1.R < w2.R) ? w1.Q : w2.Q;
                int minR = w1.Q < w2.Q || (w1.Q == w2.Q && w1.R < w2.R) ? w1.R : w2.R;
                int maxQ = minQ == w1.Q && minR == w1.R ? w2.Q : w1.Q;
                int maxR = minQ == w1.Q && minR == w1.R ? w2.R : w1.R;
                var key = (minQ, minR, maxQ, maxR);
                if (!seen.Add(key)) continue;

                var edge = Edge.Create(w1, w2);
                var vertices = edge.GetVertices();
                if (vertices.Length != 2) continue;

                bool bothVertexTouchLand = vertices.All(v =>
                    v.GetHexes().Any(h => landSet.Contains(h)));

                if (bothVertexTouchLand)
                    count++;
            }
        }

        return count;
    }

    public static IEnumerable<object[]> SeedsAndCounts()
    {
        yield return new object[] { 1,   30 };
        yield return new object[] { 42,  30 };
        yield return new object[] { 100, 40 };
        yield return new object[] { 999, 50 };
        yield return new object[] { 12345, 60 };
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_HasAtLeastTwoValidMaritimeEdgesPerPassage(int seed, int count)
    {
        var prng = new GamePRNG(seed);
        var generator = new IslandShapeGeneratorArchipelago(prng);
        var coords = generator.GenerateCoords(count);

        int numIslands = Math.Max(2, count / 15);
        int numPassages = numIslands - 1;
        int maritimeCount = CountValidMaritimeEdges(coords);

        Assert.True(maritimeCount >= numPassages * 2,
            $"Seed={seed}, count={count}: {maritimeCount} route(s) maritime(s), attendu >= {numPassages * 2} ({numIslands} îles, {numPassages} passages).");
    }

    [Fact]
    public void GenerateCoords_WithoutSeed_HasAtLeastTwoValidMaritimeEdges()
    {
        var prng = new GamePRNG();
        var generator = new IslandShapeGeneratorArchipelago(prng);
        var coords = generator.GenerateCoords(30);

        int maritimeCount = CountValidMaritimeEdges(coords);

        Assert.True(maritimeCount >= 2,
            $"Sans seed: seulement {maritimeCount} route(s) maritime(s) valide(s) trouvée(s), minimum 2 attendu.");
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_ProducesExpectedNumberOfIslands(int seed, int count)
    {
        var prng = new GamePRNG(seed);
        var generator = new IslandShapeGeneratorArchipelago(prng);
        var coords = generator.GenerateCoords(count);
        var landSet = new HashSet<HexCoord>(coords);

        int expectedComponents = Math.Max(2, count / 15);
        int components = CountConnectedComponents(landSet);

        Assert.Equal(expectedComponents, components);
    }

    private static int CountConnectedComponents(HashSet<HexCoord> landSet)
    {
        var visited = new HashSet<HexCoord>();
        int components = 0;

        foreach (var start in landSet)
        {
            if (visited.Contains(start)) continue;
            components++;
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    var nb = current.Neighbor(dir);
                    if (landSet.Contains(nb) && visited.Add(nb))
                        queue.Enqueue(nb);
                }
            }
        }

        return components;
    }
}
