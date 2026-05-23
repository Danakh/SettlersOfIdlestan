using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class GlassWorks : Building
{
    public GlassWorks() : base(BuildingType.GlassWorks)
    {
        AvailableAtLevel = 2;
    }

    // Locked by default; unlocked by the Laboratory prestige vertex (+1 max level)
    public override int GetDefaultMaxLevel() => 0;

    public override int AutomaticHarvestUnlockLevel => 2;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Desert)
            return Resource.Glass;
        return null;
    }

    // Desert production is 2x slower than other terrains
    public override double GetAutomaticHarvestCooldownMultiplier(TerrainType terrain)
        => terrain == TerrainType.Desert ? 2.0 : 1.0;

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Stone, 20 },
        { Resource.Brick, 20 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost();

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Desert);
    }
}
