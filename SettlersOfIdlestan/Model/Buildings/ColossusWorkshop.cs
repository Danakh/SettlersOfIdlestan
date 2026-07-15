using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Atelier des Colosses : bâtiment unique racial des Géants (voir RaceDefinitions). Donne une
/// chance de doubler le rendement de toutes les récoltes automatiques. Niveau max par défaut 0 :
/// constructible uniquement quand la race Géante fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class ColossusWorkshop : Building, IUniqueBuilding
{
    public ColossusWorkshop() : base(BuildingType.ColossusWorkshop)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 100 },
        { Resource.Wood,  100 },
        { Resource.Gold,   50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.HARVEST_PRODUCTION_BONUS, EType.ADDITIVE, 10);
    }
}
