using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;

namespace SOITests.TestUtilities;

/// <summary>
/// One stage of a hand-tuned Priority strategy: either "build these to level N in every city" or
/// "expand to city count N". These orderings are found offline by racing variants through
/// SOIStrategyTester (see SOIStrategyTester/Data/Best/island1-*.best.json for the winning sequences) and
/// then translated here, one PriorityStage per StrategyPhase.priorityObjectives entry.
/// </summary>
public abstract class PriorityStage
{
    public static PriorityStage Buildings(BuildingType[] buildings, int targetLevel) => new BuildingsStage(buildings, targetLevel);
    public static PriorityStage Cities(int targetCount) => new CitiesStage(targetCount);
    public static PriorityStage ImperialPort() => new ImperialPortStage();

    /// <summary>
    /// Same as <see cref="Buildings"/>, except it's skipped entirely until at least one <see cref="Bandit"/>
    /// has been spotted (<see cref="SettlersOfIdlestan.Model.IslandFeatures.IslandFeature.Found"/>) on the
    /// surface — used to put up a Palisade only once bandits are actually a threat, rather than wasting
    /// build time on it from turn one.
    /// </summary>
    public static PriorityStage BuildingsIfBanditSpotted(BuildingType[] buildings, int targetLevel) => new ConditionalBuildingsStage(buildings, targetLevel);

    internal abstract IAutoplayObjective ToObjective(CivilizationAutoplayer autoplayer, BuildingController buildingController);

    private sealed class BuildingsStage : PriorityStage
    {
        private readonly BuildingType[] _buildings;
        private readonly int _targetLevel;

        public BuildingsStage(BuildingType[] buildings, int targetLevel)
        {
            _buildings = buildings;
            _targetLevel = targetLevel;
        }

        internal override IAutoplayObjective ToObjective(CivilizationAutoplayer autoplayer, BuildingController buildingController) =>
            new BuildingLevelObjective(autoplayer, buildingController, _buildings, _targetLevel);
    }

    private sealed class CitiesStage : PriorityStage
    {
        private readonly int _targetCount;

        public CitiesStage(int targetCount)
        {
            _targetCount = targetCount;
        }

        internal override IAutoplayObjective ToObjective(CivilizationAutoplayer autoplayer, BuildingController buildingController) =>
            new CityCountObjective(autoplayer, _targetCount);
    }

    private sealed class ImperialPortStage : PriorityStage
    {
        internal override IAutoplayObjective ToObjective(CivilizationAutoplayer autoplayer, BuildingController buildingController) =>
            new ImperialPortObjective(autoplayer);
    }

    private sealed class ConditionalBuildingsStage : PriorityStage
    {
        private readonly BuildingType[] _buildings;
        private readonly int _targetLevel;

        public ConditionalBuildingsStage(BuildingType[] buildings, int targetLevel)
        {
            _buildings = buildings;
            _targetLevel = targetLevel;
        }

        internal override IAutoplayObjective ToObjective(CivilizationAutoplayer autoplayer, BuildingController buildingController) =>
            new ConditionalBuildingLevelObjective(
                () => autoplayer.WorldState != null && autoplayer.WorldState.Features.OfType<Bandit>().Any(b => b.Found),
                new BuildingLevelObjective(autoplayer, buildingController, _buildings, _targetLevel));
    }
}

/// <summary>
/// Test-layer wrapper around CivilizationAutoplayer that adds time-advancing loops.
/// Keeps clock management out of the core library.
/// </summary>
public class CivilizationAutoplayerRunner
{
    private readonly CivilizationAutoplayer _autoplayer;
    private readonly Civilization _civ;
    private readonly MainGameController _controller;
    private readonly double _timeStep = 0.5;

    public CivilizationAutoplayerRunner(CivilizationAutoplayer autoplayer, Civilization civ, MainGameController controller)
    {
        _autoplayer = autoplayer ?? throw new ArgumentNullException(nameof(autoplayer));
        _civ = civ ?? throw new ArgumentNullException(nameof(civ));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
    }

