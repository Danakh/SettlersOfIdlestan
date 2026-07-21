using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SOIStrategyTester.Model;

namespace SOIStrategyTester;

public class StrategyRunOptions
{
    public int DefaultMaxIterationsPerPhase { get; set; } = 20000;
    public double TimeStep { get; set; } = 0.5;
}

/// <summary>
/// Runs a single StrategyDefinition against a live MainGameController until the run's global
/// objective is satisfied, and reports how many game ticks that took. Each phase rebuilds its own
/// CivilizationAutoplayer from the controller's current state — required after a Prestige phase,
/// since CivilizationAutoplayer.TryPrestigeOnce's civ/map references go stale once prestige happens
/// (see CivilizationAutoplayer's doc comment), and cheap enough to do unconditionally for every phase.
/// </summary>
public static class StrategyRunner
{
    public static StrategyRunResult Run(MainGameController controller, StrategyDefinition strategy, ObjectiveSpec globalObjective, StrategyRunOptions options)
    {
        var clock = controller.Clock ?? throw new InvalidOperationException("Controller has no clock.");
        long iterationsUsed = 0;

        for (int phaseIndex = 0; phaseIndex < strategy.Phases.Count; phaseIndex++)
        {
            var phase = strategy.Phases[phaseIndex];
            var auto = BuildAutoplayer(controller);
            var priorityStrategy = phase.Kind == PhaseKind.Priority
                ? BuildPriorityStrategy(auto, controller.BuildingController, phase.PriorityObjectives
                    ?? throw new ArgumentException($"Phase {phaseIndex}: Priority phases require PriorityObjectives."))
                : null;

            int phaseMaxIterations = phase.MaxIterations ?? options.DefaultMaxIterationsPerPhase;
            bool reachedPhaseEnd = false;

            for (int i = 0; i < phaseMaxIterations; i++)
            {
                if (ObjectiveEvaluator.Evaluate(globalObjective, controller))
                    return StrategyRunResult.Successful(strategy.Name, clock.CurrentTick, iterationsUsed);

                if (phase.Until != null && ObjectiveEvaluator.Evaluate(phase.Until, controller))
                {
                    reachedPhaseEnd = true;
                    break;
                }

                try { ExecutePhaseOnce(phase, auto, controller, priorityStrategy); }
                catch { /* mirrors CivilizationAutoplayerRunner's swallow-and-retry-next-tick behaviour */ }

                clock.SimulateAdvance((long)(options.TimeStep * 100));
                iterationsUsed++;
            }

            if (!reachedPhaseEnd)
            {
                return StrategyRunResult.Failed(strategy.Name, clock.CurrentTick, iterationsUsed,
                    $"Phase {phaseIndex} ('{phase.Kind}') exceeded {phaseMaxIterations} iterations without reaching its objective.");
            }
        }

        if (ObjectiveEvaluator.Evaluate(globalObjective, controller))
            return StrategyRunResult.Successful(strategy.Name, clock.CurrentTick, iterationsUsed);

        return StrategyRunResult.Failed(strategy.Name, clock.CurrentTick, iterationsUsed,
            "All phases completed without reaching the global objective.");
    }

    /// <summary>
    /// Drives the phase's own action, plus (unconditionally, every call) background progress that no
    /// phase kind otherwise triggers — see TryAdvanceBackgroundSystemsOnce.
    /// </summary>
    internal static bool ExecutePhaseOnce(StrategyPhase phase, CivilizationAutoplayer auto, MainGameController controller, PriorityAutoplayStrategy? priorityStrategy)
    {
        bool didBackground = TryAdvanceBackgroundSystemsOnce(auto);
        return ExecutePhaseCore(phase, auto, controller, priorityStrategy) || didBackground;
    }

    private static bool ExecutePhaseCore(StrategyPhase phase, CivilizationAutoplayer auto, MainGameController controller, PriorityAutoplayStrategy? priorityStrategy)
    {
        var bc = controller.BuildingController;
        switch (phase.Kind)
        {
            case PhaseKind.Step1:
            case PhaseKind.Step2:
            case PhaseKind.Step3:
            case PhaseKind.Military:
                return CivilizationAutoplayerPriorities.Unified(auto, bc).TryStepOnce();

            case PhaseKind.UnifiedAggressive:
                return CivilizationAutoplayerPriorities.Unified(auto, bc, aggressive: true).TryStepOnce();

            case PhaseKind.ExterminateMonsters:
                {
                    bool did = CivilizationAutoplayerPriorities.Unified(auto, bc).TryStepOnce();
                    did |= CivilizationAutoplayerPriorities.Step2(auto, bc, expand: true).TryStepOnce();
                    return did;
                }

            case PhaseKind.ExterminateCivilizations:
                {
                    bool did = CivilizationAutoplayerPriorities.Unified(auto, bc, attackNeighborsAtCities: 0).TryStepOnce();
                    did |= CivilizationAutoplayerPriorities.Step2(auto, bc, expand: true).TryStepOnce();
                    foreach (var city in auto.Civilization.Cities.ToList())
                    {
                        if (city.FlowTarget != null) continue;
                        var enemy = controller.MilitaryController.FindNearbyEnemyCity(city);
                        if (enemy != null)
                        {
                            controller.MilitaryController.SetCityFlow(city, enemy.Position);
                            did = true;
                        }
                    }
                    return did;
                }

            case PhaseKind.Wonder:
                {
                    bool did = CivilizationAutoplayerPriorities.Step2(auto, bc, expand: false).TryStepOnce();
                    did |= auto.TryWonderInvestmentOnce();
                    did |= auto.TryTradeForResourceOnce(Resource.Gold);
                    return did;
                }

            case PhaseKind.WonderInvestOnly:
                {
                    bool did = auto.TryWonderInvestmentOnce();
                    did |= auto.TryTradeForResourceOnce(Resource.Gold);
                    return did;
                }

            case PhaseKind.Prestige:
                return auto.TryPrestigeOnce(ResolvePriorityVertices(phase.PrestigePriorityVertexNames));

            case PhaseKind.Priority:
                return priorityStrategy!.TryStepOnce();

            default:
                throw new NotSupportedException($"Unknown phase kind: {phase.Kind}");
        }
    }

