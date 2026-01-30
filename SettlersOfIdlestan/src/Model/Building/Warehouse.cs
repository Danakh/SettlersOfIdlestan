using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Warehouse building.
/// </summary>
public class Warehouse : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Warehouse"/> class.
    /// </summary>
    public Warehouse() : base(BuildingType.Warehouse)
    {
        MaxLevel = 4;
        Description = "Entrepôt - Augmente la capacité de stockage des ressources";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    /// <summary>
    /// Gets the build cost of the warehouse.
    /// </summary>
    /// <returns>A dictionary containing the resources and their quantities required for building.</returns>
    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 2 },
        { Resource.Brick, 2 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };

    /// <summary>
    /// Gets the upgrade cost for the warehouse.
    /// </summary>
    /// <param name="level">The current level of the warehouse.</param>
    /// <returns>A dictionary containing the resources and their quantities required for upgrading.</returns>
    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };
}