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

    public HexTile(HexCoord coord, TerrainType terrainType)
    {
        Coord = coord;
        TerrainType = terrainType;
    }

    public HexCoord Coord { get; set; }
    public TerrainType TerrainType { get; set; }

    public override string ToString()
    {
        return $"HexTile({Coord}, {TerrainType})";
    }
}