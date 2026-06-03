using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Algorithme A* sur le graphe des sommets (Vertex) de la grille hexagonale.
/// Chaque arÃªte (Edge) relie deux Vertex adjacents avec un coÃ»t unitaire.
/// </summary>
public static class HexGridPathfinder
{
    /// <summary>
    /// Calcule le chemin le plus court entre deux Vertex en parcourant les edges de la grille.
    /// La longueur du chemin retournÃ© est toujours Ã©gale Ã  from.EdgeDistanceTo(to) + 1.
    /// Retourne { from, to } si aucun chemin n'est trouvÃ© (ne devrait pas arriver sur une grille infinie).
    /// </summary>
    public static List<Vertex> FindVertexPath(Vertex from, Vertex to)
    {
        from.EnsureSameZ(to, nameof(FindVertexPath));

        if (from.Equals(to)) return new List<Vertex> { from };

        var open = new PriorityQueue<Vertex, int>();
        var cameFrom = new Dictionary<Vertex, Vertex?>();
        var gScore = new Dictionary<Vertex, int>();
        var closed = new HashSet<Vertex>();

        open.Enqueue(from, 0);
        gScore[from] = 0;
        cameFrom[from] = null;

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (closed.Contains(current)) continue;
            closed.Add(current);

            if (current.Equals(to))
                return ReconstructPath(cameFrom, to);

            foreach (var neighbor in current.GetAdjacentVertices())
            {
                if (closed.Contains(neighbor)) continue;
                int tentativeG = gScore[current] + 1;
                if (!gScore.TryGetValue(neighbor, out int existingG) || tentativeG < existingG)
                {
                    gScore[neighbor] = tentativeG;
                    cameFrom[neighbor] = current;
                    open.Enqueue(neighbor, tentativeG + neighbor.EdgeDistanceTo(to));
                }
            }
        }

        return new List<Vertex> { from, to };
    }

    private static List<Vertex> ReconstructPath(Dictionary<Vertex, Vertex?> cameFrom, Vertex goal)
    {
        var path = new List<Vertex>();
        Vertex? current = goal;
        while (current != null)
        {
            path.Add(current);
            cameFrom.TryGetValue(current, out current);
        }
        path.Reverse();
        return path;
    }
}
