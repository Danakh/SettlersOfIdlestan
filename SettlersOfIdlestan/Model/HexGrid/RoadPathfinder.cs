using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// BFS sur le graphe des routes d'une civilisation pour trouver le chemin entre deux Vertex.
/// </summary>
public static class RoadPathfinder
{
    /// <summary>
    /// Trouve le chemin le plus court entre deux Vertex en n'empruntant que les routes fournies.
    /// Retourne null si aucun chemin n'existe. Le chemin inclut les Vertex de départ et d'arrivée.
    /// </summary>
    public static List<Vertex>? FindPath(IEnumerable<Road> roads, Vertex from, Vertex to)
    {
        if (from.Equals(to)) return new List<Vertex> { from };

        var adjacency = BuildAdjacency(roads, from.Z);

        var prev = new Dictionary<Vertex, Vertex?> { [from] = null };
        var queue = new Queue<Vertex>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur.Equals(to))
                return ReconstructPath(prev, to);

            if (!adjacency.TryGetValue(cur, out var neighbors)) continue;
            foreach (var n in neighbors)
            {
                if (prev.ContainsKey(n)) continue;
                prev[n] = cur;
                queue.Enqueue(n);
            }
        }

        return null;
    }

    /// <summary>
    /// Variante qui prend un graphe d'adjacence déjà construit — évite de le reconstruire à chaque appel.
    /// </summary>
    public static List<Vertex>? FindPathInGraph(Dictionary<Vertex, List<Vertex>> adjacency, Vertex from, Vertex to)
    {
        if (from.Equals(to)) return new List<Vertex> { from };

        var prev = new Dictionary<Vertex, Vertex?> { [from] = null };
        var queue = new Queue<Vertex>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur.Equals(to))
                return ReconstructPath(prev, to);

            if (!adjacency.TryGetValue(cur, out var neighbors)) continue;
            foreach (var n in neighbors)
            {
                if (prev.ContainsKey(n)) continue;
                prev[n] = cur;
                queue.Enqueue(n);
            }
        }

        return null;
    }

    public static Dictionary<Vertex, List<Vertex>> BuildAdjacency(IEnumerable<Road> roads, int z)
    {
        var adj = new Dictionary<Vertex, List<Vertex>>();
        foreach (var road in roads)
        {
            if (road.Position.Z != z) continue;
            var verts = road.Position.GetVertices();
            var v1 = verts[0];
            var v2 = verts[1];
            Link(adj, v1, v2);
            Link(adj, v2, v1);
        }
        return adj;
    }

    private static void Link(Dictionary<Vertex, List<Vertex>> adj, Vertex a, Vertex b)
    {
        if (!adj.TryGetValue(a, out var list))
            adj[a] = list = new List<Vertex>();
        list.Add(b);
    }

    private static List<Vertex> ReconstructPath(Dictionary<Vertex, Vertex?> prev, Vertex goal)
    {
        var path = new List<Vertex>();
        Vertex? cur = goal;
        while (cur != null)
        {
            path.Add(cur);
            prev.TryGetValue(cur, out cur);
        }
        path.Reverse();
        return path;
    }
}
