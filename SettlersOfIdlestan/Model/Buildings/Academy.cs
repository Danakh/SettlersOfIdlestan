using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class Academy : Building, IUniqueBuilding
{
    public long LastLibraryBuildTick { get; set; }

    public Academy() : base(BuildingType.Academy)
    {
        AvailableAtLevel = 3;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoLibraryCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(IBuildingContext city) =>
        city.Buildings.Any(b => b.Type == BuildingType.Library && b.Level >= 4);

    public override string? GetMissingPrerequisiteKey(IBuildingContext city) =>
        HasBuildPrerequisites(city) ? null : "tooltip_requires_library_level4";

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 100 },
        { Resource.Stone, 50 },
        { Resource.Glass, 30 },
    };

    public override ResourceSet GetUpgradeCost(int level) => level switch
    {
        2 => new ResourceSet { { Resource.Brick, 200 }, { Resource.Stone, 100 }, { Resource.Glass, 60 } },
        3 => new ResourceSet { { Resource.Brick, 350 }, { Resource.Stone, 175 }, { Resource.Glass, 100 } },
        4 => new ResourceSet { { Resource.Brick, 550 }, { Resource.Stone, 275 }, { Resource.Glass, 160 } },
        5 => new ResourceSet { { Resource.Brick, 800 }, { Resource.Stone, 400 }, { Resource.Glass, 240 } },
        _ => new ResourceSet()
    };

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;

        yield return new Modifier(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.25 * Level);
        yield return new Modifier(ECategory.RESEARCH_CANCEL_REFUND_BONUS, EType.ADDITIVE, Math.Min(0.5, 0.125 * Level));
    }
}
