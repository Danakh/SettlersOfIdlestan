using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Mine building.
/// </summary>
public class Mine : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mine"/> class.
    /// </summary>
    public Mine() : base(BuildingType.Mine)
    {
        MaxLevel = 4;
        AvailableAtLevel = 1;
    }

    public override Resource? ManualHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Mountain)
            return Resource.Ore;
        return null;
    }
    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Mountain)
            return Resource.Stone;
        return null;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 20 },
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 20 * (level + 1) },
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Mountain);
    }
}