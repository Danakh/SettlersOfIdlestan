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

    public override Resource? ManualHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Forest)
            return Resource.Wood;
        if (terrain == TerrainType.Hill)
            return Resource.Brick;
        if (terrain == TerrainType.Plain)
            return Resource.Food;
        if (terrain == TerrainType.Mountain)
            return Resource.Stone;
        return null;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Food, 2 },
        { Resource.Wood, 2 },
        { Resource.Brick, 2 }
    };

    public override ResourceCost GetUpgradeCost(int level)
    {
        var result = new ResourceCost
        {
            { Resource.Food, 2 * (level^2 + 1) },
            { Resource.Wood, 2 * (level^2 + 1) },
            { Resource.Brick, 2 * (level^2 + 1) },
            { Resource.Stone, 2 * (level^2 + 1) }
        };
        if (level > 2)
        {
            result.Add(Resource.Gold, 10 * (level + 1));
        }
        if (level > 3)
        {
            result.Add(Resource.Glass, 10 * (level + 1));
        }
        return result;
    }
}