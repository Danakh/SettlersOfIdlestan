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
        Production.Add(Resource.Ore, 1);
        MaxLevel = 4;
        Description = "Forge - Améliore la production de minerai et permet la création d'outils";
        RequiresWater = false;
        AvailableAtLevel = 1;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 2 },
        { Resource.Brick, 2 },
        { Resource.Ore, 2 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Ore, 1 }
    };
}