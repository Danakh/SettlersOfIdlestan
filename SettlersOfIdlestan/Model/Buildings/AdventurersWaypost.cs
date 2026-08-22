using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Buildings;

/// <summary>
/// Relais des Aventuriers — constructible une fois la Guilde des Aventuriers bâtie quelque part
/// dans la civilisation (voir <see cref="HasBuildPrerequisites(IBuildingContext, WorldState)"/>).
/// Un exemplaire est accordé automatiquement dans la ville de la Guilde à sa construction (voir
/// BuildingController.BuildBuilding) ; d'autres peuvent ensuite être bâtis dans n'importe quelle
/// ville. Chaque Relais fait apparaître un Aventurier qui ne s'éloigne jamais de plus de
/// <see cref="AdventurerRoamRadiusHexes"/> hexs de lui (voir MonsterFeatureController) ; toute la
/// mécanique de réapparition — auparavant portée par la Guilde — vit ici.
/// </summary>
public class AdventurersWaypost : Building
{
    public const long AdventurerRespawnCooldownTicks = 2_000L;

    /// <summary>Distance maximale (en hexs) que l'Aventurier invoqué par ce Relais peut s'en éloigner.</summary>
    public const int AdventurerRoamRadiusHexes = 2;

    /// <summary>Tick de la mort du dernier Aventurier invoqué par ce Relais, pour le délai de réapparition.</summary>
    public long LastAdventurerDeathTick { get; set; }

    /// <summary>
    /// Nombre de Relais déjà construits dans la civilisation avant celui-ci, pour le coût progressif
    /// (voir GetBuildCost). Renseigné par BuildingController juste avant l'appel à GetBuildCost() —
    /// ni sérialisé, ni pertinent une fois le Relais construit.
    /// </summary>
    public int PriorWaypostCount { get; set; }

    public AdventurersWaypost() : base(BuildingType.AdventurersWaypost)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 1;

    public override bool IsAvailableInLayer(int z) => z != IslandMap.IslandMap.SurfaceLayer;

    public override bool IsBuildingAvailableForCity(IslandMap.IslandMap map, IBuildingContext city)
        => IsAvailableInLayer(map.Z) && base.IsBuildingAvailableForCity(map, city);

    /// <summary>
    /// La Guilde doit être bâtie quelque part dans la civilisation — pas nécessairement dans cette
    /// ville : c'est ce qui permet d'ouvrir des Relais dans d'autres villes (voir
    /// building_adventurersguild_desc).
    /// </summary>
    public override bool HasBuildPrerequisites(IBuildingContext city, WorldState state)
    {
        var owner = state.FindCityAt(city.Position);
        var civ = owner != null ? state.GetCivilization(owner.CivilizationIndex) : null;
        return civ != null && civ.Cities.Any(c => c.FindBuilding(BuildingType.AdventurersGuild) is { Level: > 0 });
    }

    public override string? GetMissingPrerequisiteKey(IBuildingContext city, WorldState state)
        => HasBuildPrerequisites(city, state) ? null : "tooltip_requires_adventurersguild";

    /// <summary>Coût = coût de base de la Guilde des Aventuriers × (0,5 + 0,5 × PriorWaypostCount).</summary>
    public override ResourceSet GetBuildCost()
    {
        var guildCost = new AdventurersGuild().GetBuildCost();
        double multiplier = 0.5 + 0.5 * PriorWaypostCount;

        var scaled = new ResourceSet();
        foreach (var (resource, amount) in guildCost)
            scaled.Add(resource, System.Math.Max(1, (int)System.Math.Round(amount * multiplier)));
        return scaled;
    }
}