    /// <summary>
    /// Background progress that no PhaseKind otherwise drives, tried once per iteration regardless of
    /// the active phase's own kind:
    /// - Deepest Mine → Corruption Spire → Abyss Gate chain — exactly like the passive investment
    ///   controllers (DeepestMineController/CorruptionSpireController/AbyssGateController) tick every
    ///   clock Advanced independently of whatever the player is doing. Each Try*InvestmentOnce is a
    ///   no-op until its prestige-vertex unlock is purchased.
    /// - Research (CivilizationAutoplayer.TryResearchOnce) — no PhaseKind calls this at all otherwise,
    ///   so without it research (and everything it gates: higher building-level caps, UNLOCK_WONDERS,
    ///   the abyss chain's own UNLOCK_DEEPEST_MINE/UNLOCK_ABYSS prestige vertices, ...) would never
    ///   progress under any strategy built purely from Priority phases. No-op once nothing is
    ///   researchable or something is already in progress.
    /// Each of these is a no-op until unlocked, so calling them unconditionally from every phase is safe.
    /// </summary>
    private static bool TryAdvanceBackgroundSystemsOnce(CivilizationAutoplayer auto)
    {
        bool did = auto.TryDeepestMineInvestmentOnce();
        did |= auto.TryCorruptionSpireInvestmentOnce();
        did |= auto.TryAbyssGateInvestmentOnce();
        did |= auto.TryResearchOnce();
        return did;
    }

    internal static CivilizationAutoplayer BuildAutoplayer(MainGameController controller)
    {
        var worldState = controller.CurrentMainState?.CurrentWorldState
            ?? throw new InvalidOperationException("Controller has no current world state.");
        var civ = worldState.Civilizations.First(c => !c.IsNpc);

        return new CivilizationAutoplayer(
            civ,
            worldState.GetMapForZ(IslandMap.SurfaceLayer)!,
            controller.RoadController,
            controller.HarvestController,
            controller.BuildingController,
            controller.CityBuilderController,
            controller.TradeController,
            controller.ResearchController,
            controller.PrestigeController,
            controller.PrestigeMapController,
            worldState,
            controller.CurrentMainState!.PrestigeState,
            controller.PerformPrestige,
            controller.WonderController,
            deepestMineController: controller.DeepestMineController,
            corruptionSpireController: controller.CorruptionSpireController,
            abyssGateController: controller.AbyssGateController);
    }

    internal static PriorityAutoplayStrategy BuildPriorityStrategy(CivilizationAutoplayer auto, BuildingController buildingController, List<PriorityObjectiveSpec> specs)
    {
        var objectives = new List<IAutoplayObjective>(specs.Count);
        foreach (var spec in specs)
        {
            objectives.Add(spec.Kind switch
            {
                PriorityObjectiveKind.BuildingLevel => new BuildingLevelObjective(
                    auto, buildingController,
                    spec.Buildings ?? throw new ArgumentException("BuildingLevel objective requires Buildings."),
                    spec.TargetLevel ?? throw new ArgumentException("BuildingLevel objective requires TargetLevel.")),
                PriorityObjectiveKind.CityCount => new CityCountObjective(
                    auto,
                    spec.TargetCityCount ?? throw new ArgumentException("CityCount objective requires TargetCityCount.")),
                PriorityObjectiveKind.ImperialPort => new ImperialPortObjective(auto),
                PriorityObjectiveKind.BuildingLevelIfBanditSpotted => new ConditionalBuildingLevelObjective(
                    () => auto.WorldState != null && auto.WorldState.Features.OfType<Bandit>().Any(b => b.Found),
                    new BuildingLevelObjective(
                        auto, buildingController,
                        spec.Buildings ?? throw new ArgumentException("BuildingLevelIfBanditSpotted objective requires Buildings."),
                        spec.TargetLevel ?? throw new ArgumentException("BuildingLevelIfBanditSpotted objective requires TargetLevel."))),
                PriorityObjectiveKind.UniqueBuilding => new UniqueBuildingObjective(
                    auto,
                    spec.Building ?? throw new ArgumentException("UniqueBuilding objective requires Building.")),
                _ => throw new NotSupportedException($"Unknown priority objective kind: {spec.Kind}")
            });
        }
        return new PriorityAutoplayStrategy(objectives);
    }

    private static List<Vertex>? ResolvePriorityVertices(List<string>? names)
    {
        if (names == null || names.Count == 0) return null;

        var mapType = typeof(SettlersOfIdlestan.Model.Prestige.PrestigeMap.PrestigeMap);
        var result = new List<Vertex>(names.Count);
        foreach (var name in names)
        {
            var field = mapType.GetField(name, BindingFlags.Public | BindingFlags.Static)
                ?? throw new ArgumentException($"Unknown PrestigeMap vertex field '{name}'.");
            if (field.GetValue(null) is not Vertex vertex)
                throw new ArgumentException($"PrestigeMap field '{name}' is not a Vertex.");
            result.Add(vertex);
        }
        return result;
    }
}
