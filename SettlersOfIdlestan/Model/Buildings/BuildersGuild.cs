using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class BuildersGuild : Building
{
    public long LastRoadBuildTick { get; set; }
    public long LastOutpostBuildTick { get; set; }

    /// <summary>Flat reduction (Wood and Brick) applied to road build costs.</summary>
    public int RoadCostReduction => Level switch
    {
        1 => 1,
        2 => 2,
        >= 3 => 4,
        _ => 0
    };

    /// <summary>Maximum road distance that can be auto-constructed (capped at 3).</summary>
    public int MaxAutoRoadDistance => Math.Min(Level, 3);

    /// <summary>Multiplier applied to the per-existing-city new-city cost surcharge (surface & underworld) once built (any level).</summary>
    public const double NewCitySurchargeMultiplier = 0.5;

    public BuildersGuild() : base(BuildingType.BuildersGuild)
    {
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;
    public override bool ProvidesAutomation => true;

    public override int GetDefaultMaxLevel() => 4;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 100 },
        { Resource.Brick, 50 },
        { Resource.Stone, 50 }
    };

    public override ResourceSet GetUpgradeCost(int level) => level switch
    {
        2 => new ResourceSet { { Resource.Wood, 200 }, { Resource.Brick, 100 }, { Resource.Stone, 100 } },
        3 => new ResourceSet { { Resource.Wood, 300 }, { Resource.Brick, 150 }, { Resource.Stone, 150 } },
        4 => new ResourceSet { { Resource.Wood, 500 }, { Resource.Brick, 250 }, { Resource.Stone, 250 } },
        _ => new ResourceSet()
    };
}
