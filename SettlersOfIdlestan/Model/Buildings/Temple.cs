using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Represents a Temple building. Aux niveaux 2-4 (atteignables uniquement une fois le pouvoir divin
/// Foi débloqué, voir AscensionController.GetModifiers — BUILDING_MAX_LEVEL "Temple" +3), génère du
/// Dominion ou réduit la Corruption autour de sa ville (voir CorruptionController.ProcessTempleProduction).
/// </summary>
public class Temple : Building
{
    /// <summary>Dernier tick où ce Temple a agi sur la Corruption/le Dominion d'un hex voisin.</summary>
    public long LastDominionProductionTick { get; set; } = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="Temple"/> class.
    /// </summary>
    public Temple() : base(BuildingType.Temple)
    {
        AvailableAtLevel = 2;
    }

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Brick, 20 },
        { Resource.Stone, 20 }
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Brick, 20 * (level + 1) },
        { Resource.Stone, 20 * (level + 1) }
    };

    public override bool IsAvailableInLayer(int z) => z == IslandMap.IslandMap.SurfaceLayer;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
    {
        return IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city);
    }
}