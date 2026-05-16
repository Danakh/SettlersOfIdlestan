using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Town Hall building.
/// </summary>
public class TownHall : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TownHall"/> class.
    /// </summary>
    public TownHall() : base(BuildingType.TownHall)
    {
        MaxLevel = 4;
        AvailableAtLevel = 1;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 2 },
        { Resource.Brick, 2 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 },
        { Resource.Ore, 1 }
    };
}