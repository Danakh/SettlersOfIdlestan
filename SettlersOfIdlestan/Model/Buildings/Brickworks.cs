using SettlersOfIdlestan.Model.Civilization;
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
        AvailableAtLevel = 1;
    }

    /// <summary>
    /// Gets the building cost for the Brickworks.
    /// </summary>
    /// <returns>A dictionary containing the resources and their quantities needed to build the Brickworks.</returns>
    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 5 },
        { Resource.Brick, 10 }
    };

    /// <summary>
    /// Gets the upgrade cost for the Brickworks at the specified level.
    /// </summary>
    /// <param name="level">The level to which the building is to be upgraded.</param>
    /// <returns>A dictionary containing the resources and their quantities needed to upgrade the Brickworks.</returns>
    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 5 * (level + 1) },
        { Resource.Brick, 10 * (level + 1) }
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;

        return map.VertexHasTerrainType(city.Position, TerrainType.Hill);
    }
}