using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Barracks : Building
{
    public Barracks() : base(BuildingType.Barracks)
    {
        AvailableAtLevel = 1;
    }

    // Locked by default; unlocked by the Barracks prestige vertex (+2 max level)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Food, 20 },
        { Resource.Wood, 20 },
        { Resource.Stone, 50 },
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Food, 20 * (level + 1)},
        { Resource.Wood, 20 * (level + 1) },
        { Resource.Stone, 50 * (level + 1) },
    };
}
