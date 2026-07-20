using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SOIStrategyTester.Model;

namespace SOIStrategyTester;

public class EndlessRunOptions
{
    public string CsvPath { get; set; } = "run_current.csv";
    public double CheckpointIntervalHours { get; set; } = 1.0;
    public long MaxCycles { get; set; } = 100_000;

    /// <summary>Fixed prestige-point target for the Nth prestige (1-indexed by position). Once the
    /// cycle count exceeds this list, the target instead doubles the previous cycle's actual points
    /// each time — see MaxIslandHoursAfterFixedTargets.</summary>
    public List<int> PrestigePointTargets { get; set; } = new() { 35, 80, 500, 1000 };

    /// <summary>Once past PrestigePointTargets, each island is capped at this many simulated hours —
    /// whichever of (2× previous points) or this time limit is reached first ends the island.</summary>
    public double MaxIslandHoursAfterFixedTargets { get; set; } = 24.0;

    /// <summary>Pure safety valve: if a single island still hasn't triggered a prestige after this many
    /// full passes through every phase (each pass being cheap once everything is already built), force
    /// a prestige with whatever points are available rather than hang forever. Only relevant for the
    /// fixed (no-time-cap) targets, where a genuinely unreachable target would otherwise loop forever.</summary>
    public int MaxPassesPerCycle { get; set; } = 500;
}

/// <summary>
/// Runs a single StrategyDefinition on a loop, re-entering its phases from phase 0 as many times as
/// needed on the same island until this cycle's prestige trigger fires, then prestiges and moves to the
/// next cycle — until the run's global objective is met or MaxCycles is exhausted. Unlike
/// StrategyRunner.Run (a single linear pass through Phases, racing several strategies from an identical
/// starting state), this drives exactly one strategy indefinitely and decides *when* to prestige itself
/// (see EndlessRunOptions.PrestigePointTargets) rather than relying on a Prestige phase inside the
/// strategy — the strategy should therefore contain no Prestige phase; it just describes how to build
/// up an island. Reports progress: one console line (and CSV row) per prestige, plus a CSV row (and
/// console line) every CheckpointIntervalHours of simulated time — CivilizationAutoplayer/StrategyRunner
/// never advance real wall-clock time, only the game clock (1 tick = 0.01 simulated second), so "hourly"
/// here means simulated hours.
/// </summary>
public static class EndlessRunner
{
    private const string CsvHeader =
        "EventType,Cycle,PhaseIndex,PhaseKind,Iterations,Tick,SimulatedHours,PrestigeCount,PointsTarget," +
        "IslandTicks,WorldId,Tier,CityCount,BuildingCount,TotalBuildingLevels,PrestigePoints," +
        "ResearchCompleted,UniqueBuildings,WonderLevel,HasDeepestMine,HasCorruptionSpire,HasAbyssGate," +
        "AbyssGateEverUnlocked";

