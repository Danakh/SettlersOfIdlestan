using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Barracks : Building
{
    public const int MaxSoldiersPerLevel = 5;

    public Barracks() : base(BuildingType.Barracks)
    {
        AvailableAtLevel = 1;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Locked by default; unlocked by the Barracks prestige vertex (+2 max level)
    public override int GetDefaultMaxLevel() => 0;

    public override int GetDefenseBonus() => 5;
    public override int GetMaxSoldiersBonus() => MaxSoldiersPerLevel * Level;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Food, 20 },
        { Resource.Wood, 20 },
        { Resource.Stone, 50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Food, 20 * (level + 1)},
        { Resource.Wood, 20 * (level + 1) },
        { Resource.Stone, 50 * (level + 1) },
    };
}
