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
        AvailableAtLevel = 0;
    }

    public override int GetDefaultMaxLevel()
    {
        return 4;
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

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Food, 2 },
        { Resource.Wood, 2 },
        { Resource.Brick, 2 }
    };

    public override ResourceSet GetUpgradeCost(int level)
    {
        var result = new ResourceSet
        {
            { Resource.Food, 2 * (level * level + 1) },
            { Resource.Wood, 2 * (level * level + 1) },
            { Resource.Brick, 2 * (level * level + 1) },
            { Resource.Stone, 2 * (level * level + 1) }
        };
        if (level > 2)
        {
            result.Add(Resource.Gold, 10 * (level - 1));
        }
        return result;
    }
}