    public static void Run(MainGameController controller, StrategyDefinition strategy, ObjectiveSpec globalObjective,
        StrategyRunOptions options, EndlessRunOptions endlessOptions)
    {
        if (strategy.Phases.Count == 0)
            throw new ArgumentException("Endless mode requires the strategy to have at least one phase.");
        if (strategy.Phases.Any(p => p.Kind == PhaseKind.Prestige))
            throw new ArgumentException(
                "Endless mode decides when to prestige itself (EndlessRunOptions.PrestigePointTargets) — " +
                "remove the 'Prestige' phase from the strategy; it should only describe how to build up an island.");

        var clock = controller.Clock ?? throw new InvalidOperationException("Controller has no clock.");
        var mainState = controller.CurrentMainState ?? throw new InvalidOperationException("Controller has no main state.");

        long checkpointIntervalTicks = (long)Math.Round(endlessOptions.CheckpointIntervalHours * 3600.0 * 100.0);
        if (checkpointIntervalTicks <= 0)
            throw new ArgumentException("--checkpoint-hours must be positive.");
        long maxIslandTicks = (long)Math.Round(endlessOptions.MaxIslandHoursAfterFixedTargets * 3600.0 * 100.0);

        var fullCsvPath = Path.GetFullPath(endlessOptions.CsvPath);
        var csvDir = Path.GetDirectoryName(fullCsvPath);
        if (!string.IsNullOrEmpty(csvDir))
            Directory.CreateDirectory(csvDir);

        using var csv = new StreamWriter(fullCsvPath, append: false);
        csv.WriteLine(CsvHeader);
        csv.Flush();

        Console.WriteLine($"Endless run started for strategy '{strategy.Name}' — objective: {globalObjective.Kind}.");
        Console.WriteLine($"CSV: {fullCsvPath}");

        long iterationsUsed = 0;
        int prestigeCount = 0;
        long nextCheckpointTick = checkpointIntervalTicks;
        int lastAchievedPoints = 0;

        for (long cycle = 1; cycle <= endlessOptions.MaxCycles; cycle++)
        {
            int cycleIdx0 = (int)Math.Min(cycle - 1, int.MaxValue);
            bool hasTimeCap = cycleIdx0 >= endlessOptions.PrestigePointTargets.Count;
            int pointsTarget = hasTimeCap
                ? Math.Max(1, lastAchievedPoints * 2)
                : endlessOptions.PrestigePointTargets[cycleIdx0];
            long islandStartTick = mainState.CurrentWorldState?.StartTick ?? clock.CurrentTick;

            Console.WriteLine(hasTimeCap
                ? $"== Cycle {cycle}: target {pointsTarget} prestige points (2x previous), or {endlessOptions.MaxIslandHoursAfterFixedTargets}h simulated — whichever comes first =="
                : $"== Cycle {cycle}: target {pointsTarget} prestige points ==");

            bool prestigedThisCycle = false;
            int pointsAtLastPassBoundary = -1;
            int stagnantPassCount = 0;

            for (int pass = 1; pass <= endlessOptions.MaxPassesPerCycle && !prestigedThisCycle; pass++)
            {
                // A pass "doing something" (see IdleBreakThreshold below) doesn't mean it's making
                // progress toward THIS cycle's points target — e.g. CivilizationAutoplayer.TryExpandOnce
                // can keep building roads that never resolve into a new city, returning true indefinitely
                // without moving the needle. Track the metric that actually matters (prestige points) pass
                // over pass, and give up once it's been flat for a while — same effect as isLastPass, just
                // reached in a handful of passes instead of potentially hundreds when nothing is working.
                int pointsAtPassStart = controller.PrestigeController.CalculatePrestigePoints();
                stagnantPassCount = pointsAtPassStart == pointsAtLastPassBoundary ? stagnantPassCount + 1 : 0;
                pointsAtLastPassBoundary = pointsAtPassStart;
                bool isLastPass = pass == endlessOptions.MaxPassesPerCycle || stagnantPassCount >= StagnantPassLimit;

                for (int phaseIndex = 0; phaseIndex < strategy.Phases.Count && !prestigedThisCycle; phaseIndex++)
                {
                    var phase = strategy.Phases[phaseIndex];
                    var auto = StrategyRunner.BuildAutoplayer(controller);
                    var priorityStrategy = phase.Kind == PhaseKind.Priority
                        ? StrategyRunner.BuildPriorityStrategy(auto, controller.BuildingController, phase.PriorityObjectives
                            ?? throw new ArgumentException($"Cycle {cycle}, phase {phaseIndex}: Priority phases require PriorityObjectives."))
                        : null;

                    int phaseMaxIterations = phase.MaxIterations ?? options.DefaultMaxIterationsPerPhase;
                    bool reachedPhaseEnd = false;
                    int consecutiveNoOps = 0;

                    for (int i = 0; i < phaseMaxIterations; i++)
                    {
                        if (ObjectiveEvaluator.Evaluate(globalObjective, controller))
                        {
                            Console.WriteLine(
                                $"Objective reached ({globalObjective.Kind}) — cycle {cycle}, {prestigeCount} prestiges, " +
                                $"tick {clock.CurrentTick} ({FormatHours(clock.CurrentTick)} simulated hours), {iterationsUsed} iterations.");
                            WriteRow(csv, "Objective", cycle, phaseIndex, phase.Kind, iterationsUsed, prestigeCount, pointsTarget, clock, controller, mainState);
                            csv.Flush();
                            return;
                        }

                        int currentPoints = controller.PrestigeController.CalculatePrestigePoints();
                        long islandAge = clock.CurrentTick - islandStartTick;
                        bool targetMet = currentPoints >= pointsTarget;
                        bool timeCapped = hasTimeCap && islandAge >= maxIslandTicks;
                        if ((targetMet || timeCapped || isLastPass) && controller.PrestigeController.PrestigeIsAvailable())
                        {
                            string reason = targetMet ? $"reached {currentPoints} pts (target {pointsTarget})"
                                : timeCapped ? $"hit the {endlessOptions.MaxIslandHoursAfterFixedTargets}h cap with {currentPoints} pts (target was {pointsTarget})"
                                : stagnantPassCount >= StagnantPassLimit
                                    ? $"gave up — {currentPoints} pts hasn't moved in {stagnantPassCount} passes (target was {pointsTarget})"
                                    : $"gave up after {endlessOptions.MaxPassesPerCycle} passes with {currentPoints} pts (target was {pointsTarget})";

                            int prestigesBefore = mainState.GameRecord.TotalPrestigesPerformed;
                            auto.TryPrestigeOnce();
                            if (mainState.GameRecord.TotalPrestigesPerformed > prestigesBefore)
                            {
                                prestigeCount++;
                                Console.WriteLine($"[trigger] {reason}");
                                lastAchievedPoints = LogPrestige(csv, cycle, phaseIndex, phase.Kind, iterationsUsed, prestigeCount, pointsTarget, clock, mainState);
                            }
                            prestigedThisCycle = true;
                            break;
                        }

                        if (phase.Until != null && ObjectiveEvaluator.Evaluate(phase.Until, controller))
                        {
                            reachedPhaseEnd = true;
                            break;
                        }

                        bool didSomething;
                        try { didSomething = StrategyRunner.ExecutePhaseOnce(phase, auto, controller, priorityStrategy); }
                        catch { didSomething = false; /* mirrors StrategyRunner.Run's swallow-and-retry-next-tick behaviour */ }

                        clock.SimulateAdvance((long)(options.TimeStep * 100));
                        iterationsUsed++;

                        // A phase with nothing left to do (everything already built/maxed, e.g. no Wonder
                        // access yet so WonderInvestOnly is permanently a no-op) would otherwise burn its
                        // full iteration budget doing nothing, every single pass — with a null Until (the
                        // common case for a "keep grinding" phase) that budget is deliberately large, which
                        // made a genuinely stuck phase take a very long time to notice. Once truly idle for
                        // a while (a production/trade cycle away from ever doing anything), stop early —
                        // same effect as running out of iterations, just without the wait.
                        consecutiveNoOps = didSomething ? 0 : consecutiveNoOps + 1;
                        if (consecutiveNoOps >= IdleBreakThreshold)
                            break;

                        while (clock.CurrentTick >= nextCheckpointTick)
                        {
                            LogCheckpoint(csv, cycle, phaseIndex, phase.Kind, iterationsUsed, prestigeCount, pointsTarget, clock, controller, mainState);
                            nextCheckpointTick += checkpointIntervalTicks;
                        }
                    }

                    // A non-null Until that's never reached usually means this particular generated
                    // island can't satisfy it (e.g. a map that plateaus below the CityCount target — see
                    // SOIStrategyTester/CLAUDE.md's gotchas). Warn and move on to the next phase anyway —
                    // the outer pass loop retries the whole sequence, so this island still gets further
                    // chances rather than dying over one stuck phase.
                    if (!prestigedThisCycle && !reachedPhaseEnd && phase.Until != null)
                    {
                        Console.WriteLine(
                            $"Cycle {cycle} pass {pass}, phase {phaseIndex} ('{phase.Kind}') exceeded {phaseMaxIterations} " +
                            "iterations without reaching its Until — this island may not support it; moving on.");
                        WriteRow(csv, "Stalled", cycle, phaseIndex, phase.Kind, iterationsUsed, prestigeCount, pointsTarget, clock, controller, mainState);
                        csv.Flush();
                    }
                }
            }

            if (!prestigedThisCycle)
            {
                Console.WriteLine(
                    $"Cycle {cycle}: gave up without ever reaching Prestige-available " +
                    $"(needs an Imperial Port + {PrestigeControllerRequiredPoints} points) — aborting endless run.");
                csv.Flush();
                return;
            }
        }

        Console.WriteLine(
            $"Reached the {endlessOptions.MaxCycles}-cycle safety cap without meeting the objective. " +
            $"{prestigeCount} prestiges performed, tick {clock.CurrentTick} ({FormatHours(clock.CurrentTick)} simulated hours).");
    }

