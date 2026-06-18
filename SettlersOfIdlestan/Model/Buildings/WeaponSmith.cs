using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Forge d'Armes — produit automatiquement des Armes en Acier en consommant de l'Acier.
/// Verrouillée par défaut ; débloquée par la recherche Armes en Acier (+2 niveaux max).
/// </summary>
public class WeaponSmith : Building
{
    public const long ProductionCooldownTicks = 1000L; // 10 s
    public const int SteelInputPerWeapon = 2;

    public long LastProductionTick { get; set; } = 0;

    public WeaponSmith() : base(BuildingType.WeaponSmith)
    {
        AvailableAtLevel = 3;
        ActivationStatus = ActivationStatus.ACTIVE;
    }

    // Verrouillée par défaut ; débloquée par la recherche Armes en Acier (+2 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 50 },
        { Resource.Brick, 30 },
        { Resource.Steel, 15 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 30 * (level + 1) },
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Steel, 10 * (level + 1) },
    };
}
