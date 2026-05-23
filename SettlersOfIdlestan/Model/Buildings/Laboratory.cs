using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Laboratory : Building
{
    public Laboratory() : base(BuildingType.Laboratory)
    {
        AvailableAtLevel = 2;
    }

    // Locked by default; unlocked by the Laboratory prestige vertex (+2 max level)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Brick, 50 },
        { Resource.Stone, 20 },
        { Resource.Glass, 10 },
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Brick, 50 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) },
        { Resource.Glass, 10 * (level + 1) },
    };
}