    private const int PrestigeControllerRequiredPoints = 20; // SettlersOfIdlestan.Controller.Expand.PrestigeController.PrestigeRequiredPoints
    private const int IdleBreakThreshold = 300; // consecutive no-op iterations (≈150 simulated seconds at the default time step) before giving up on a phase early
    private const int StagnantPassLimit = 8; // full passes through every phase with zero prestige-point movement before giving up on the whole cycle

    private static int LogPrestige(StreamWriter csv, long cycle, int phaseIndex, PhaseKind phaseKind,
        long iterationsUsed, int prestigeCount, int pointsTarget, GameClock clock, MainGameState mainState)
    {
        var stats = mainState.PrestigeState?.RunHistory.LastOrDefault();
        Console.WriteLine(stats == null
            ? $"[Prestige #{prestigeCount}] cycle {cycle}, tick {clock.CurrentTick} — (no stats recorded)"
            : $"[Prestige #{prestigeCount}] cycle {cycle}, world {stats.WorldId}, tick {clock.CurrentTick} " +
              $"({FormatHours(clock.CurrentTick)}h sim) — island lasted {FormatHours(stats.TickDuration)}h, " +
              $"{stats.CityCount} cities, {stats.BuildingCount} buildings (lvl sum {stats.TotalBuildingLevels}), " +
              $"{stats.PrestigePoints} prestige pts, {stats.ResearchCompleted} research, " +
              $"wonder lvl {stats.WonderLevel}, mine={stats.HasDeepestMine}, spire={stats.HasCorruptionSpire}, gate={stats.HasAbyssGate}");

        csv.WriteLine(string.Join(',', new object?[]
        {
            "Prestige", cycle, phaseIndex, phaseKind, iterationsUsed, clock.CurrentTick, FormatHours(clock.CurrentTick),
            prestigeCount, pointsTarget,
            stats?.TickDuration ?? 0, stats?.WorldId ?? 0, mainState.PrestigeState?.Tier ?? 0,
            stats?.CityCount ?? 0, stats?.BuildingCount ?? 0, stats?.TotalBuildingLevels ?? 0,
            stats?.PrestigePoints ?? 0, stats?.ResearchCompleted ?? 0, stats?.UniqueBuildings ?? 0, stats?.WonderLevel ?? 0,
            stats?.HasDeepestMine ?? false, stats?.HasCorruptionSpire ?? false, stats?.HasAbyssGate ?? false,
            mainState.GameRecord.HasBuiltAbyssGate,
        }));
        csv.Flush();

        return stats?.PrestigePoints ?? 0;
    }

