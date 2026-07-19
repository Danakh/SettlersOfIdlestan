using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Mine : Building
{
    public Mine() : base(BuildingType.Mine)
    {
        AvailableAtLevel = 3;
    }

    public override int GetDefaultMaxLevel() => 4;

    public override Resource? AutomaticHarvestResource => Resource.Ore;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Mountain)
            return Resource.Ore;
        return null;
    }

    // Ore production is 2x slower than the default automatic harvest rate
    public override long GetAutomaticHarvestCooldown(long baseCooldownTicks, int? atLevel = null)
    {
        int level = atLevel ?? Level;
        long levelsAbove = Math.Max(0, level - AutomaticHarvestUnlockLevel);
        return Math.Max(1L, baseCooldownTicks * 2 - levelsAbove * 50);
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 40 },
        { Resource.Wood, 20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 40 * (level + 1) },
        { Resource.Wood, 20 * (level + 1) },
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Mountain);
    }
}
