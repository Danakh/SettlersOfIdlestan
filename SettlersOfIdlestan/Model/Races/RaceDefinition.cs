using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Races;

/// <summary>
/// Définition statique d'une race jouable : restrictions de placement, modifiers de gameplay et
/// bâtiment unique racial. Les modifiers sont émis par AscensionController.GetModifiers() quand la
/// race est jouée (AscensionState.SelectedRace) ; le bâtiment racial rejoint définitivement les
/// choix de bâtiment permanent une fois une Ascension effectuée avec cette race (AscendedRaces).
/// </summary>
public class RaceDefinition
{
    public RaceId Id { get; }
    public string NameKey { get; }
    public string DescKey { get; }
    public RaceTier Tier { get; }

    /// <summary>
    /// Terrain dont au moins un hex doit toucher tout nouveau vertex de ville en surface (null =
    /// aucune restriction). Consommé via le modifier CITY_PLACEMENT_REQUIRES_TERRAIN — la ville de
    /// départ (posée par le générateur) et les avant-postes de couche en sont exemptés.
    /// </summary>
    public TerrainType? RequiredAdjacentTerrain { get; }

    /// <summary>
    /// Bâtiment unique racial, constructible uniquement en jouant cette race (la race fournit son
    /// BUILDING_MAX_LEVEL +1, les prototypes partant de 0 — même patron que les uniques de prestige).
    /// </summary>
    public BuildingType? RacialBuilding { get; }

    public IReadOnlyList<Modifier> Modifiers { get; }

    public RaceDefinition(
        RaceId id,
        RaceTier tier,
        TerrainType? requiredAdjacentTerrain,
        BuildingType? racialBuilding,
        Modifier[] modifiers)
    {
        Id = id;
        Tier = tier;
        RequiredAdjacentTerrain = requiredAdjacentTerrain;
        RacialBuilding = racialBuilding;
        Modifiers = modifiers;
        NameKey = $"race_{id.ToString().ToLowerInvariant()}_name";
        DescKey = $"race_{id.ToString().ToLowerInvariant()}_desc";
    }
}
