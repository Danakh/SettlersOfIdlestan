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
        => FindPathInGraph(BuildAdjacency(roads, from.Z), from, to);

    /// <summary>
    /// Variante qui prend un graphe d'adjacence déjà construit — évite de le reconstruire à chaque appel.
    /// </summary>
    /// <param name="maxDepth">
    /// Nombre maximum de segments à explorer (null = illimité). Évite de parcourir tout le graphe
    /// quand l'appelant va de toute façon rejeter les chemins plus longs qu'une portée donnée.
    /// </param>
    public static List<Vertex>? FindPathInGraph(Dictionary<Vertex, List<Vertex>> adjacency, Vertex from, Vertex to, int? maxDepth = null)
    {
        if (from.Equals(to)) return new List<Vertex> { from };

        var prev = new Dictionary<Vertex, Vertex?>();
        return Search(adjacency, from, to, maxDepth, prev) ? ReconstructPath(prev, to) : null;
    }

    /// <summary>
    /// Teste uniquement l'existence d'un chemin. Moins coûteux que <see cref="FindPathInGraph"/>
    /// (pas de table de parents ni de reconstruction) quand l'appelant n'a besoin que du booléen.
    /// </summary>
    public static bool HasPathInGraph(Dictionary<Vertex, List<Vertex>> adjacency, Vertex from, Vertex to, int? maxDepth = null)
        => from.Equals(to) || Search(adjacency, from, to, maxDepth, null);

    /// <summary>
    /// Retourne tous les Vertex atteignables depuis <paramref name="from"/> en au plus
    /// <paramref name="maxDepth"/> segments (<paramref name="from"/> inclus).
    /// Un seul parcours remplace N appels de pathfinding quand on teste plusieurs cibles
    /// depuis la même origine.
    /// </summary>
    /// <param name="reachable">
    /// Ensemble de travail optionnel, vidé puis rempli par l'appel. Les appelants qui lancent un
    /// parcours par emplacement (assignation des flux de renfort : un BFS par ville, à chaque
    /// réévaluation) réutilisent ainsi la même instance au lieu d'en allouer une par ville.
    /// </param>
    /// <param name="queue">File de travail optionnelle, même usage.</param>
    public static HashSet<Vertex> ReachableWithin(
        Dictionary<Vertex, List<Vertex>> adjacency, Vertex from, int maxDepth,
        HashSet<Vertex>? reachableBuffer = null, Queue<Vertex>? queueBuffer = null)
    {
        var reachable = reachableBuffer ?? new HashSet<Vertex>();
        reachable.Clear();
        reachable.Add(from);
        if (maxDepth <= 0) return reachable;

        var queue = queueBuffer ?? new Queue<Vertex>();
        queue.Clear();
        queue.Enqueue(from);

        for (int depth = 0; depth < maxDepth && queue.Count > 0; depth++)
        {
            int levelSize = queue.Count;
            for (int i = 0; i < levelSize; i++)
            {
                if (!adjacency.TryGetValue(queue.Dequeue(), out var neighbors)) continue;
                foreach (var n in neighbors)
                    if (reachable.Add(n)) queue.Enqueue(n);
            }
        }

        return reachable;
    }

    /// <summary>
    /// BFS par niveaux. La profondeur est déduite du découpage en niveaux plutôt que stockée par
    /// sommet, et <paramref name="prev"/> sert à la fois de table de parents et de marqueur de
    /// visite : un seul hachage de Vertex par voisin au lieu de trois.
    /// Quand une portée est fournie, on élague avec la distance géométrique restante (minorant
    /// admissible du nombre de segments) : la recherche reste dirigée vers la cible au lieu de
    /// balayer toute la composante connexe.
    /// </summary>
    private static bool Search(
        Dictionary<Vertex, List<Vertex>> adjacency,
        Vertex from,
        Vertex to,
        int? maxDepth,
        Dictionary<Vertex, Vertex?>? prev)
    {
        bool bounded = maxDepth.HasValue;
        int limit = maxDepth ?? int.MaxValue;
        if (limit <= 0) return false;

        // Le graphe d'adjacence ne contient qu'une seule couche : une cible sur une autre couche
        // (ou une origine hors du réseau routier) est forcément inatteignable.
        if (from.Z != to.Z) return false;
        if (bounded && from.EdgeDistanceTo(to) > limit) return false;
        if (!adjacency.ContainsKey(from)) return false;

        HashSet<Vertex>? seen = null;
        if (prev != null) prev[from] = null;
        else seen = new HashSet<Vertex> { from };

        var queue = new Queue<Vertex>();
        queue.Enqueue(from);

        for (int depth = 1; depth <= limit && queue.Count > 0; depth++)
        {
            int budget = limit - depth;   // segments encore disponibles une fois arrivé à cette profondeur
            int levelSize = queue.Count;

            for (int i = 0; i < levelSize; i++)
            {
                var cur = queue.Dequeue();
                if (!adjacency.TryGetValue(cur, out var neighbors)) continue;

                foreach (var n in neighbors)
                {
                    if (n.Equals(to))
                    {
                        // La cible n'est jamais marquée comme visitée : ce test précède tout marquage.
                        if (prev != null) prev[n] = cur;
                        return true;
                    }

                    // Depuis cette profondeur, plus aucun segment disponible : inutile d'enfiler n.
                    if (budget == 0) continue;

                    // TryAdd/Add sert de test de visite ET de marquage en un seul hachage.
                    if (prev != null ? !prev.TryAdd(n, cur) : !seen!.Add(n)) continue;

                    // Minorant géométrique : n ne peut plus atteindre la cible dans le budget restant.
                    if (bounded && n.EdgeDistanceTo(to) > budget) continue;

                    queue.Enqueue(n);
                }
            }
        }

        return false;
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
