using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Sheepfold building.
/// </summary>
public class Sheepfold : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sheepfold"/> class.
    /// </summary>
    public Sheepfold() : base(BuildingType.Sheepfold)
    {
        Production.Add(Resource.Sheep, 1);
        MaxLevel = 4;
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

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City.City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.Pasture);
    }
}