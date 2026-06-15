using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class WarRoom : Building, IUniqueBuilding
{
    public long LastMilitaryBuildTick { get; set; }

    public WarRoom() : base(BuildingType.WarRoom)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoMilitaryCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(IBuildingContext city) =>
        city.Buildings.Any(b => b.Type == BuildingType.MilitaryAcademy && b.Level >= 1);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city) =>
        HasBuildPrerequisites(city) ? null : "tooltip_requires_military_academy";

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 100 },
        { Resource.Gold,  100 },
        { Resource.Ore,    50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;
        yield return new Modifier(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.5);
    }
}
