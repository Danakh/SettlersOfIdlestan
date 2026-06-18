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

    /// <summary>
    /// Chance (en %) de produire une seconde ressource lors d'une récolte automatique adjacente à cette ville.
    /// </summary>
    public int DoubleProdChancePercent => Level * 10;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 40 },
        { Resource.Stone, 20 },
        { Resource.Ore, 20 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Brick, 40 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) },
        { Resource.Ore, 10 * (level * (level + 1)) }
    };
}