using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Fosse aux Crânes : bâtiment unique racial des Orcs (voir RaceDefinitions). Offre la nourriture
/// d'entretien de quelques soldats par ville. Niveau max par défaut 0 : constructible uniquement
/// quand la race Orc fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class SkullPit : Building, IUniqueBuilding
{
    public SkullPit() : base(BuildingType.SkullPit)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood,  50 },
        { Resource.Ore,   75 },
        { Resource.Gold,  50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, 5);
    }
}
