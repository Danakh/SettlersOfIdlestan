using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Barracks : Building
{
    /// <summary>Nombre de soldats actuellement en garnison.</summary>
    public int Soldiers { get; set; } = 0;

    /// <summary>Tick de la dernière production de soldat. 0 = jamais produit.</summary>
    public long LastSoldierProductionTick { get; set; } = 0;

    public Barracks() : base(BuildingType.Barracks)
    {
        AvailableAtLevel = 1;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Locked by default; unlocked by the Barracks prestige vertex (+2 max level)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Food, 20 },
        { Resource.Wood, 20 },
        { Resource.Stone, 50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Food, 20 * (level + 1)},
        { Resource.Wood, 20 * (level + 1) },
        { Resource.Stone, 50 * (level + 1) },
    };
}
