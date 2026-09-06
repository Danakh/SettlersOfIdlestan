using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Bâtiment unique racial des Humains (voir RaceDefinitions). Émet deux modificateurs :
/// DOMINION_HARVEST_SPEED_BONUS (+50% sur le bonus de vitesse de récolte du Dominion pour la
/// civilisation, voir Dominion.GetHarvestTimeMultiplier) et TEMPLE_DOMINION_LEVEL_BONUS (chaque
/// Temple produit du Dominion comme s'il avait un niveau de plus, voir
/// CorruptionController.GetTempleDominionLevel).
/// Niveau max par défaut 0 : constructible uniquement quand la race Humaine fournit son
/// BUILDING_MAX_LEVEL +1 (même patron que les uniques débloqués par prestige).
/// Prérequis de construction : Dominion débloqué (pouvoir divin Foi) ET un Temple niveau 4 dans
/// la ville (voir HasBuildPrerequisites).
/// </summary>
public class Ziggurat : Building, IUniqueBuilding
{
    public Ziggurat() : base(BuildingType.Ziggurat)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override int GetDefaultMaxLevel() => 0;

    public override bool IsAvailableInLayer(int z) => z == IslandMap.IslandMap.SurfaceLayer;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city, civ);

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state)
        => HasDominion(state) && city.HasBuildingAtLevel(BuildingType.Temple, 4);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state)
    {
        if (!HasDominion(state))
            return "tooltip_requires_dominion";
        if (!city.HasBuildingAtLevel(BuildingType.Temple, 4))
            return "tooltip_requires_temple_level4";
        return null;
    }

    /// <summary>Monde absent : le pouvoir divin Foi n'est pas consultable, le prérequis est tenu pour non rempli.</summary>
    private static bool HasDominion(WorldState? state) =>
        state != null && state.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_DOMINION);

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 100 },
        { Resource.Stone, 100 },
        { Resource.Gold,   50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.DOMINION_HARVEST_SPEED_BONUS, EType.ADDITIVE, 0.5);
        yield return new Modifier(ECategory.TEMPLE_DOMINION_LEVEL_BONUS, EType.ADDITIVE, 1);
    }
}
