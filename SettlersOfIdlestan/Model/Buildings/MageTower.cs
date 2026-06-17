using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Tour de Mages — le nombre de tours limite le nombre de rituels actifs simultanés,
/// la somme de leurs niveaux limite la puissance totale des rituels.
/// Extrait aussi automatiquement des cristaux des Grottes de Cristal adjacentes (Inframonde).
/// Verrouillée par défaut ; débloquée par le vertex de prestige Secret de la Magie.
/// </summary>
public class MageTower : Building
{
    public MageTower() : base(BuildingType.MageTower)
    {
        AvailableAtLevel = 4;
    }

    // Verrouillé par défaut ; débloqué par le vertex de prestige Secret de la Magie (+3 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override Resource? AutomaticHarvestResource => Resource.Crystal;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.CrystalCave)
            return Resource.Crystal;
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone,   60 },
        { Resource.Glass,    5 },
        { Resource.Crystal,  5 },
        { Resource.Gold,    30 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone,   40 * (level + 1) },
        { Resource.Glass,    5 * (level + 1) },
        { Resource.Crystal,  5 * (level + 1) },
        { Resource.Gold,    20 * (level + 1) },
    };
}
