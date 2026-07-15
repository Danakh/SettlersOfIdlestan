using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Bâtiment unique racial des Humains (voir RaceDefinitions). Émet le flag TEMPLE_INSTANT_DOMINION :
/// chaque Temple construit ou amélioré déclenche instantanément une production de Dominion sur les
/// 3 hexs de sa ville, à 100 %, jusqu'à <see cref="MaxTriggersPerCity"/> fois par ville (voir
/// City.ZigguratTriggersUsed et CorruptionController.ApplyZigguratInstantProduction).
/// Niveau max par défaut 0 : constructible uniquement quand la race Humaine fournit son
/// BUILDING_MAX_LEVEL +1 (même patron que les uniques débloqués par prestige).
/// </summary>
public class Ziggurat : Building, IUniqueBuilding
{
    /// <summary>Temple max niveau 4 (avec Foi) : construction + 3 améliorations = 4 déclenchements par ville.</summary>
    public const int MaxTriggersPerCity = 4;

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
        => state.PlayerCivilization.ModifierAggregator.HasModifier(ECategory.UNLOCK_DOMINION);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state)
        => HasBuildPrerequisites(city, state) ? null : "tooltip_requires_dominion";

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
        yield return new Modifier(ECategory.TEMPLE_INSTANT_DOMINION, EType.ADDITIVE, 1.0);
    }
}
