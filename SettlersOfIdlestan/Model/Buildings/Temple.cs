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
        AvailableAtLevel = 3;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 40 },
        { Resource.Brick, 20 },
        { Resource.Stone, 100 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Wood, 40 * (level + 1) },
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Stone, 100 * (level + 1) }
    };
}