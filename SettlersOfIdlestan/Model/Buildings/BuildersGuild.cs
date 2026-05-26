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
        AvailableAtLevel = 4;
    }

    public override bool IsUnique => true;

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 100 },
        { Resource.Brick, 50 },
        { Resource.Stone, 50 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new();
}