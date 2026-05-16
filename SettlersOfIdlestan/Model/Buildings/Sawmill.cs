using SettlersOfIdlestan.Model.Civilization;
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
        AvailableAtLevel = 1;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 }
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Forest);
    }
}