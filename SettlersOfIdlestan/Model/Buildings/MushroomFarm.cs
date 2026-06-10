using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Ferme fongique — produit automatiquement de la nourriture sur les Cavernes aux
/// Champignons adjacentes (Inframonde). Aucune récolte manuelle possible.
/// Verrouillée par défaut ; débloquée par le vertex de prestige Cultures Fongiques.
/// </summary>
public class MushroomFarm : Building
{
    public MushroomFarm() : base(BuildingType.MushroomFarm)
    {
        AvailableAtLevel = 1;
    }

    // Verrouillé par défaut ; débloqué par le vertex de prestige Cultures Fongiques (+2 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override Resource? AutomaticHarvestResource => Resource.Food;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.MushroomCave)
            return Resource.Food;
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Wood,  40 },
        { Resource.Stone, 20 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Wood,  40 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) },
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.MushroomCave);
    }
}
