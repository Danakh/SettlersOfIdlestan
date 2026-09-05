using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Fosse aux Crânes : bâtiment unique racial des Orcs (voir RaceDefinitions). Augmente la capacité
/// maximale de soldats des villes, ajoute +1 de portée d'attaque des villes, +10% de vitesse
/// d'attaque et retranche 10 or/s à l'entretien d'un Raid actif. Niveau max par défaut 0 :
/// constructible uniquement quand la race Orc fournit son BUILDING_MAX_LEVEL +1.
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
        yield return new Modifier(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 5);
        yield return new Modifier(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1);
        yield return new Modifier(ECategory.ATTACK_SPEED, EType.ADDITIVE, 0.1);
        yield return new Modifier(ECategory.RAID_UPKEEP_REDUCTION, EType.ADDITIVE, 10);
    }
}