    private static void LogCheckpoint(StreamWriter csv, long cycle, int phaseIndex, PhaseKind phaseKind,
        long iterationsUsed, int prestigeCount, int pointsTarget, GameClock clock,
        MainGameController controller, MainGameState mainState)
    {
        var worldState = mainState.CurrentWorldState;
        if (worldState == null) return;
        var civ = worldState.Civilizations.FirstOrDefault(c => !c.IsNpc);
        if (civ == null) return;

        var allBuildings = civ.Cities.SelectMany(c => c.Buildings).ToList();
        var wonder = worldState.Features.OfType<Wonder>().FirstOrDefault();
        bool hasDeepestMine = worldState.Features.OfType<SettlersOfIdlestan.Model.IslandFeatures.DeepestMine>().Any(m => m.Dug);
        bool hasCorruptionSpire = worldState.Features.OfType<CorruptionSpire>().Any(s => s.Built);
        bool hasAbyssGate = worldState.Features.OfType<AbyssGate>().Any(g => g.Built);
        long islandTicks = clock.CurrentTick - worldState.StartTick;

        Console.WriteLine(
            $"[Checkpoint {FormatHours(clock.CurrentTick)}h] cycle {cycle}, phase {phaseIndex} ({phaseKind}), " +
            $"{prestigeCount} prestiges so far, world {worldState.WorldId}, {civ.Cities.Count} cities, " +
            $"{allBuildings.Count} buildings, {controller.PrestigeController.CalculatePrestigePoints()}/{pointsTarget} prestige pts, " +
            $"wonder lvl {wonder?.Level ?? 0}, mine={hasDeepestMine}, spire={hasCorruptionSpire}, gate={hasAbyssGate}");

        csv.WriteLine(string.Join(',', new object?[]
        {
            "Checkpoint", cycle, phaseIndex, phaseKind, iterationsUsed, clock.CurrentTick, FormatHours(clock.CurrentTick),
            prestigeCount, pointsTarget,
            islandTicks, worldState.WorldId, mainState.PrestigeState?.Tier ?? 0,
            civ.Cities.Count, allBuildings.Count, allBuildings.Sum(b => b.Level),
            controller.PrestigeController.CalculatePrestigePoints(),
            worldState.RunRecord?.ResearchCompleted ?? 0, allBuildings.Count(b => b.IsUnique), wonder?.Level ?? 0,
            hasDeepestMine, hasCorruptionSpire, hasAbyssGate,
            mainState.GameRecord.HasBuiltAbyssGate,
        }));
        csv.Flush();
    }

