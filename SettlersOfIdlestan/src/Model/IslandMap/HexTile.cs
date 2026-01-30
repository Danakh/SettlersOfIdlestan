using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents a tile on the island map, which is a hexagon with a terrain type and optional resource.
/// </summary>
public class HexTile
{
    public HexTile(HexCoord coord, TerrainType terrainType, Resource? resource = null, int? productionNumber = null)
    {
        Coord = coord;
        TerrainType = terrainType;
        Resource = resource;
        ProductionNumber = productionNumber;

        // Validation: only Land tiles can have resources
        if (terrainType != TerrainType.Land && resource.HasValue)
        {
            throw new ArgumentException("Only Land tiles can have resources.");
        }
        if (terrainType == TerrainType.Land && !resource.HasValue)
        {
            throw new ArgumentException("Land tiles must have a resource.");
        }
    }

    public HexCoord Coord { get; }
    public TerrainType TerrainType { get; }
    public Resource? Resource { get; }
    public int? ProductionNumber { get; }

    public override string ToString()
    {
        return $"HexTile({Coord}, {TerrainType}, {Resource}, {ProductionNumber})";
    }
}