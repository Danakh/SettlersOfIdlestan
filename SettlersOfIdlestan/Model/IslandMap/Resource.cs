namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the resources produced by land tiles.
/// </summary>
public enum Resource
{
    Food,
    Wood,
    Brick,
    Stone,
    Ore,
    Gold,
    Glass,
    Crystal
}

public class ResourceUtils
{
    public static List<Resource> BasicResources = new List<Resource>
    {
        Resource.Food,
        Resource.Wood,
        Resource.Brick,
        Resource.Stone
    };
}   