using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Forge building.
/// </summary>
public class Forge : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Forge"/> class.
    /// </summary>
    public Forge() : base(BuildingType.Forge)
    {
        AvailableAtLevel = 2;
    }

    public override int GetDefaultMaxLevel() => 4;

    public override Resource? AutomaticHarvestResource => Resource.Ore;

    public override int AutomaticHarvestUnlockLevel => 2;

    /// <summary>
    /// Chance (en %) de produire une seconde ressource lors d'une récolte automatique adjacente à cette ville.
    /// </summary>
    public int DoubleProdChancePercent => Level * 10;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.Mountain)
            return Resource.Ore;
        return null;
    }

    public override ResourceCost GetBuildCost() => new ResourceCost
    {
        { Resource.Brick, 5 },
        { Resource.Stone, 20 },
        { Resource.Ore, 5 }
    };

    public override ResourceCost GetUpgradeCost(int level) => new ResourceCost
    {
        { Resource.Brick, 5 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) },
        { Resource.Ore, 5 * (level + 1) }
    };
}