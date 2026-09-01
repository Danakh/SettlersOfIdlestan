using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class ArcaneTower : Building, IUniqueBuilding
{
    /// <summary>Fraction de réduction du coût d'entretien des rituels accordée par la Tour des Arcanes.</summary>
    public const double RitualUpkeepReduction = 0.25;

    /// <summary>Cristal généré passivement par cycle de 5 secondes (voir HarvestController.PassiveCrystalGenerationIntervalTicks) — soit 1 Cristal/s.</summary>
    public const int CrystalGenerationPerCrystalCycle = 5;

    public long LastMagicBuildTick { get; set; }

    public ArcaneTower() : base(BuildingType.ArcaneTower)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoMagicCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state) =>
        city.HasBuildingAtLevel(BuildingType.MageTower, 4);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state) =>
        HasBuildPrerequisites(city, state) ? null : "tooltip_requires_mage_tower";

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   80 },
        { Resource.Glass,   30 },
        { Resource.Crystal, 20 },
        { Resource.Gold,    60 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.RITUAL_UPKEEP_REDUCTION, EType.ADDITIVE, RitualUpkeepReduction);
        yield return new Modifier(ECategory.PASSIVE_RESOURCE_GENERATION, nameof(Resource.Crystal), EType.ADDITIVE, CrystalGenerationPerCrystalCycle * Level);
        yield return new Modifier(ECategory.UNLOCK_RESOURCE, nameof(Resource.Crystal), EType.ADDITIVE, 1);
    }
}
