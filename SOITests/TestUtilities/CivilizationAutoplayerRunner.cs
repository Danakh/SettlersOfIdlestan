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

    private void Advance() => _controller.Clock?.SimulateAdvance((long)(_timeStep * 100));

    // ── Primitive time-advancing wrappers ────────────────────────────────────

    public void AutoGrind(ResourceSet? requiredResources, int maxIterations = 500)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            try { _autoplayer.TryGrindOnce(requiredResources); } catch { }
            Advance();
        }
    }

    public bool AutoBuildRoad(Edge edge, int maxIterations = 500)
    {
        for (int i = 0; i < maxIterations; i++)
        {
            if (_autoplayer.TryBuildRoadOnce(edge)) return true;
            Advance();
        }
        return false;
    }

    public bool AutoBuildRoadToDistance(int distance, int maxIterations = 10)
    {
        if (distance <= 0) throw new ArgumentException("distance must be >= 1", nameof(distance));

        var roadController = _controller.RoadController;
        for (int i = 0; i < maxIterations; i++)
        {
            try
            {
                var candidates = roadController.GetBuildableRoadsAtDistance(_civ.Index, distance);
                if (candidates != null && candidates.Any())
                    if (AutoBuildRoad(candidates.First().Position)) return true;

                var nearest = roadController.GetBuildableRoads(_civ.Index)
                    .OrderBy(r => r.DistanceToNearestCity).FirstOrDefault();
                if (nearest != null) AutoBuildRoad(nearest.Position);
            }
            catch { }
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
    /// Builds Barracks/Palisade, keeps growing the civilization (so player cities eventually fall
    /// within attack range of NPC territory), and points each idle player city's attack flow at the
    /// nearest enemy city within range (matching MilitaryController.FindNearbyEnemyCity — the same
    /// range/visibility rules the real attack resolution uses). Exits once the condition (e.g. all
    /// NPC civilizations eliminated) is met.
    /// </summary>
    public void RunStepExterminateCivilizationsUntil(Func<bool> condition, int maxIterations = 50000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try
            {
                _autoplayer.TryMilitaryStepOnce();
                _autoplayer.TryStep2Once(shouldExpand: true);

                var militaryController = _controller.MilitaryController;
                foreach (var city in _civ.Cities.ToList())
                {
                    if (city.FlowTarget != null) continue;
                    var enemy = militaryController.FindNearbyEnemyCity(city);
                    if (enemy != null) militaryController.SetCityFlow(city, enemy.Position);
                }
            }
            catch { }
            Advance();
        }
    }

    /// <summary>
    /// Lightweight stand-in for <see cref="RunStepExterminateCivilizationsUntil"/>: just builds the
    /// Barracks to level 1 in every existing city, without ever attacking. Used while the full
    /// extermination loop is disabled for a given scenario step because it's too slow to run regularly
    /// (see StepIslandScenarios.Island4) — keeps the save chain intact at a fraction of the cost, ready
    /// to swap back to the full extermination step later.
    /// </summary>
    public void RunStepBuildBarracksUntil(Func<bool> condition, int maxIterations = 10000)
    {
        var objective = new BuildingLevelObjective(_autoplayer, _controller.BuildingController,
            new[] { BuildingType.Barracks }, targetLevel: 1);
        var strategy = new PriorityAutoplayStrategy(new IAutoplayObjective[] { objective });
        RunPriorityStrategyUntil(strategy, condition, maxIterations);
    }

    public void RunStep1Until(Func<bool> condition, bool shouldExpand = true, int maxIterations = 10000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryStep1Once(shouldExpand); } catch { }
            Advance();
        }
    }

    public void RunStep2Until(Func<bool> condition, bool shouldExpand = true, int maxIterations = 10000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryStep2Once(shouldExpand); } catch { }
            Advance();
        }
    }

    public void RunStep3Until(Func<bool> condition, bool shouldExpand = true, int maxIterations = 10000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryStep3Once(shouldExpand); } catch { }
            Advance();
        }
    }

    public void RunStepMilitaryUntil(Func<bool> condition, bool shouldExpand = true, int maxIterations = 10000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryMilitaryStepOnce(); } catch { }
            Advance();
        }
    }

    /// <summary>
    /// Drives a PriorityAutoplayStrategy until either the given condition or the strategy itself
    /// reports completion (all its objectives satisfied), advancing the clock between attempts.
    /// </summary>
    public void RunPriorityStrategyUntil(PriorityAutoplayStrategy strategy, Func<bool> condition, int maxIterations = 10000)
    {
        for (int i = 0; i < maxIterations && !condition() && !strategy.IsComplete(); i++)
        {
            try { strategy.TryStepOnce(); } catch { }
            Advance();
        }
    }

    /// <summary>
    /// Builds a PriorityAutoplayStrategy from a hand-tuned stage sequence and drives it. See
    /// <see cref="PriorityStage"/> — the stages come from SOIStrategyTester experiments.
    /// </summary>
    public void RunPriorityStrategyUntil(IReadOnlyList<PriorityStage> stages, Func<bool> condition, int maxIterations = 10000)
    {
        var objectives = stages.Select(s => s.ToObjective(_autoplayer, _controller.BuildingController)).ToList();
        RunPriorityStrategyUntil(new PriorityAutoplayStrategy(objectives), condition, maxIterations);
    }
    /// <summary>
    /// Grinds step-3 actions, places the Wonder if needed, keeps investment enabled for whichever
    /// resources the next level requires (via CivilizationAutoplayer.TryWonderInvestmentOnce — handles
    /// re-enabling after each level-up clears it), and trades for gold to help fund it. Used both to
    /// reach the initial placement checkpoint and to push the Wonder to any further target level.
    /// </summary>
    public void RunStepWonderUntil(Func<bool> condition, int maxIterations = 100000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try
            {
                _autoplayer.TryStep2Once(shouldExpand: false);
                _autoplayer.TryWonderInvestmentOnce();
                _autoplayer.TryTradeForResourceOnce(Resource.Gold);
            }
            catch { }
            Advance();
        }
    }

    /// <summary>
    /// Expands the civilization via Step2 until the city count has been stable for
    /// <paramref name="stagnationWindow"/> consecutive iterations (no new city placed),
    /// which signals that all reachable vertices are already occupied. Exits early if
    /// <paramref name="condition"/> is met.
    /// </summary>
    public void RunStepExpandToStableUntil(Func<bool> condition, int stagnationWindow = 200, int maxIterations = 30000)
    {
        int lastCityCount = _civ.Cities.Count;
        int stagnantIterations = 0;

        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryStep2Once(shouldExpand: true); } catch { }
            Advance();

            int newCount = _civ.Cities.Count;
            if (newCount > lastCityCount)
            {
                lastCityCount = newCount;
                stagnantIterations = 0;
            }
            else if (++stagnantIterations >= stagnationWindow)
                break;
        }
    }

    /// <summary>
    /// Expands the civilization via Step2 as long as there are road slots available in the
    /// reachable network. Stops as soon as <see cref="RoadController.GetBuildableRoads"/> returns
    /// empty — meaning no further expansion is topologically possible — or until
    /// <paramref name="condition"/> is met.
    /// </summary>
    public void RunStepExpandWhileRoadsExistUntil(Func<bool> condition, int maxIterations = 30000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            if (!_controller.RoadController.GetBuildableRoads(_civ.Index).Any())
                break;

            try { _autoplayer.TryStep2Once(shouldExpand: true); } catch { }
            Advance();
        }
    }

    /// <summary>
    /// Alternates between attack and rebuild phases until all NPC civilizations are eliminated
    /// (or <paramref name="condition"/> is met). Each iteration focuses player city attacks on the
    /// NPC civilization with the fewest cities visible to the player. Whenever an NPC city is
    /// destroyed, the runner switches to rebuild mode: it expands the player civilization back
    /// up to <paramref name="targetPlayerCityCount"/> and builds Barracks in every city before
    /// returning to the attack phase.
    /// </summary>
    public void RunStepAttackWeakestNpcAndRebuildUntil(Func<bool> condition, int targetPlayerCityCount = 12, int maxIterations = 100000)
    {
        var militaryController = _controller.MilitaryController;
        var worldState = _controller.CurrentMainState!.CurrentWorldState!;
        int prevNpcCityCount = worldState.Civilizations.Where(c => c.IsNpc).Sum(c => c.Cities.Count);
        bool inRebuildPhase = _civ.Cities.Count < targetPlayerCityCount;

        var barracksObjective = new BuildingLevelObjective(_autoplayer, _controller.BuildingController,
            new[] { BuildingType.Barracks }, targetLevel: 1);

        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try
            {
                int currentNpcCityCount = worldState.Civilizations.Where(c => c.IsNpc).Sum(c => c.Cities.Count);
                if (currentNpcCityCount < prevNpcCityCount)
                {
                    prevNpcCityCount = currentNpcCityCount;
                    inRebuildPhase = true;
                }

                if (inRebuildPhase)
                {
                    bool needsMoreCities = _civ.Cities.Count < targetPlayerCityCount;
                    _autoplayer.TryStep2Once(shouldExpand: needsMoreCities);
                    _autoplayer.TryMilitaryStepOnce();

                    if (!needsMoreCities && barracksObjective.IsComplete())
                        inRebuildPhase = false;
                }
                else
                {
                    var visibleMaps = worldState.Visibility.GetForZ(IslandMap.SurfaceLayer);
                    visibleMaps.TryGetValue(_civ.Index, out var playerVisibleMap);

                    var weakestNpc = worldState.Civilizations
                        .Where(c => c.IsNpc && c.Cities.Count > 0)
                        .OrderBy(c => playerVisibleMap == null
                            ? c.Cities.Count
                            : c.Cities.Count(city => city.Position.GetHexes().Any(h => playerVisibleMap.HasTile(h))))
                        .FirstOrDefault();

                    _autoplayer.TryMilitaryStepOnce();
                    _autoplayer.TryStep2Once(shouldExpand: true);

                    if (weakestNpc != null)
                    {
                        foreach (var city in _civ.Cities.ToList())
                        {
                            if (city.FlowTarget != null) continue;
                            var enemy = militaryController.FindNearbyEnemyCity(city, new[] { weakestNpc.Index });
                            if (enemy != null) militaryController.SetCityFlow(city, enemy.Position);
                        }
                    }
                }
            }
            catch { }
            Advance();
        }
    }
}
