using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Guilde des Aventuriers — bâtiment unique de l'Inframonde. Ne fait plus apparaître d'Aventurier
/// elle-même : elle débloque le Relais des Aventuriers (<see cref="AdventurersWaypost"/>) et en
/// accorde automatiquement un dans sa propre ville à la construction (voir
/// BuildingController.BuildBuilding). Son niveau détermine la puissance des Aventuriers invoqués
/// par tous les Relais de la civilisation ; la mécanique de réapparition vit désormais sur chaque
/// Relais, pas sur la Guilde.
/// </summary>
public class AdventurersGuild : Building
{
    public AdventurersGuild() : base(BuildingType.AdventurersGuild)
    {
        AvailableAtLevel = 1;
    }

    public override bool IsUnique => true;

    public override int GetDefaultMaxLevel() => 0;

    public override bool IsAvailableInLayer(int z) => z != IslandMap.IslandMap.SurfaceLayer;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city);

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Mithril, 100 },
        { Resource.Stone, 200 },
        { Resource.Steel, 100 },
        { Resource.Food, 100 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet
    {
        { Resource.Mithril, 100 * level },
        { Resource.Stone, 200 * level },
        { Resource.Steel, 100 * level },
        { Resource.Food, 100 * level },
    };
}
