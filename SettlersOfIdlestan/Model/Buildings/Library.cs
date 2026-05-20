using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Library building.
/// </summary>
public class Library : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Library"/> class.
    /// </summary>
    public Library() : base(BuildingType.Library)
    {
        AvailableAtLevel = 2;
    }

    public override int GetDefaultMaxLevel()
    {
        return 4;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 10 },
        { Resource.Brick, 5 },
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 10 * (level + 1) },
        { Resource.Brick, 5 * (level + 1) },
    };
}