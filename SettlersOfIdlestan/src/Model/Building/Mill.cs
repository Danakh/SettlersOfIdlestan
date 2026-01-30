using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Mill building.
/// </summary>
public class Mill : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Mill"/> class.
    /// </summary>
    public Mill() : base(BuildingType.Mill)
    {
        Production.Add(Resource.Wheat, 1);
        MaxLevel = 4;
        Description = "Moulin - Produit du blé";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 2 },
        { Resource.Brick, 2 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };
}