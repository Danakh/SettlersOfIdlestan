using System;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandFeatures;
using SOIStrategyTester.Model;

namespace SOIStrategyTester;

/// <summary>
/// Evaluates an ObjectiveSpec against the live game state. Mirrors the Condition lambdas in
/// SOITests/IslandMapTests/StepIslandTest/StepIslandScenarios.cs so the same stopping conditions can
/// be expressed as data and reused both as a run's global objective and as a phase's "Until".
/// </summary>
public static class ObjectiveEvaluator
{
    public static bool Evaluate(ObjectiveSpec spec, MainGameController controller)
    {
        var worldState = controller.CurrentMainState?.CurrentWorldState
            ?? throw new InvalidOperationException("Controller has no current world state.");
        var civ = worldState.Civilizations.First(c => !c.IsNpc);

        switch (spec.Kind)
        {
            case ObjectiveKind.CityCountWithBuilding:
                return civ.Cities.Count >= Require(spec.CityCount, nameof(spec.CityCount))
                    && civ.Cities.All(c => c.Buildings.Any(b => b.Type == Require(spec.RequiredBuilding, nameof(spec.RequiredBuilding))));

            case ObjectiveKind.CityCount:
                return civ.Cities.Count >= Require(spec.CityCount, nameof(spec.CityCount));

            case ObjectiveKind.PrestigePointsAtLeast:
                return controller.PrestigeController.CalculatePrestigePoints() >= Require(spec.Points, nameof(spec.Points));

            case ObjectiveKind.PrestigeAvailable:
                return controller.PrestigeController.PrestigeIsAvailable();

            case ObjectiveKind.NoSurfaceMonsters:
                return !controller.PrestigeController.HasSurfaceMonsters();

            case ObjectiveKind.NoEnemyCivilizations:
                return worldState.Civilizations.Where(c => c.IsNpc).All(c => c.Cities.Count == 0);

            case ObjectiveKind.WonderPlaced:
                {
                    var wonder = worldState.Features.OfType<Wonder>().FirstOrDefault();
                    return wonder != null && wonder.InvestmentEnabled.Count > 0;
                }

            case ObjectiveKind.WonderLevelAtLeast:
                {
                    var wonder = worldState.Features.OfType<Wonder>().FirstOrDefault();
                    return wonder != null && wonder.Level >= Require(spec.Level, nameof(spec.Level));
                }

            case ObjectiveKind.PrestigeRunCountAtLeast:
                return (controller.CurrentMainState?.PrestigeState?.RunHistory.Count ?? 0) >= Require(spec.Count, nameof(spec.Count));

            case ObjectiveKind.UniqueBuildingPresent:
                return civ.UniqueBuildings.Contains(Require(spec.RequiredBuilding, nameof(spec.RequiredBuilding)));

            case ObjectiveKind.AllCitiesBuildingAtLeast:
                {
                    var requiredBuilding = Require(spec.RequiredBuilding, nameof(spec.RequiredBuilding));
                    var level = Require(spec.Level, nameof(spec.Level));
                    return civ.Cities.All(c =>
                    {
                        var building = controller.BuildingController.GetBuildingOrBuildable(c, requiredBuilding);
                        if (building == null) return true;
                        var maxLevel = controller.BuildingController.GetMaxLevel(building, c.CivilizationIndex);
                        return building.Level >= Math.Min(level, maxLevel);
                    });
                }

            default:
                throw new NotSupportedException($"Unknown objective kind: {spec.Kind}");
        }
    }

    private static T Require<T>(T? value, string paramName) where T : struct
        => value ?? throw new ArgumentException($"Objective is missing required field '{paramName}'.");
}
