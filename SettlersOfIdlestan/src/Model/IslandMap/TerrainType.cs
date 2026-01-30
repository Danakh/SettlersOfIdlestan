namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the terrain type of a hex tile.
/// </summary>
public enum TerrainType
{
    Forest,  // Produces Wood
    Hill,    // Produces Brick
    Pasture, // Produces Sheep
    Field,   // Produces Wheat
    Mountain,// Produces Ore
    Desert,  // No production
    Water    // No production
}

/// <summary>
/// Provides mappings for terrain types.
/// </summary>
public static class TerrainTypeMappings
{
    /// <summary>
    /// Maps each terrain type to its produced resource, or null if none.
    /// </summary>
    public static readonly Dictionary<TerrainType, Resource?> TerrainResourceMap = new()
    {
        { TerrainType.Forest, Resource.Wood },
        { TerrainType.Hill, Resource.Brick },
        { TerrainType.Pasture, Resource.Sheep },
        { TerrainType.Field, Resource.Wheat },
        { TerrainType.Mountain, Resource.Ore },
        { TerrainType.Desert, null },
        { TerrainType.Water, null }
    };

    /// <summary>
    /// Maps each resource to its corresponding terrain type.
    /// </summary>
    public static readonly Dictionary<Resource, TerrainType> ResourceTerrainMap = new()
    {
        { Resource.Wood, TerrainType.Forest },
        { Resource.Brick, TerrainType.Hill },
        { Resource.Sheep, TerrainType.Pasture },
        { Resource.Wheat, TerrainType.Field },
        { Resource.Ore, TerrainType.Mountain }
    };
}