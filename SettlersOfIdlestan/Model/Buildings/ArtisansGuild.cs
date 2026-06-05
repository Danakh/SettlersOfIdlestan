using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class ArtisansGuild : Building, IUniqueBuilding
{
    public long LastArtisanBuildTick { get; set; }

    public ArtisansGuild() : base(BuildingType.ArtisansGuild)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoArtisanCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(City city) =>
        city.Buildings.Any(b => b.Type == BuildingType.Forge && b.Level >= 4);

    public override string? GetMissingPrerequisiteKey(City city) =>
        HasBuildPrerequisites(city) ? null : "tooltip_requires_forge_level4";

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 150 },
        { Resource.Stone, 75 },
        { Resource.Ore, 75 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;

        yield return new Modifier(ECategory.BUILDING_MAX_LEVEL, "Forge", EType.ADDITIVE, 1);
    }
}
