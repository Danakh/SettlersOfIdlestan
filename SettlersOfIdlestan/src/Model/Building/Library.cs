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
        MaxLevel = 4;
        AvailableAtLevel = 2;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 3 },
        { Resource.Brick, 2 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };
}