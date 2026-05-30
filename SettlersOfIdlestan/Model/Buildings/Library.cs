using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Library : Building
{
    public long LastResearchTick { get; set; } = 0;

    public Library() : base(BuildingType.Library)
    {
        AvailableAtLevel = 2;
    }

    public override int GetDefaultMaxLevel() => 0;

    public bool CanProduceResearch => true;

    public long GetResearchCooldownTicks(int? atLevel = null) => (atLevel ?? Level) switch
    {
        1 => 1000L,
        2 => 800L,
        >= 3 => 600L,
        _ => long.MaxValue,
    };

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 40 },
        { Resource.Brick, 20 },
        { Resource.Stone, 20 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Wood, 40 * (level + 1) },
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) }
    };
}
