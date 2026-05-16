using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Forge building.
/// </summary>
public class Forge : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Forge"/> class.
    /// </summary>
    public Forge() : base(BuildingType.Forge)
    {
        Production.Add(Resource.Ore, 1);
        MaxLevel = 4;
        AvailableAtLevel = 2;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Brick, 5 },
        { Resource.Stone, 20 },
        { Resource.Ore, 5 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Brick, 5 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) },
        { Resource.Ore, 5 * (level + 1) }
    };
}