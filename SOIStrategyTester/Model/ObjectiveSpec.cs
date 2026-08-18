using SettlersOfIdlestan.Model.Buildings;

namespace SOIStrategyTester.Model;

/// <summary>
/// The stopping conditions used throughout SOITests' StepIslandScenarios, reified as data so the
/// same condition can be supplied either as the run's global objective or as a phase's "Until"
/// (the point where a strategy switches to its next phase).
/// </summary>
public enum ObjectiveKind
{
    /// <summary>civ.Cities.Count >= CityCount, and every city has at least one RequiredBuilding.</summary>
    CityCountWithBuilding,
    /// <summary>civ.Cities.Count >= CityCount.</summary>
    CityCount,
    /// <summary>PrestigeController.CalculatePrestigePoints() >= Points.</summary>
    PrestigePointsAtLeast,
    /// <summary>PrestigeController.PrestigeIsAvailable().</summary>
    PrestigeAvailable,
    /// <summary>!PrestigeController.HasSurfaceMonsters().</summary>
    NoSurfaceMonsters,
    /// <summary>Every NPC civilization has zero cities left.</summary>
    NoEnemyCivilizations,
    /// <summary>The Wonder has been placed and has investment enabled.</summary>
    WonderPlaced,
    /// <summary>The Wonder's level is at least Level.</summary>
    WonderLevelAtLeast,
    /// <summary>PrestigeState.RunHistory.Count >= Count (i.e. the Nth prestige transition has happened).</summary>
    PrestigeRunCountAtLeast,
    /// <summary>civ.UniqueBuildings.Contains(RequiredBuilding) — the (unique) building has been built
    /// somewhere, regardless of city. Useful as a Priority phase's "Until" when priming a UniqueBuilding
    /// objective (e.g. ArtisansGuild/HarvestersGuild/TraderGuild) before switching to another phase.</summary>
    UniqueBuildingPresent,
    /// <summary>Every city has RequiredBuilding at level >= Level, or the building is unavailable/maxed
    /// for that city (which counts as done — mirrors StepIslandScenarios' IsBuildingAtLeastLevelForCity,
    /// already used for the Library checkpoint). Useful as a Priority phase's "Until" when priming a
    /// normal (non-unique) BuildingLevel objective, e.g. Warehouse, before switching to another phase.</summary>
    AllCitiesBuildingAtLeast,
    /// <summary>The Abyss Gate has been built — either right now on the live island (AbyssGate.Built)
    /// or ever, cross-prestige (GameRecord.HasBuiltAbyssGate, which is only stamped at the next
    /// prestige after building it). The live check makes this trigger the moment the Gate finishes,
    /// without waiting for the run to prestige again.</summary>
    AbyssGateUnlocked,
    /// <summary>Plus aucun DemonGod vivant sur la couche Pandémonium — la victoire de la manche
    /// end-game (voir PandemoniumRunner). Vrai aussi tant que le Pandémonium n'est pas ouvert, donc à
    /// n'utiliser que sur un état qui l'a déjà : c'est le cas de tout ce que produit
    /// EndGameStateFactory.</summary>
    DemonGodDefeated,
    /// <summary>Il reste au plus Count Tentacules vivantes dans le Pandémonium. Sert d'étape
    /// intermédiaire : « les huit sont tombées » (Count = 0) est le vrai préalable au boss, et le
    /// distinguer de la victoire dit si une manche perdue l'a été sur les gardes ou sur le centre.</summary>
    TentaclesRemainingAtMost,
}

public class ObjectiveSpec
{
    public ObjectiveKind Kind { get; set; }

    public int? CityCount { get; set; }
    public BuildingType? RequiredBuilding { get; set; }
    public int? Points { get; set; }
    public int? Level { get; set; }
    public int? Count { get; set; }
}
