using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

public class Arsenal : Building
{
    public const int MaxSoldiersPerLevel = 4;

    /// <summary>Chance (en %) de base de sauver un soldat perdu en consommant 1 Acier (Armures d'Acier), sans Arsenal.</summary>
    public const int ArmorSaveBasePercent = 35;

    /// <summary>Chance (en %) additionnelle par niveau d'Arsenal de sauver un soldat perdu dans cette ville.</summary>
    public const int ArmorSavePercentPerLevel = 5;

    /// <summary>Soldats produits par cycle par un Arsenal actif, pour <see cref="SteelInputPerCycle"/> Acier consommé — voir SoldierProductionEngine.ProduceArsenalSoldiers. Nécessite le vertex de prestige Production Accélérée (UNLOCK_ARSENAL_PRODUCTION).</summary>
    public const int SoldiersProducedPerCycle = 2;

    /// <summary>Acier consommé par cycle de production de soldats.</summary>
    public const int SteelInputPerCycle = 1;

    public Arsenal() : base(BuildingType.Arsenal)
    {
        AvailableAtLevel = 3;
        // Activable comme les autres bâtiments de production (Caserne, Fonderie, ...) ; la production
        // elle-même reste gatée par le vertex de prestige Production Accélérée (UNLOCK_ARSENAL_PRODUCTION),
        // voir SoldierProductionEngine.ProduceArsenalSoldiers.
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
