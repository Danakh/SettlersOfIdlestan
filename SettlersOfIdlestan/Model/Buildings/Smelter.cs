using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Smelter : Building
{
    public const long ProductionCooldownTicks = 1000L; // 10 s
    public const int OreInputPerCycle  = 5;
    public const int WoodInputPerCycle = 2;
    public const int SteelOutputPerCycle = 1;

    public long LastProductionTick { get; set; } = 0;

    public Smelter() : base(BuildingType.Smelter)
    {
        AvailableAtLevel = 3;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillé par défaut ; débloqué par le vertex de prestige Secret de l'Acier (+2 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 60 },
        { Resource.Brick, 40 },
        { Resource.Ore,   20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 30 * (level + 1) },
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Ore,   15 * (level + 1) },
    };
}
