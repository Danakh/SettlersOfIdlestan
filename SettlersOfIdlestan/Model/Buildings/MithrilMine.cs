using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Mine de Mithril — extrait automatiquement du Mithril des Filons adjacents (Inframonde).
/// Verrouillée par défaut ; débloquée par le vertex de prestige Le Mithril.
/// </summary>
public class MithrilMine : Building
{
    public MithrilMine() : base(BuildingType.MithrilMine)
    {
        AvailableAtLevel = 2;
    }

    // Verrouillé par défaut ; débloqué par le vertex de prestige Le Mithril (+2 niveaux max)
    public override int GetDefaultMaxLevel() => 0;

    public override Resource? AutomaticHarvestResource => Resource.Mithril;
    public override int AutomaticHarvestUnlockLevel => 1;

    public override Resource? AutomaticHarvestCapability(TerrainType terrain)
    {
        if (terrain == TerrainType.MithrilVein)
            return Resource.Mithril;
        return null;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Stone, 80 },
        { Resource.Steel, 10 },
        { Resource.Gold,  50 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Stone, 60 * (level + 1) },
        { Resource.Steel, 10 * (level + 1) },
        { Resource.Gold,  40 * (level + 1) },
    };

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        if (!base.IsBuildingAvailableForCity(map, city))
            return false;
        return map.VertexHasTerrainType(city.Position, TerrainType.MithrilVein);
    }
}