    private static void WriteRow(StreamWriter csv, string eventType, long cycle, int phaseIndex, PhaseKind phaseKind,
        long iterationsUsed, int prestigeCount, int pointsTarget, GameClock clock,
        MainGameController controller, MainGameState mainState)
    {
        var worldState = mainState.CurrentWorldState;
        var civ = worldState?.Civilizations.FirstOrDefault(c => !c.IsNpc);
        var allBuildings = civ?.Cities.SelectMany(c => c.Buildings).ToList() ?? new();
        var wonder = worldState?.Features.OfType<Wonder>().FirstOrDefault();
        bool hasDeepestMine = worldState?.Features.OfType<SettlersOfIdlestan.Model.IslandFeatures.DeepestMine>().Any(m => m.Dug) ?? false;
        bool hasCorruptionSpire = worldState?.Features.OfType<CorruptionSpire>().Any(s => s.Built) ?? false;
        bool hasAbyssGate = worldState?.Features.OfType<AbyssGate>().Any(g => g.Built) ?? false;
        long islandTicks = worldState == null ? 0 : clock.CurrentTick - worldState.StartTick;

        csv.WriteLine(string.Join(',', new object?[]
        {
            eventType, cycle, phaseIndex, phaseKind, iterationsUsed, clock.CurrentTick, FormatHours(clock.CurrentTick),
            prestigeCount, pointsTarget,
            islandTicks, worldState?.WorldId ?? 0, mainState.PrestigeState?.Tier ?? 0,
            civ?.Cities.Count ?? 0, allBuildings.Count, allBuildings.Sum(b => b.Level),
            controller.PrestigeController.CalculatePrestigePoints(),
            worldState?.RunRecord?.ResearchCompleted ?? 0, allBuildings.Count(b => b.IsUnique), wonder?.Level ?? 0,
            hasDeepestMine, hasCorruptionSpire, hasAbyssGate,
            mainState.GameRecord.HasBuiltAbyssGate,
        }));
    }

    /// <summary>Ticks (1 tick = 0.01 simulated second) formatted as simulated hours, 2 decimals.</summary>
    private static string FormatHours(long ticks)
        => (ticks / 100.0 / 3600.0).ToString("F2", CultureInfo.InvariantCulture);
}
