using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Arbre-Cœur : bâtiment unique racial des Elfes (voir RaceDefinitions). Accélère la génération de
/// points de recherche et produit du Bois passivement. Relie aussi par la Forêt les villes du
/// joueur toutes deux adjacentes à une case Forêt et sur le même plan : le renfort entre elles
/// ignore alors la portée normale et le réseau routier, et transite instantanément, sans soldat
/// visible en marche sur la carte (voir ReinforcementEngine.HasUnlimitedRangeReinforcementLink).
/// Niveau max par
/// défaut 0 : constructible uniquement quand la race Elfe fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class HeartTree : Building, IUniqueBuilding
{
    public HeartTree() : base(BuildingType.HeartTree)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 150 },
        { Resource.Food,  50 },
        { Resource.Gold,  50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.25);
        yield return new Modifier(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Wood), EType.ADDITIVE, 5);
        yield return new Modifier(ECategory.UNLOCK_FOREST_REINFORCEMENT_LINK, EType.ADDITIVE, 1);
    }
}
