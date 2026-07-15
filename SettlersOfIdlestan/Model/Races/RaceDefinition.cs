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
    /// aucune restriction). Consommé via le modifier CITY_PLACEMENT_REQUIRES_TERRAIN — les
    /// avant-postes de couche en sont exemptés ; la ville de départ le respecte via
    /// <see cref="StartVertexTerrain"/> (le générateur adapte les terrains initiaux).
    /// </summary>
    public TerrainType? RequiredAdjacentTerrain { get; }

    /// <summary>
    /// Terrain accompagnant la Forêt sur le vertex de départ garanti par le générateur (voir
    /// IslandMapGenerator.EnsureStartPairNearEdge) : la Colline par défaut, remplacée par le
    /// terrain requis de la race quand il y en a un (Montagne pour les Nains — le vertex de départ
    /// devient Montagne/Forêt/Eau ; la brique manquante s'achète au Marché offert par le vertex
    /// central de la carte de prestige). La Forêt et l'Eau restent inchangées (Elfes, Sirènes).
    /// </summary>
    public TerrainType StartVertexTerrain =>
        RequiredAdjacentTerrain is { } terrain && terrain != TerrainType.Forest && terrain != TerrainType.Water
            ? terrain
            : TerrainType.Hill;

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
