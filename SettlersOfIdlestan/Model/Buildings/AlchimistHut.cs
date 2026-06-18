using System.Linq;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Hutte d'Alchimie — récolte automatiquement les cristaux des Cercles de Fées adjacents
/// (comportement aligné sur les bâtiments de production : cooldown de base 60s, réduit avec le
/// niveau, modificateur HARVEST_SPEED applicable) et produit des Potions de Soin (consommable).
/// Ne peut être construite qu'adjacente à un Cercle de Fées découvert.
/// Verrouillée par défaut ; débloquée par le vertex de prestige Hutte d'Alchimie.
/// </summary>
public class AlchimistHut : Building
{
    /// <summary>Cooldown de base (en ticks) de la récolte automatique de cristaux : 60s, réduit avec le niveau.</summary>
    public const long CrystalHarvestBaseCooldownTicks = 6000L;

    /// <summary>Dernier tick où la hutte a récolté des cristaux des Cercles de Fées adjacents.</summary>
    public long LastCrystalProductionTick { get; set; } = 0;

    /// <summary>Dernier tick où la hutte a produit une Potion de Soin.</summary>
    public long LastPotionProductionTick { get; set; } = 0;

    /// <summary>Verre consommé par Potion de Soin produite.</summary>
    public const int GlassInputPerPotion = 1;

    /// <summary>Cristal consommé par Potion de Soin produite.</summary>
    public const int CrystalInputPerPotion = 1;

    public AlchimistHut() : base(BuildingType.AlchimistHut)
    {
        AvailableAtLevel = 1;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillée par défaut ; débloquée par le vertex de prestige Hutte d'Alchimie (+3 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override bool IsAvailableInLayer(int z) => z == IslandMap.IslandMap.SurfaceLayer;

    public override Resource? AutomaticHarvestResource => Resource.Crystal;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override long GetAutomaticHarvestCooldown(long baseCooldownTicks, int? atLevel = null)
        => base.GetAutomaticHarvestCooldown(CrystalHarvestBaseCooldownTicks, atLevel);

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState state)
        => IsAdjacentToFoundFairyCircle(city, state);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state)
        => HasBuildPrerequisites(city, state) ? null : "tooltip_requires_fairy_circle";

    private static bool IsAdjacentToFoundFairyCircle(IBuildingContext city, WorldState state)
        => city.Position.GetHexes().Any(hex => state.GetFeaturesAt(hex).OfType<FairyCircle>().Any(f => f.Found));

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   50 },
        { Resource.Glass,   10 },
        { Resource.Gold,    50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone,   30 * (level + 1) },
        { Resource.Glass,    5 * (level + 1) },
        { Resource.Crystal,  3 * (level + 1) },
    };
}
