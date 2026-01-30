using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Town Hall building.
/// </summary>
public class TownHall : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TownHall"/> class.
    /// </summary>
    public TownHall() : base(BuildingType.TownHall)
    {
        MaxLevel = 4;
        Description = "Hôtel de ville - Permet l'amélioration de la ville";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 2 },
        { Resource.Brick, 2 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 },
        { Resource.Ore, 1 }
    };
}