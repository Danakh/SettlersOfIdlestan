using SettlersOfIdlestan.Model.HexGrid;
using CivilizationModel = SettlersOfIdlestan.Model.Civilization.Civilization;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Island map filtered to the tiles visible to a civilization.
/// A tile is visible when it touches one of the civilization's cities or roads.
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
            AddVertexHexes(visibleHexes, city.Position);
        }

        foreach (var road in civilization.Roads)
        {
            foreach (var vertex in road.Position.GetVertices())
            {
                AddVertexHexes(visibleHexes, vertex);
            }
        }

        return visibleHexes
            .Select(sourceMap.GetTile)
            .Where(tile => tile != null)
            .Cast<HexTile>();
    }

    private static void AddVertexHexes(HashSet<HexCoord> visibleHexes, Vertex vertex)
    {
        foreach (var hex in vertex.GetHexes())
        {
            visibleHexes.Add(hex);
        }
    }
}
