using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Garrison : Building
{
    public const int MaxSoldiersPerLevel = 5;

    public Garrison() : base(BuildingType.Garrison)
    {
        AvailableAtLevel = 2;
    }

    // Locked by default; unlocked by the Garrison prestige vertex (+4 max level)
    public override int GetDefaultMaxLevel() => 0;
    public override int GetMaxSoldiersBonus() => MaxSoldiersPerLevel * Level;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 100 },
        { Resource.Gold,  100 },
        { Resource.Glass,  20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 100 * (level + 1) },
        { Resource.Gold,  100 * (level + 1) },
        { Resource.Glass,  20 * (level + 1) },
    };

    public override double GetUnitProductionSpeedBonus() => 0.25 * Level;
}
