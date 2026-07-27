using System.Linq;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Tour de Mages — le nombre de tours limite le nombre de rituels actifs simultanés,
/// la somme de leurs niveaux limite la puissance totale des rituels.
/// Extrait aussi automatiquement des cristaux des Grottes de Cristal adjacentes (Inframonde).
/// Ne peut être construite qu'à côté d'une Grotte de Cristal ou d'un Cercle de Fées découvert.
/// Verrouillée par défaut ; débloquée par le vertex de prestige Secret de la Magie.
/// </summary>
public class MageTower : Building
{
    /// <summary>Cooldown de base (en ticks) de la récolte automatique de cristaux : 20s, réduit avec le niveau.</summary>
    public const long CrystalHarvestBaseCooldownTicks = 2000L;

    public MageTower() : base(BuildingType.MageTower)
    {
        AvailableAtLevel = 4;
    }

    // Verrouillé par défaut ; débloqué par le vertex de prestige Secret de la Magie (+3 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override Resource? AutomaticHarvestResource => Resource.Crystal;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override long GetAutomaticHarvestCooldown(long baseCooldownTicks, int? atLevel = null)
        => base.GetAutomaticHarvestCooldown(CrystalHarvestBaseCooldownTicks, atLevel);

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.CrystalCave)
            return Resource.Crystal;
        return null;
    }

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState state)
        => IsAdjacentToCrystalCaveOrFoundFairyCircle(city, state);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state)
        => HasBuildPrerequisites(city, state) ? null : "tooltip_requires_crystal_cave_or_fairy_circle";

    private static bool IsAdjacentToCrystalCaveOrFoundFairyCircle(IBuildingContext city, WorldState state)
    {
        var map = state.GetMapFor(city.Position);
        if (map != null && map.VertexHasTerrainType(city.Position, TerrainType.CrystalCave))
            return true;

        return city.Position.GetHexes().Any(hex => state.GetFeaturesAt(hex).OfType<FairyCircle>().Any(f => f.Found));
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   60 },
        { Resource.Glass,   20 },
        { Resource.Crystal,  5 },
        { Resource.Gold,    30 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone,   40 * (level + 1) },
        { Resource.Glass,   20 * (level + 1) },
        { Resource.Crystal,  5 * (level + 1) },
        { Resource.Gold,    20 * (level + 1) },
    };
}
