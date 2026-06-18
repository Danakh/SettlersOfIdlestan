using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Forge d'Armures — produit automatiquement des Armures en Acier en consommant de l'Acier.
/// Verrouillée par défaut ; débloquée par la recherche Armures d'Acier (+2 niveaux max).
/// </summary>
public class ArmorSmith : Building
{
    public const long ProductionCooldownTicks = 1000L; // 10 s
    public const int SteelInputPerArmor = 3;

    public long LastProductionTick { get; set; } = 0;

    public ArmorSmith() : base(BuildingType.ArmorSmith)
    {
        AvailableAtLevel = 3;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillée par défaut ; débloquée par la recherche Armures d'Acier (+2 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 60 },
        { Resource.Brick, 30 },
        { Resource.Steel, 20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 35 * (level + 1) },
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Steel, 12 * (level + 1) },
    };
}
