using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Trône des Vents : bâtiment unique racial des Garudas (voir RaceDefinitions). Rend +3 de défense
/// des villes (compensant le malus racial -3) et génère de l'Or passivement. Niveau max par
/// défaut 0 : constructible uniquement quand la race Garuda fournit son BUILDING_MAX_LEVEL +1.
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
        yield return new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, 3);
        yield return new Modifier(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Gold), EType.ADDITIVE, 5);
    }
}
