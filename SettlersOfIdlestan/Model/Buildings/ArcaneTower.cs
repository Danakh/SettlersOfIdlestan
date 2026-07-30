using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class ArcaneTower : Building, IUniqueBuilding
{
    /// <summary>Fraction de réduction du coût d'entretien des rituels accordée par la Tour des Arcanes.</summary>
    public const double RitualUpkeepReduction = 0.25;

    public long LastMagicBuildTick { get; set; }

    public ArcaneTower() : base(BuildingType.ArcaneTower)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoMagicCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(IBuildingContext city) =>
        city.Buildings.Any(b => b.Type == BuildingType.MageTower && b.Level >= 4);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city) =>
        HasBuildPrerequisites(city) ? null : "tooltip_requires_mage_tower";

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
    }
}
