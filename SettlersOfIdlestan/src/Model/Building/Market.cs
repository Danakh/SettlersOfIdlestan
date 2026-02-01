using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Market building.
/// </summary>
public class Market : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Market"/> class.
    /// </summary>
    public Market() : base(BuildingType.Market)
    {
        MaxLevel = 1;
        Description = "Marché - Permet le commerce (4:1)";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 5 },
        { Resource.Brick, 5 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new();
}