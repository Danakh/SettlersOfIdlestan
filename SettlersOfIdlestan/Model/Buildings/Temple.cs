using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Temple building.
/// </summary>
public class Temple : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Temple"/> class.
    /// </summary>
    public Temple() : base(BuildingType.Temple)
    {
        MaxLevel = 4;
        AvailableAtLevel = 3;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 40 },
        { Resource.Brick, 20 },
        { Resource.Stone, 100 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 40 * (level + 1) },
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Stone, 100 * (level + 1) }
    };
}