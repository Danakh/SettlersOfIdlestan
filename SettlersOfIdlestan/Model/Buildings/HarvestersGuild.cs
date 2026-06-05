using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Buildings;

public class HarvestersGuild : Building, IUniqueBuilding
{
    private static readonly BuildingType[] ProductionBuildingTypes =
    [
        BuildingType.Sawmill, BuildingType.Brickworks, BuildingType.Quarry, BuildingType.Mill
    ];

    public long LastProductionBuildTick { get; set; }

    public HarvestersGuild() : base(BuildingType.HarvestersGuild)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;
    public override int GetDefaultMaxLevel() => 0;

    public long GetAutoProductionCooldownTicks() => 1000L;

    public override bool HasBuildPrerequisites(City city)
    {
        int count = city.Buildings.Count(b => ProductionBuildingTypes.Contains(b.Type) && b.Level >= 4);
        return count >= 3;
    }

    public override string? GetMissingPrerequisiteKey(City city) =>
        HasBuildPrerequisites(city) ? null : "tooltip_requires_3_production_level4";

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 150 },
        { Resource.Brick, 75 },
        { Resource.Stone, 75 },
        { Resource.Food, 50 }
    };

    public override ResourceSet GetUpgradeCost(int level) => level switch
    {
        2 => new ResourceSet { { Resource.Wood, 300 }, { Resource.Brick, 150 }, { Resource.Stone, 150 } },
        3 => new ResourceSet { { Resource.Wood, 500 }, { Resource.Brick, 250 }, { Resource.Stone, 250 } },
        4 => new ResourceSet { { Resource.Wood, 800 }, { Resource.Brick, 400 }, { Resource.Stone, 400 } },
        _ => new ResourceSet()
    };

    public IEnumerable<Modifier> GetUniqueBuildingModifiers()
    {
        if (Level <= 0) yield break;

        foreach (var type in ProductionBuildingTypes)
        {
            yield return new Modifier(
                ECategory.BUILDING_MAX_LEVEL,
                type.ToString(),
                EType.ADDITIVE,
                1);
        }
    }
}
