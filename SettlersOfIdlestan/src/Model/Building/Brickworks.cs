using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Brickworks building.
/// </summary>
public class Brickworks : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Brickworks"/> class.
    /// </summary>
    public Brickworks() : base(BuildingType.Brickworks)
    {
        Production.Add(Resource.Brick, 1);
        MaxLevel = 4;
        Description = "Briqueterie - Produit de la brique";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    /// <summary>
    /// Gets the building cost for the Brickworks.
    /// </summary>
    /// <returns>A dictionary containing the resources and their quantities needed to build the Brickworks.</returns>
    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };

    /// <summary>
    /// Gets the upgrade cost for the Brickworks at the specified level.
    /// </summary>
    /// <param name="level">The level to which the building is to be upgraded.</param>
    /// <returns>A dictionary containing the resources and their quantities needed to upgrade the Brickworks.</returns>
    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };
}