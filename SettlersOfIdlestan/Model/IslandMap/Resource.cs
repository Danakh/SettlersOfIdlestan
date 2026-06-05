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
    Crystal,
    Mithril,
    Steel
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
    public static List<Resource> IntermediateResources = new List<Resource>
    {
        Resource.Ore,
        Resource.Gold
    };
    public static List<Resource> AdvancedResources = new List<Resource>
    {
        Resource.Glass,
        Resource.Crystal,
        Resource.Mithril,
        Resource.Steel
    };
}