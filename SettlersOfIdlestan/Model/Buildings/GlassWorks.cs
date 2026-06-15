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

    public override Resource? AutomaticHarvestResource => Resource.Glass;

    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Desert)
            return Resource.Glass;
        return null;
    }

    // Desert production is 2x slower than other terrains
    public override long GetAutomaticHarvestCooldown(long baseCooldownTicks, int? atLevel = null)
    {
        int level = atLevel ?? Level;
        long levelsAbove = Math.Max(0, level - AutomaticHarvestUnlockLevel);
        return Math.Max(1L, baseCooldownTicks * 2 - levelsAbove * 50);
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 20 },
        { Resource.Brick, 20 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Desert);
    }
}
