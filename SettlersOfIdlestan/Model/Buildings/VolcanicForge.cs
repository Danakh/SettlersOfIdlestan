using System.Linq;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Forge Volcanique — bâtiment unique exploitant la chaleur d'un volcan adjacent : génère du Verre
/// passivement (obsidienne) et augmente la production civ-wide de Minerai (Mine), d'Acier (Fonderie)
/// et de Mithril (Mine de Mithril), au prorata du niveau. Ne peut être construite qu'à côté d'un
/// volcan découvert. Verrouillée par défaut ; débloquée par la recherche Métallurgie Volcanique
/// (+3 niveaux max).
/// </summary>
public class VolcanicForge : Building, IUniqueBuilding
{
    /// <summary>Verre généré passivement par cycle (1 000 ticks), par niveau.</summary>
    public const int GlassGenerationPerLevel = 2;

    /// <summary>Chance (en %) supplémentaire de doubler la récolte automatique de Minerai, par niveau.</summary>
    public const int OreHarvestBonusPerLevel = 10;

    /// <summary>Acier supplémentaire produit par cycle de la Fonderie, par niveau.</summary>
    public const int SteelBonusPerLevel = 1;

    /// <summary>Chance (en %) supplémentaire de doubler la récolte automatique de Mithril, par niveau.</summary>
    public const int MithrilHarvestBonusPerLevel = 10;

    public VolcanicForge() : base(BuildingType.VolcanicForge)
    {
        AvailableAtLevel = 3;
    }

    public override bool IsUnique => true;

    // Verrouillée par défaut ; débloquée par la recherche Métallurgie Volcanique (+3 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState state)
        => IsAdjacentToFoundVolcano(city, state);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state)
        => HasBuildPrerequisites(city, state) ? null : "tooltip_requires_volcano";

    private static bool IsAdjacentToFoundVolcano(IBuildingContext city, WorldState state)
        => city.Position.GetHexes().Any(hex => state.GetFeaturesAt(hex).OfType<VolcanoFeature>().Any(v => v.Found));

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 400 },
        { Resource.Ore,   200 },
        { Resource.Gold,  150 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 250 * (level + 1) },
        { Resource.Ore,   150 * (level + 1) },
        { Resource.Steel,  20 * (level + 1) },
    };

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Glass), EType.ADDITIVE, GlassGenerationPerLevel * Level);
        yield return new Modifier(ECategory.HARVEST_PRODUCTION_BONUS, nameof(BuildingType.Mine), EType.ADDITIVE, OreHarvestBonusPerLevel * Level);
        yield return new Modifier(ECategory.BUILDING_PRODUCTION, nameof(BuildingType.Smelter), EType.ADDITIVE, SteelBonusPerLevel * Level);
        yield return new Modifier(ECategory.HARVEST_PRODUCTION_BONUS, nameof(BuildingType.MithrilMine), EType.ADDITIVE, MithrilHarvestBonusPerLevel * Level);
    }
}
