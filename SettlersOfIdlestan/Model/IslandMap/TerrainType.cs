using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the terrain type of a hex tile.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TerrainType>))]
public enum TerrainType
{
    Forest,
    Hill,
    Plain,
    Mountain,
    Desert,
    Water,
    MithrilVein,
    CrystalCave,
    MushroomCave
}
