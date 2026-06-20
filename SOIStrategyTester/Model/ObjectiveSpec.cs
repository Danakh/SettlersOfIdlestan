using SettlersOfIdlestan.Model.Buildings;

namespace SOIStrategyTester.Model;

/// <summary>
/// The stopping conditions used throughout SOITests' FullIslandScenarios, reified as data so the
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
