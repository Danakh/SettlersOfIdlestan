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
        AvailableAtLevel = 0;
        Actions.Add("Prestige");
    }

    public override int GetDefaultMaxLevel()
    {
        return 4;
    }

    public override Resource? ManualHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Water)
            return Resource.Food;
        return null;
    }

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if ((Level > 1) && (terrain == TerrainType.Water))
            return Resource.Food;
        return null;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Wood, 10 },
    };

    public override ResourceCost GetUpgradeCost(int level)
    {
        if (level == 2)
        {
            return new ResourceCost
            {
                { Resource.Food, 10 },
                { Resource.Wood, 30 },
                { Resource.Stone, 20 }
            };
        }
        else
        {
            return new ResourceCost
            {
                { Resource.Food, 50 * (level + 1) },
                { Resource.Wood, 100 * (level + 1) },
                { Resource.Stone, 50 * (level + 1) }
            };
        }
    }

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, City city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;

        return map.VertexHasTerrainType(city.Position, TerrainType.Water);
    }
}