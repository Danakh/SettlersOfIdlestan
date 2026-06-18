using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.IslandMapTests;

public class IslandShapeGeneratorLakeTests
{
    public static IEnumerable<object[]> SeedsAndCounts()
    {
        yield return new object[] { 1, 65 };
        yield return new object[] { 42, 65 };
        yield return new object[] { 100, 65 };
        yield return new object[] { 999, 65 };
        yield return new object[] { 12345, 50 };
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_ReturnsExactlyRequestedLandCount(int seed, int count)
    {
        var generator = new IslandShapeGeneratorLake(new GamePRNG(seed));
        var coords = generator.GenerateCoords(count);

        Assert.Equal(count, coords.Count);
        Assert.Equal(count, coords.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_HasAnEnclosedLakeOf2To4Hexes(int seed, int count)
    {
        var generator = new IslandShapeGeneratorLake(new GamePRNG(seed));
        var coords = generator.GenerateCoords(count);
        var landSet = new HashSet<HexCoord>(coords);

        var waterBoundary = new HashSet<HexCoord>();
        foreach (var land in landSet)
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = land.Neighbor(dir);
                if (!landSet.Contains(nb))
                    waterBoundary.Add(nb);
            }

        // Une île compacte a une seule composante d'eau extérieure (de grande taille) ;
        // le lac, lui, forme une composante isolée de 2 à 4 hexs (aucun chemin d'eau vers l'extérieur).
        var components = ConnectedComponents(waterBoundary);
        Assert.Contains(components, c => c.Count is >= 2 and <= 4);
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_LakeHasNoHexTriangle(int seed, int count)
    {
        var generator = new IslandShapeGeneratorLake(new GamePRNG(seed));
        var coords = generator.GenerateCoords(count);
        var landSet = new HashSet<HexCoord>(coords);

        var waterBoundary = new HashSet<HexCoord>();
        foreach (var land in landSet)
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = land.Neighbor(dir);
                if (!landSet.Contains(nb))
                    waterBoundary.Add(nb);
            }

        var lake = ConnectedComponents(waterBoundary).FirstOrDefault(c => c.Count is >= 2 and <= 4);

        Assert.NotNull(lake);

        foreach (var a in lake!)
            foreach (var b in lake)
            {
                if (a.Equals(b)) continue;
                bool adjacent = HexDirectionUtils.AllHexDirections.Any(d => a.Neighbor(d).Equals(b));
                if (!adjacent) continue;

                foreach (var c in lake)
                {
                    if (c.Equals(a) || c.Equals(b)) continue;
                    bool aTouchesC = HexDirectionUtils.AllHexDirections.Any(d => a.Neighbor(d).Equals(c));
                    bool bTouchesC = HexDirectionUtils.AllHexDirections.Any(d => b.Neighbor(d).Equals(c));
                    Assert.False(aTouchesC && bTouchesC, $"Lake hexes {a}, {b}, {c} form a triangle.");
                }
            }
    }

    private static List<List<HexCoord>> ConnectedComponents(HashSet<HexCoord> set)
    {
        var visited = new HashSet<HexCoord>();
        var components = new List<List<HexCoord>>();

        foreach (var start in set)
        {
            if (visited.Contains(start)) continue;
            var component = new List<HexCoord>();
            var queue = new Queue<HexCoord>();
            queue.Enqueue(start);
            visited.Add(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    var nb = current.Neighbor(dir);
                    if (set.Contains(nb) && visited.Add(nb))
                        queue.Enqueue(nb);
                }
            }
            components.Add(component);
        }

        return components;
    }
}
