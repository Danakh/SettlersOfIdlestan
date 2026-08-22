using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Grand Terrier : bâtiment unique racial des Gobelins (voir RaceDefinitions). Réduit le coût des
/// nouvelles villes (l'expansion dense est leur force), agrandit le stockage de base et abaisse d'un
/// niveau tous les prérequis des bâtiments uniques. Niveau max par défaut 0 : constructible
/// uniquement quand la race Gobeline fournit son BUILDING_MAX_LEVEL +1.
///
/// <para>Hôtel de Ville 3 requis, et non 4 comme les autres bâtiments raciaux : le malus gobelin
/// BUILDING_MAX_LEVEL -1 sur les bâtiments standards leur rend les seuils de niveau 4 bien plus durs
/// à atteindre qu'aux autres races. C'est aussi ce que corrige la réduction de prérequis qu'il
/// accorde — sans elle, un Port Impérial exigeant un Comptoir niveau 4 reste hors de portée d'un
/// Gobelin dont les Comptoirs plafonnent à 3.</para>
/// </summary>
public class GreatBurrow : Building, IUniqueBuilding
{
    public GreatBurrow() : base(BuildingType.GreatBurrow)
    {
        AvailableAtLevel = 3;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    // Race Gobeline : +1 (voir RaceDefinitions.cs).
    public override int GetAbsoluteMaxLevel() => 1;

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
        yield return new Modifier(ECategory.UNIQUE_BUILDING_PREREQUISITE_REDUCTION, EType.ADDITIVE, 1);
    }
}
