using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Arsenal : Building
{
    public const int MaxSoldiersPerLevel = 4;

    /// <summary>Chance (en %) de base de sauver un soldat perdu en consommant 1 Acier (Armures d'Acier), sans Arsenal.</summary>
    public const int ArmorSaveBasePercent = 35;

    /// <summary>Chance (en %) additionnelle par niveau d'Arsenal de sauver un soldat perdu dans cette ville.</summary>
    public const int ArmorSavePercentPerLevel = 5;

    public Arsenal() : base(BuildingType.Arsenal)
    {
        AvailableAtLevel = 3;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillé par défaut ; débloqué par le vertex de prestige Génie Militaire (+3 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override int GetMaxSoldiersBonus() => MaxSoldiersPerLevel * Level;

    /// <summary>Chance (en %) de sauver un soldat perdu, pour le niveau actuel.</summary>
    public int ArmorSavePercent => ArmorSaveBasePercent + ArmorSavePercentPerLevel * Level;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 100 },
        { Resource.Brick,  80 },
        { Resource.Steel,  20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 60 * (level + 1) },
        { Resource.Brick, 40 * (level + 1) },
        { Resource.Steel, 15 * (level + 1) },
    };
}
