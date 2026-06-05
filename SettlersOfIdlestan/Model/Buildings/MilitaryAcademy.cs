using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class MilitaryAcademy : Building, IUniqueBuilding
{
    public const int MaxSoldiersPerLevel = 5;

    public MilitaryAcademy() : base(BuildingType.MilitaryAcademy)
    {
        AvailableAtLevel = 2;
    }

    // Locked by default; unlocked by the Military Academy prestige vertex (+4 max level)
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

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.25 * Level);
    }
}
