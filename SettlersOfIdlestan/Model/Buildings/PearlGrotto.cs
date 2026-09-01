using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Grotte aux Perles : bâtiment unique racial des Sirènes (voir RaceDefinitions). Rend +8 de
/// défense des villes (+3 de base, +5) et génère de la Nourriture passivement. Double la vitesse
/// de génération périodique de ressource aléatoire des Ports maritimes de niveau 3+, et élargit le
/// tirage aux ressources intermédiaires/avancées (hors consommables). Niveau max par défaut 0 :
/// constructible uniquement quand la race Sirènes fournit son BUILDING_MAX_LEVEL +1.
/// </summary>
public class PearlGrotto : Building, IUniqueBuilding
{
    public PearlGrotto() : base(BuildingType.PearlGrotto)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood,  80 },
        { Resource.Stone, 60 },
        { Resource.Gold,  40 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.CITY_DEFENSE, EType.ADDITIVE, 8);
        yield return new Modifier(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Food), EType.ADDITIVE, 5);
        yield return new Modifier(ECategory.SEAPORT_RANDOM_RESOURCE_SPEED, EType.ADDITIVE, 1.0);
        yield return new Modifier(ECategory.UNLOCK_SEAPORT_ADVANCED_RESOURCE_GENERATION, EType.ADDITIVE, 1);
    }
}
