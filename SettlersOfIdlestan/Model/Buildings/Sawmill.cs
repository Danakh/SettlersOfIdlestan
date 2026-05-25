using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Sawmill building.
/// </summary>
public class Sawmill : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sawmill"/> class.
    /// </summary>
    public Sawmill() : base(BuildingType.Sawmill)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel()
    {
        return 4;
    }

    public override Resource? ManualHarvestResource => null;
    public override Resource? AutomaticHarvestResource => Resource.Wood;

    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Forest)
            return Resource.Wood;
        return null;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 10 },
        { Resource.Brick, 5 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 10 * (level + 1) },
        { Resource.Brick, 5 * (level + 1) }
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Forest);
    }
}