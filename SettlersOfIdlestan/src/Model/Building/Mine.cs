using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Mine building.
/// </summary>
public class Mine : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mine"/> class.
    /// </summary>
    public Mine() : base(BuildingType.Mine)
    {
        Production.Add(Resource.Ore, 1);
        MaxLevel = 4;
        Description = "Mine - Produit du minerai";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };
}