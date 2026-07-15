using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Grand Terrier : bâtiment unique racial des Gobelins (voir RaceDefinitions). Réduit le coût des
/// nouvelles villes (l'expansion dense est leur force) et agrandit le stockage de base. Niveau max
/// par défaut 0 : constructible uniquement quand la race Gobeline fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class GreatBurrow : Building, IUniqueBuilding
{
    public GreatBurrow() : base(BuildingType.GreatBurrow)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood,  75 },
        { Resource.Brick, 75 },
        { Resource.Gold,  50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.NEW_CITY_COST_REDUCTION, EType.ADDITIVE, 0.25);
        yield return new Modifier(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 20);
    }
}
