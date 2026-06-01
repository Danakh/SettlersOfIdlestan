using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using CivilizationModel = SettlersOfIdlestan.Model.Civilization.Civilization;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Island map filtered to the tiles visible to a civilization.
/// A tile is visible when it touches one of the civilization's cities or roads.
/// Cities with a Watchtower reveal hexes within a radius of 2 instead of 1.
/// For roads, tiles touching either endpoint vertex are visible too.
/// </summary>
public class VisibleIslandMap : IslandMap
{
    public VisibleIslandMap(IslandMap sourceMap, CivilizationModel civilization)
        : base(GetVisibleTiles(sourceMap, civilization))
    {
    }

    private static IEnumerable<HexTile> GetVisibleTiles(IslandMap sourceMap, CivilizationModel civilization)
    {
        if (sourceMap == null) throw new ArgumentNullException(nameof(sourceMap));
        if (civilization == null) throw new ArgumentNullException(nameof(civilization));

        var visibleHexes = new HashSet<HexCoord>();

        foreach (var city in civilization.Cities)
        {
            if (!sourceMap.IsOnSameLayer(city.Position))
                continue;

            int radius = city.Buildings.Any(b => b.Type == BuildingType.Watchtower && b.Level > 0) ? 2 : 1;
            AddVertexHexesWithRadius(visibleHexes, city.Position, radius);
        }

        foreach (var road in civilization.Roads)
        {
            if (!sourceMap.IsOnSameLayer(road.Position))
                continue;

            foreach (var vertex in road.Position.GetVertices())
            {
                AddVertexHexesWithRadius(visibleHexes, vertex, 1);
            }
        }

        return visibleHexes
            .Where(sourceMap.IsOnSameLayer)
            .Select(sourceMap.GetTile)
            .Where(tile => tile != null)
            .Cast<HexTile>();
    }

    private static void AddVertexHexesWithRadius(HashSet<HexCoord> visibleHexes, Vertex vertex, int radius)
    {
        var frontier = new HashSet<HexCoord>(vertex.GetHexes());
        foreach (var hex in frontier)
            visibleHexes.Add(hex);

        for (int r = 1; r < radius; r++)
        {
            var next = new HashSet<HexCoord>();
            foreach (var hex in frontier)
                foreach (var neighbor in hex.Neighbors())
                    if (!visibleHexes.Contains(neighbor))
                        next.Add(neighbor);
            foreach (var hex in next)
                visibleHexes.Add(hex);
            frontier = next;
        }
    }
}
