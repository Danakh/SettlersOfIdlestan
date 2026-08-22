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
/// mécanique de réapparition — auparavant portée par la Guilde — vit ici. Le niveau du Relais
/// (jusqu'à <see cref="GetDefaultMaxLevel"/>) détermine désormais la puissance de l'Aventurier
/// qu'il invoque — auparavant porté par le niveau de la Guilde (voir MonsterController.UpdateAdventurerSpawns).
/// </summary>
public class AdventurersWaypost : Building
{
    public const long AdventurerRespawnCooldownTicks = 2_000L;

    /// <summary>Distance maximale (en hexs) que l'Aventurier invoqué par ce Relais peut s'en éloigner.</summary>
    public const int AdventurerRoamRadiusHexes = 1;

    /// <summary>Tick de la mort du dernier Aventurier invoqué par ce Relais, pour le délai de réapparition.</summary>
    public long LastAdventurerDeathTick { get; set; }

    /// <summary>
    /// Nombre de Relais déjà construits dans la civilisation avant celui-ci, pour le coût progressif
    /// (voir GetBuildCost). Renseigné par BuildingController juste avant l'appel à GetBuildCost() —
    /// ni sérialisé, ni pertinent une fois le Relais construit.
    /// </summary>
    public int PriorWaypostCount { get; set; }

    /// <summary>
    /// Multiplicateur de coût figé au moment de la construction du niveau 1 (voir GetBuildCost),
    /// réappliqué à l'amélioration (voir GetUpgradeCost) pour que le coût d'un Relais tardif — plus
    /// cher à construire — reste proportionnellement plus cher à améliorer. Valeur par défaut
    /// (0,5, cas PriorWaypostCount = 0) pour le Relais gratuit accordé avec la Guilde, qui ne passe
    /// jamais par GetBuildCost (voir BuildingController.BuildBuilding).
    /// </summary>
    public double BuildCostMultiplier { get; set; } = 0.5;

    public AdventurersWaypost() : base(BuildingType.AdventurersWaypost)
    {
        AvailableAtLevel = 1;
    }

    public override int GetDefaultMaxLevel() => 4;

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

    /// <summary>
    /// Coût = coût de base de la Guilde des Aventuriers × (0,5 + 0,5 × PriorWaypostCount). Le
    /// multiplicateur est figé dans <see cref="BuildCostMultiplier"/> pour être réappliqué à
    /// l'amélioration (voir GetUpgradeCost).
    /// </summary>
    public override ResourceSet GetBuildCost()
    {
        var guildCost = new AdventurersGuild().GetBuildCost();
        BuildCostMultiplier = 0.5 + 0.5 * PriorWaypostCount;

        var scaled = new ResourceSet();
        foreach (var (resource, amount) in guildCost)
            scaled.Add(resource, System.Math.Max(1, (int)System.Math.Round(amount * BuildCostMultiplier)));
        return scaled;
    }

    /// <summary>Coût de base scalé par <see cref="BuildCostMultiplier"/>, figé à la construction du niveau 1.</summary>
    public override ResourceSet GetUpgradeCost(int level)
    {
        var baseCost = new ResourceSet
        {
            { Resource.Mithril, 100 * level },
            { Resource.Stone, 200 * level },
            { Resource.Steel, 100 * level },
            { Resource.Food, 100 * level },
        };

        var scaled = new ResourceSet();
        foreach (var (resource, amount) in baseCost)
            scaled.Add(resource, System.Math.Max(1, (int)System.Math.Round(amount * BuildCostMultiplier)));
        return scaled;
    }
}
