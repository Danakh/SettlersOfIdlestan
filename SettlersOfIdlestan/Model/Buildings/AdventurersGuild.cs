using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Guilde des Aventuriers — bâtiment unique de l'Inframonde, plafonné à un seul niveau (niveau 0
/// tant que le vertex de prestige ne la débloque pas, voir PrestigeMapFactory). Ne fait plus
/// apparaître d'Aventurier elle-même : elle débloque le Relais des Aventuriers
/// (<see cref="AdventurersWaypost"/>) et en accorde automatiquement un dans sa propre ville à la
/// construction (voir BuildingController.BuildBuilding). La puissance des Aventuriers et la
/// mécanique de réapparition vivent désormais sur chaque Relais, pas sur la Guilde — voir
/// AdventurersWaypost.Level.
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

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city, Model.Civilization.Civilization? civ)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city, civ);

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Mithril, 100 },
        { Resource.Stone, 200 },
        { Resource.Steel, 100 },
        { Resource.Food, 100 },
    };
}
