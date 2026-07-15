using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Forge Runique : bâtiment unique racial des Nains (voir RaceDefinitions). Améliore la chance de
/// double récolte des Forges, la chance d'or des Mines et la production des Fonderies. Niveau max
/// par défaut 0 : constructible uniquement quand la race Naine fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class RunicForge : Building, IUniqueBuilding
{
    public RunicForge() : base(BuildingType.RunicForge)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 100 },
        { Resource.Ore,    50 },
        { Resource.Gold,   50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 15);
        yield return new Modifier(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10);
        yield return new Modifier(ECategory.BUILDING_PRODUCTION, nameof(BuildingType.Smelter), EType.ADDITIVE, 1);
    }
}