    public CivilizationAutoplayer Autoplayer => _autoplayer;
    public BuildingController BuildingController => _controller.BuildingController;

    private void Advance() => _controller.Clock?.SimulateAdvance((long)(_timeStep * 100));

    // ── Primitive time-advancing wrappers ────────────────────────────────────

    public bool AutoBuildRoad(Edge edge, int maxIterations = 500)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            if (_autoplayer.TryBuildRoadOnce(edge)) return true;
            Advance();
        }
        return false;
    }

    public bool AutoBuildOutpost(Vertex vertex, int maxIterations = 500)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            try { if (_autoplayer.TryBuildOutpostOnce(vertex)) return true; } catch { }
            Advance();
        }
        return false;
    }

    public bool AutoBuildBuilding(City city, BuildingType buildingType, int maxIterations = 500)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            try { if (_autoplayer.TryBuildBuildingOnce(city, buildingType)) return true; } catch { }
            Advance();
        }
        return false;
    }

    // ── Step runners ─────────────────────────────────────────────────────────

    /// <summary>
    /// Performs prestige and greedily distributes all prestige points.
    /// <paramref name="priorityVertices"/>, if given, are purchased first — useful to deterministically
    /// unlock a specific building (e.g. the Barracks) regardless of the default cheapest-first order.
    /// Exits as soon as the condition is true (normally after one iteration).
    /// </summary>
    public void RunStepPrestige(Func<bool> condition, IReadOnlyList<Vertex>? priorityVertices = null, int maxIterations = 100)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryPrestigeOnce(priorityVertices); } catch { }
            Advance();
        }
    }

    /// <summary>
    /// Drives a PriorityAutoplayStrategy until either the given condition or the strategy itself
    /// reports completion (all its objectives satisfied), advancing the clock between attempts.
    /// Returns the number of iterations actually executed.
    /// </summary>
    public int RunPriorityStrategyUntil(PriorityAutoplayStrategy strategy, Func<bool> condition, int maxIterations = 10000)
    {
        int i;
        for (i = 0; i < maxIterations && !condition() && !strategy.IsComplete(); i++)
        {
            try
            {
                strategy.TryStepOnce();
                _autoplayer.TryUpdatePriorityTargetFlowsOnce();
            }
            catch { }
            Advance();
        }
        return i;
    }

    /// <summary>
    /// Builds a PriorityAutoplayStrategy from a hand-tuned stage sequence and drives it. See
    /// <see cref="PriorityStage"/> — the stages come from SOIStrategyTester experiments.
    /// Returns the number of iterations actually executed.
    /// </summary>
    public int RunPriorityStrategyUntil(IReadOnlyList<PriorityStage> stages, Func<bool> condition, int maxIterations = 10000)
    {
        var objectives = stages.Select(s => s.ToObjective(_autoplayer, _controller.BuildingController)).ToList();
        return RunPriorityStrategyUntil(new PriorityAutoplayStrategy(objectives), condition, maxIterations);
    }
    /// <summary>
    /// Grinds step-3 actions, places the Wonder if needed, keeps investment enabled for whichever
    /// resources the next level requires (via CivilizationAutoplayer.TryWonderInvestmentOnce — handles
    /// re-enabling after each level-up clears it), and trades for gold to help fund it. Used both to
    /// reach the initial placement checkpoint and to push the Wonder to any further target level.
    /// </summary>
    public void RunStepWonderUntil(Func<bool> condition, int maxIterations = 100000)
    {
        var strategy = CivilizationAutoplayerPriorities.Step2(_autoplayer, _controller.BuildingController, expand: false);
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try
            {
                strategy.TryStepOnce();
                _autoplayer.TryWonderInvestmentOnce();
                _autoplayer.TryTradeForResourceOnce(Resource.Gold);
            }
            catch { }
            Advance();
        }
    }

}
