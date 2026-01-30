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
        Production.Add(Resource.Wood, 1);
        Production.Add(Resource.Brick, 1);
        Production.Add(Resource.Sheep, 1);
        Production.Add(Resource.Wheat, 1);
        Production.Add(Resource.Ore, 1);
        MaxLevel = 4;
        Description = "Port maritime - Permet le commerce maritime (3:1), nécessite de l'eau. Disponible au niveau Ville (2). Niveau 4 débloque l'action Prestige.";
        RequiresWater = true;
        AvailableAtLevel = 2;
        Actions.Add("Prestige");
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new Dictionary<Resource, int>
    {
        { Resource.Wood, 1 },
        { Resource.Brick, 1 },
        { Resource.Sheep, 1 },
        { Resource.Wheat, 1 }
    };
}