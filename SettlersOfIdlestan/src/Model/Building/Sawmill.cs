using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Sawmill building.
/// </summary>
public class Sawmill : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sawmill"/> class.
    /// </summary>
    public Sawmill() : base(BuildingType.Sawmill)
    {
        Production.Add(Resource.Wood, 1);
        MaxLevel = 4;
        Description = "Scierie - Produit du bois";
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