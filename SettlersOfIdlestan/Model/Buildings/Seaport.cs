using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Seaport building.
/// </summary>
public class Seaport : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Seaport"/> class.
    /// </summary>
    public Seaport() : base(BuildingType.Seaport)
    {
        MaxLevel = 4;
        AvailableAtLevel = 2;
        Actions.Add("Prestige");
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;

        return map.VertexHasTerrainType(city.Position, TerrainType.Water);
    }
}