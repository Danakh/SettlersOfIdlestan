using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Guilde des Aventuriers — bâtiment unique de l'Inframonde. Fait apparaître un Aventurier qui
/// patrouille et combat les monstres errants (jamais les villes) ; quand il meurt, un autre prend
/// sa place après un court délai.
/// </summary>
public class AdventurersGuild : Building
{
    public const long AdventurerRespawnCooldownTicks = 500L;

    /// <summary>Tick de la dernière invocation d'un Aventurier, pour le délai de réapparition.</summary>
    public long LastAdventurerSpawnTick { get; set; }

    public AdventurersGuild() : base(BuildingType.AdventurersGuild)
    {
        AvailableAtLevel = 1;
    }

    public override bool IsUnique => true;

    public override int GetDefaultMaxLevel() => 0;

    public override bool IsAvailableInLayer(int z) => z != IslandMap.IslandMap.SurfaceLayer;

    public override ResourceSet GetBuildCost() => new ResourceSet
    {
        { Resource.Mithril, 100 },
    };

    public override ResourceSet GetUpgradeCost(int level) => new ResourceSet();
}
