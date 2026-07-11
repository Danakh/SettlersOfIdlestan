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
    MushroomCave,
    DeepWater
}

public static class TerrainTypeExtensions
{
    /// <summary>
    /// Vrai pour <see cref="TerrainType.Water"/> et <see cref="TerrainType.DeepWater"/> : à utiliser
    /// partout où "ceci n'est pas un hex de terre" est testé, pour que la bordure d'eau profonde
    /// cosmétique ne soit jamais traitée comme un hex de terre valide.
    /// </summary>
    public static bool IsWater(this TerrainType terrain) => terrain is TerrainType.Water or TerrainType.DeepWater;
}
