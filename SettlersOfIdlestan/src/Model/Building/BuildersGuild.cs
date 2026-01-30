using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Builders Guild building.
/// </summary>
public class BuildersGuild : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuildersGuild"/> class.
    /// </summary>
    public BuildersGuild() : base(BuildingType.BuildersGuild)
    {
        MaxLevel = 1;
        Description = "Guilde des batisseurs - Permet l'automatisation de constructions. Disponible au niveau Capitale (4).";
        RequiresWater = false;
        AvailableAtLevel = 4;
    }

    public override Dictionary<Resource, int> GetBuildCost() => new Dictionary<Resource, int>
    {
        { Resource.Wood, 3 },
        { Resource.Brick, 2 },
        { Resource.Ore, 2 }
    };

    public override Dictionary<Resource, int> GetUpgradeCost(int level) => new();
}