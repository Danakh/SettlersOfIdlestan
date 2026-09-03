using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Trône des Vents : bâtiment unique racial des Garudas (voir RaceDefinitions). Ajoute +1 de portée
/// d'attaque des villes (cumulé au +1 racial, soit +2 au total), +1 de portée d'attaque de
/// l'Aventurier et améliore les ratios de commerce du Marché dans les deux sens (achat moins cher,
/// vente plus rémunératrice). Niveau max par défaut 0 : constructible uniquement quand la race
/// Garuda fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class ThroneOfWinds : Building, IUniqueBuilding
{
    public ThroneOfWinds() : base(BuildingType.ThroneOfWinds)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood,  100 },
        { Resource.Stone, 100 },
        { Resource.Gold,   50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1);
        yield return new Modifier(ECategory.TRADE_RATIO_BONUS, EType.ADDITIVE, 0.15);
        yield return new Modifier(ECategory.ADVENTURER_ATTACK_RANGE_BONUS, EType.ADDITIVE, 1);
    }
}
