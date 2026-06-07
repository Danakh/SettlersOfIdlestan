using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Quarry : Building
{
    public Quarry() : base(BuildingType.Quarry)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 4;

    public override Resource? AutomaticHarvestResource => Resource.Stone;

    public override Resource? ManualHarvestCapability(TerrainType terrain)
    {
        return null;
    }

    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Mountain)
            return Resource.Stone;
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Wood, 20 * (level + 1) },
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Mountain);
    }
}
