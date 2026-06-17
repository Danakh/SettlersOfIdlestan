using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Warehouse building.
/// </summary>
public class Warehouse : Building
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Warehouse"/> class.
    /// </summary>
    public Warehouse() : base(BuildingType.Warehouse)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel()
    {
        return 2;
    }

    /// <summary>Bonus de capacité de stockage (ressources de base) : +20 à la construction, +10 par niveau.</summary>
    public override int GetStorageCapacityBonusBasic() => GetStorageCapacityBonusBasicAtLevel(Level);

    public int GetStorageCapacityBonusBasicAtLevel(int level) => level <= 0 ? 0 : 20 + 10 * level;

    /// <summary>Bonus de capacité de stockage (ressources avancées) : +5 à la construction, +5 par niveau.</summary>
    public override int GetStorageCapacityBonusAdvanced() => GetStorageCapacityBonusAdvancedAtLevel(Level);

    public int GetStorageCapacityBonusAdvancedAtLevel(int level) => level <= 0 ? 0 : 5 + 5 * level;

    /// <summary>
    /// Gets the build cost of the warehouse.
    /// </summary>
    /// <returns>A dictionary containing the resources and their quantities required for building.</returns>
    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood, 10 },
        { Resource.Brick, 20 },
    };

    /// <summary>
    /// Gets the upgrade cost for the warehouse.
    /// </summary>
    /// <param name="level">The current level of the warehouse.</param>
    /// <returns>A dictionary containing the resources and their quantities required for upgrading.</returns>
    public override ResourceSet GetUpgradeCost(int level)
    {
        var result = new ResourceSet
        {
            { Resource.Wood, 10 * (level + 1) },
            { Resource.Brick, 20 * (level + 1) }
        };
        if (level > 1)
        {
            result.Add(Resource.Stone, 10 * (level + 1));
        }
        if (level > 3)
        {
            result.Add(Resource.Ore, 10 * (level + 1));
        }
        return result;
    }
}