using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using CivilizationModel = SettlersOfIdlestan.Model.Civilization.Civilization;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Island map filtered to the tiles visible to a civilization.
/// A tile is visible when it touches one of the civilization's cities or roads.
/// Cities with a Watchtower reveal hexes within a radius of 2 instead of 1 (3 with the
/// Great Lighthouse's level 1 bonus).
/// For roads, tiles touching either endpoint vertex are visible too.
/// </summary>
public class VisibleIslandMap : IslandMap
{
    public VisibleIslandMap(IslandMap sourceMap, CivilizationModel civilization, bool watchtowerVisionBonus = false)
        : base(GetVisibleTiles(sourceMap, civilization, watchtowerVisionBonus), sourceMap.Z)
    {
    }

    private static IEnumerable<HexTile> GetVisibleTiles(IslandMap sourceMap, CivilizationModel civilization, bool watchtowerVisionBonus)
    {
        if (sourceMap == null) throw new ArgumentNullException(nameof(sourceMap));
        if (civilization == null) throw new ArgumentNullException(nameof(civilization));

        var visibleHexes = new HashSet<HexCoord>();

        // Ensembles de travail du BFS, alloués une fois pour toute la carte au lieu d'une paire par
        // source. Seul le cas rayon ≥ 2 (Tour de Guet) les utilise ; voir AddVertexHexesWithRadius.
        HashSet<HexCoord>? visited = null;
        HashSet<HexCoord>? frontier = null;
        HashSet<HexCoord>? next = null;

        foreach (var city in civilization.Cities)
        {
            if (!sourceMap.IsOnSameLayer(city.Position))
                continue;

            bool hasWatchtower = city.FindBuilding(BuildingType.Watchtower) is { Level: > 0 };
            int radius = hasWatchtower ? (watchtowerVisionBonus ? 3 : 2) : 1;
            AddVertexHexesWithRadius(visibleHexes, city.Position, radius, ref visited, ref frontier, ref next);
        }

        foreach (var road in civilization.Roads)
        {
            if (!sourceMap.IsOnSameLayer(road.Position))
                continue;

            foreach (var vertex in road.Position.GetVertices())
            {
                AddVertexHexesWithRadius(visibleHexes, vertex, 1, ref visited, ref frontier, ref next);
            }
        }

        return visibleHexes
            .Where(sourceMap.IsOnSameLayer)
            .Select(sourceMap.GetTile)
            .Where(tile => tile != null)
            .Cast<HexTile>();
    }

    /// <summary>
    /// Ajoute à <paramref name="visibleHexes"/> les hexagones à <paramref name="radius"/> anneaux du
    /// sommet donné.
    ///
    /// <para>Rayon 1 — le cas de très loin le plus fréquent : chaque route appelle cette méthode deux
    /// fois, et une carte de fin de partie compte des milliers de routes — se réduit aux trois
    /// hexagones du sommet, sans BFS ni ensemble de travail. La version précédente allouait deux
    /// HashSet par appel avant de constater que la boucle de propagation ne tournait pas une seule
    /// fois : c'était le premier poste d'allocation de toute la simulation.</para>
    ///
    /// <para>Au-delà, les ensembles de travail sont fournis par l'appelant et réutilisés d'une source
    /// à l'autre. <paramref name="visited"/> reste propre à chaque source : réutiliser directement
    /// visibleHexes pour couper la frontière ferait qu'un hex déjà révélé par une AUTRE source proche
    /// (route, ville sans Tour de Guet…) bloquerait la propagation de celle-ci vers son anneau
    /// suivant, faisant disparaître des hexs pourtant à portée (typiquement l'anneau 2 d'une Tour de
    /// Guet boostée par le Grand Phare, quand une autre ville ou route est adjacente).</para>
    /// </summary>
    private static void AddVertexHexesWithRadius(
        HashSet<HexCoord> visibleHexes, Vertex vertex, int radius,
        ref HashSet<HexCoord>? visited, ref HashSet<HexCoord>? frontier, ref HashSet<HexCoord>? next)
    {
        var hexes = vertex.GetHexes();
        for (int i = 0; i < hexes.Length; i++)
            visibleHexes.Add(hexes[i]);

        if (radius <= 1) return;

        visited ??= new HashSet<HexCoord>();
        frontier ??= new HashSet<HexCoord>();
        next ??= new HashSet<HexCoord>();

        visited.Clear();
        frontier.Clear();
        for (int i = 0; i < hexes.Length; i++)
        {
            visited.Add(hexes[i]);
            frontier.Add(hexes[i]);
        }

        for (int r = 1; r < radius; r++)
        {
            next.Clear();
            foreach (var hex in frontier)
                foreach (var neighbor in hex.Neighbors())
                    if (!visited.Contains(neighbor))
                        next.Add(neighbor);

            foreach (var hex in next)
            {
                visited.Add(hex);
                visibleHexes.Add(hex);
            }

            (frontier, next) = (next, frontier);
        }
    }
}
