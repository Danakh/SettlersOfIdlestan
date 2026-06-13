using System;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

namespace SOITests.TestUtilities;

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
    /// Exits as soon as the condition is true (normally after one iteration).
    /// </summary>
    public void RunStepPrestige(Func<bool> condition, int maxIterations = 100)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try { _autoplayer.TryPrestigeOnce(); } catch { }
            Advance();
        }
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
    public void RunStepWaitUntil(Func<bool> condition, int maxIterations = 100000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            _autoplayer.TryTradeForResourceOnce(Resource.Gold);
            Advance();
        }
    }

    /// <summary>
    /// Grind step-3 actions until Architecture is researched, then places the wonder and
    /// enables investment for all level-1 resources. Exits as soon as the condition is met.
    /// </summary>
    public void RunStepWonderSetupUntil(Func<bool> condition, int maxIterations = 10000)
    {
        for (int i = 0; i < maxIterations && !condition(); i++)
        {
            try
            {
                _autoplayer.TryStep2Once(shouldExpand: false);

                var worldState = _controller.CurrentMainState?.CurrentWorldState;
                if (worldState != null)
                {
                    var wonderCtrl = _controller.WonderController;
                    var wonder = worldState.Features.OfType<Wonder>().FirstOrDefault();

                    if (wonder == null && wonderCtrl.CanPlaceWonder(worldState.PlayerCivilization))
                    {
                        var hexes = wonderCtrl.GetPlaceableHexes();
                        if (hexes.Count > 0)
                            wonder = wonderCtrl.PlaceWonder(hexes[0]);
                    }

                    if (wonder != null)
                    {
                        var cost = WonderController.GetLevelCost(1);
                        foreach (var r in cost.Keys)
                            if (!wonder.InvestmentEnabled.Contains(r))
                                wonder.InvestmentEnabled.Add(r);
                    }
                }
            }
            catch { }
            Advance();
        }
    }
}
