using System;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents a tile on the island map, which is a hexagon with a terrain type and optional resource.
/// </summary>
[Serializable]
public class HexTile
{
    [JsonConstructor]
    public HexTile()
    {
        // Parameterless constructor for deserialization
    }

    public HexTile(HexCoord coord, TerrainType terrainType, int? productionNumber = null)
    {
        Coord = coord;
        TerrainType = terrainType;
        ProductionNumber = productionNumber;

        // Validation: only producing terrains can have production numbers
        if (productionNumber.HasValue && !Resource.HasValue)
        {
            throw new ArgumentException("Only terrain types that produce resources can have production numbers.");
        }
    }

    public HexCoord Coord { get; set; }
    public TerrainType TerrainType { get; set; }
    public Resource? Resource => TerrainTypeMappings.TerrainResourceMap[TerrainType];
    public int? ProductionNumber { get; set; }

    public override string ToString()
    {
        return $"HexTile({Coord}, {TerrainType}, {Resource}, {ProductionNumber})";
    }
}