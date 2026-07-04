using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the resources produced by land tiles.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<Resource>))]
public enum Resource
{
    Food,
    Wood,
    Brick,
    Stone,
    Gold,
    Ore,
    Glass,
    Steel,
    Crystal,
    Mithril,
    SteelWeapon,
    SteelArmor,
    MithrilWeapon,
    MithrilArmor,
    HealingPotion,
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
    public static List<Resource> ConsumableResources = new List<Resource>
    {
        Resource.SteelWeapon,
        Resource.SteelArmor,
        Resource.MithrilWeapon,
        Resource.MithrilArmor,
        Resource.HealingPotion,
    };
}