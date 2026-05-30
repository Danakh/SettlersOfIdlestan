using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Laboratory : Building
{
    public long LastResearchTick { get; set; } = 0;

    public const int ResearchPointsPerBatch = 10;

    public Laboratory() : base(BuildingType.Laboratory)
    {
        AvailableAtLevel = 2;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Locked by default; unlocked by the Laboratory prestige vertex (+2 max level)
    public override int GetDefaultMaxLevel() => 0;

    public long GetResearchCooldownTicks(int? atLevel = null)
    {
        int level = atLevel ?? Level;
        if (level < 1) return long.MaxValue;
        return Math.Max(1L, (long)(1000.0 * Math.Pow(0.8, level - 1)));
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 50 },
        { Resource.Stone, 20 },
        { Resource.Glass, 10 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Brick, 50 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) },
        { Resource.Glass, 10 * (level + 1) },
    };
}
