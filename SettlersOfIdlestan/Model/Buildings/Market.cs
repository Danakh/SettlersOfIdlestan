using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Market building.
/// </summary>
public class Market : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Market"/> class.
    /// </summary>
    public Market() : base(BuildingType.Market)
    {
        MaxLevel = 1;
        AvailableAtLevel = 1;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Food, 5 },
        { Resource.Wood, 5 },
        { Resource.Brick, 5 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new();
}