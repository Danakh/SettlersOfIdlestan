using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Library : Building
{
    public long LastResearchTick { get; set; } = 0;

    public Library() : base(BuildingType.Library)
    {
        AvailableAtLevel = 2;
    }

    public override int GetDefaultMaxLevel() => 1;

    public bool CanProduceResearch => Level >= 2;

    public long GetResearchCooldownTicks() => Level switch
    {
        2 => 1000L,
        3 => 800L,
        >= 4 => 600L,
        _ => long.MaxValue,
    };

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 10 },
        { Resource.Brick, 5 },
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 10 * (level + 1) },
        { Resource.Brick, 5 * (level + 1) },
    };
}
