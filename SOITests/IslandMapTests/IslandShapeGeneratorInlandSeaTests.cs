using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.IslandMapTests;

public class IslandShapeGeneratorInlandSeaTests
{
    public static IEnumerable<object[]> SeedsAndCounts()
    {
        yield return new object[] { 1, 30 };
        yield return new object[] { 42, 40 };
        yield return new object[] { 100, 65 };
        yield return new object[] { 999, 65 };
        yield return new object[] { 12345, 50 };
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_ReturnsExactlyRequestedLandCount(int seed, int count)
    {
        var generator = new IslandShapeGeneratorInlandSea(new GamePRNG(seed));
        var coords = generator.GenerateCoords(count);

        Assert.Equal(count, coords.Count);
        Assert.Equal(count, coords.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(SeedsAndCounts))]
    public void GenerateCoords_HasInlandSeaConnectedToExteriorByASingleMaritimeEdge(int seed, int count)
    {
        var generator = new IslandShapeGeneratorInlandSea(new GamePRNG(seed));
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

        // L'eau extérieure et la mer intérieure doivent être reliées (une seule composante d'eau) :
        // pas un lac fermé.
        var components = ConnectedComponents(waterBoundary);
        Assert.Single(components);

        // Il doit exister un edge "pont" (bridge) du graphe d'adjacence eau-eau dont la suppression
        // isole un petit bassin de 3 à 6 hexs (la mer intérieure) du reste de l'eau (le large).
        Assert.True(HasSingleEdgeChokepointPocket(waterBoundary, minPocket: 3, maxPocket: 6),
            "Aucun goulet d'étranglement (edge unique) séparant un bassin de 3 à 6 hexs n'a été trouvé.");
    }

    // Cherche, parmi tous les edges eau-eau, un "pont" dont la suppression scinde le graphe en deux
    // composantes dont la plus petite a une taille comprise entre minPocket et maxPocket.
    private static bool HasSingleEdgeChokepointPocket(HashSet<HexCoord> waterBoundary, int minPocket, int maxPocket)
    {
        var nodes = waterBoundary.ToList();
        var adjacency = nodes.ToDictionary(
            n => n,
            n => HexDirectionUtils.AllHexDirections.Select(n.Neighbor).Where(waterBoundary.Contains).ToList());

        var seen = new HashSet<(HexCoord, HexCoord)>();
        foreach (var a in nodes)
            foreach (var b in adjacency[a])
            {
                var key = a.GetHashCode() < b.GetHashCode() ? (a, b) : (b, a);
                if (!seen.Add(key)) continue;

                var visited = new HashSet<HexCoord> { a };
                var queue = new Queue<HexCoord>();
                queue.Enqueue(a);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var nb in adjacency[current])
                    {
                        if ((current.Equals(a) && nb.Equals(b)) || (current.Equals(b) && nb.Equals(a))) continue;
                        if (visited.Add(nb)) queue.Enqueue(nb);
                    }
                }

                if (visited.Contains(b)) continue; // not a bridge

                int pocketSize = System.Math.Min(visited.Count, nodes.Count - visited.Count);
                if (pocketSize >= minPocket && pocketSize <= maxPocket)
                    return true;
            }

        return false;
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
