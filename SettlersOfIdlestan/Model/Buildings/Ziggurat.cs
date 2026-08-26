using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Bâtiment unique racial des Humains (voir RaceDefinitions). Émet le flag
/// DOMINION_HARVEST_SPEED_DOUBLED : double le bonus de vitesse de récolte du Dominion pour la
/// civilisation (voir Dominion.GetHarvestTimeMultiplier).
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

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city);

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState state)
        => state.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_DOMINION)
        && city.HasBuildingAtLevel(BuildingType.Temple, 4);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state)
    {
        if (!state.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_DOMINION))
            return "tooltip_requires_dominion";
        if (!city.HasBuildingAtLevel(BuildingType.Temple, 4))
            return "tooltip_requires_temple_level4";
        return null;
    }

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
        yield return new Modifier(ECategory.DOMINION_HARVEST_SPEED_DOUBLED, EType.ADDITIVE, 1.0);
    }
}
