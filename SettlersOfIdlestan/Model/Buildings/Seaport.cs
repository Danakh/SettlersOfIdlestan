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

    public override Resource? ManualHarvestResource => Resource.Food;
    public override Resource? AutomaticHarvestResource => Resource.Food;

    public override Resource? ManualHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Water)
            return Resource.Food;
        return null;
    }

    public override int AutomaticHarvestUnlockLevel => 2;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if ((Level > 1) && (terrain == TerrainType.Water))
            return Resource.Food;
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 10 },
    };

    public override ResourceSet GetUpgradeCost(int level)
    {
        return new ResourceSet
        {
            { Resource.Food, 5 * (level + 1) },
            { Resource.Wood, 10 * (level + 1) },
        };
    }

    public long LastGenerationTick { get; set; } = 0;

    // Chaque niveau au-delà de 3 multiplie le temps de génération par 0.8.
    public double GetGenerationCooldownMultiplier() =>
        Level >= 3 ? Math.Pow(0.8, Level - 3) : 1.0;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;

        return map.VertexHasTerrainType(city.Position, TerrainType.Water);
    }
}