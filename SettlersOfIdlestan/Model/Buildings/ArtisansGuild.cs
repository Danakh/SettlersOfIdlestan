using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class ArtisansGuild : Building, IUniqueBuilding
{
    /// <summary>Bonus de capacité de stockage des ressources basiques accordé par niveau.</summary>
    public const int StorageCapacityBasicBonusPerLevel = 100;

    /// <summary>Bonus de capacité de stockage des ressources avancées accordé par niveau.</summary>
    public const int StorageCapacityAdvancedBonusPerLevel = 50;

    public long LastArtisanBuildTick { get; set; }

    public ArtisansGuild() : base(BuildingType.ArtisansGuild)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoArtisanCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState? state) =>
        city.HasBuildingAtLevel(BuildingType.Forge, 4);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState? state) =>
        HasBuildPrerequisites(city, state) ? null : "tooltip_requires_forge_level4";

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
        yield return new Modifier(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, StorageCapacityBasicBonusPerLevel * Level);
        yield return new Modifier(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, StorageCapacityAdvancedBonusPerLevel * Level);
    }
}
