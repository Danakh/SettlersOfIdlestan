using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Builders Guild building.
/// </summary>
public class BuildersGuild : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildersGuild"/> class.
    /// </summary>
    public BuildersGuild() : base(BuildingType.BuildersGuild)
    {
        MaxLevel = 1;
        AvailableAtLevel = 4;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 3 },
        { Resource.Brick, 2 },
        { Resource.Ore, 2 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost();
}