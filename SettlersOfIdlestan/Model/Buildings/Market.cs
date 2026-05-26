using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Market building.
/// </summary>
public class Market : Building
{
    /// <summary>
    /// Tick de la dernière génération de ressource. 0 = jamais généré.
    /// </summary>
    public long LastGenerationTick { get; set; } = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Market"/> class.
    /// </summary>
    public Market() : base(BuildingType.Market)
    {
        AvailableAtLevel = 1;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Food, 5 },
        { Resource.Wood, 5 },
        { Resource.Brick, 5 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new();
}