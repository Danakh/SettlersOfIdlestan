using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Prestige;
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

    /// <summary>
    /// Applies MaxIslandHoursAfterFixedTargets to <b>every</b> island, including the ones still covered
    /// by a fixed PrestigePointTargets entry (which normally have no time cap at all). Used by the race
    /// gauntlet, where a fixed target a given race simply cannot reach must not turn into an unbounded
    /// grind: the island ends on whichever of "target reached" or "time cap" comes first.
    /// </summary>
    public bool TimeCapAllIslands { get; set; }

    /// <summary>
    /// Hard stop, in simulated hours of a single island: if the island is still not
    /// PrestigeIsAvailable() by then, the whole run is abandoned rather than looping. 0 disables it.
    /// Distinct from the time cap above, which only ends an island that <i>can</i> prestige — a run
    /// that never gets an Imperial Port up would otherwise keep going until MaxPassesPerCycle.
    /// </summary>
    public double AbandonIslandAfterHours { get; set; }

    /// <summary>Pure safety valve: if a single island still hasn't triggered a prestige after this many
    /// full passes through every phase (each pass being cheap once everything is already built), force
    /// a prestige with whatever points are available rather than hang forever. Only relevant for the
    /// fixed (no-time-cap) targets, where a genuinely unreachable target would otherwise loop forever.</summary>
    public int MaxPassesPerCycle { get; set; } = 500;

    /// <summary>
    /// Plancher appliqué au objectif de points du <b>dernier</b> cycle (celui qui atteint
    /// <see cref="MaxCycles"/>) : son objectif devient le maximum de ce plancher et de celui qu'il
    /// aurait eu autrement. 0 désactive.
    ///
    /// <para>Sert aux appelants qui jugent une manche sur les points de la dernière île plutôt que
    /// sur le simple fait d'avoir prestigé — voir <see cref="RaceGauntletOptions.MinFinalIslandPrestigePoints"/>.
    /// Sans ce plancher, le critère serait truqué : l'objectif d'une île passée les cibles fixes vaut
    /// le double des points réels de la précédente (~80-120 en pratique), donc l'île prestigerait dès
    /// ce palier et la mesure ne dirait pas si la race pouvait aller plus loin, seulement qu'on ne le
    /// lui a pas demandé. Ce n'est pas une garantie d'atteindre le plancher pour autant : le plafond
    /// de temps par île et la soupape de stagnation continuent de terminer l'île en dessous, ce qui
    /// est précisément le résultat négatif qu'on cherche à observer.</para>
    /// </summary>
    public int LastCyclePointsFloor { get; set; }
}

/// <summary>
/// Outcome of an <see cref="EndlessRunner.Run"/> call. The caller decides what counts as success:
/// the endless mode itself only cares about <see cref="ObjectiveReached"/>, while the race gauntlet
/// judges on <see cref="PrestigeCount"/> (did this race clear N islands?) and reads
/// <see cref="IslandStats"/> for the per-island summary table.
/// </summary>
public class EndlessRunResult
{
    public bool ObjectiveReached { get; set; }
    public int PrestigeCount { get; set; }
    public long Tick { get; set; }
    public long Iterations { get; set; }

    /// <summary>Non-null only when the run stopped on a problem (never reached Prestige-available,
    /// abandoned an island). A run that simply exhausted MaxCycles leaves this null.</summary>
    public string? FailureReason { get; set; }

    /// <summary>One entry per completed island, in order — PrestigeState.RunHistory's own stats.</summary>
    public List<PrestigeRunStats> IslandStats { get; } = new();

    /// <summary>
    /// Ce qui a déclenché le dernier prestige, tel qu'affiché en console : cible atteinte, plafond de
    /// temps, ou abandon sur stagnation. Null si aucune île n'a été terminée. Les trois ne se lisent
    /// pas pareil quand on juge une île sur ses points — « plafond de 8 h avec 57 pts » dit que le
    /// budget de temps est la contrainte, « 57 pts n'ont pas bougé en 8 passes » dit que l'île avait
    /// fini de produire bien avant et que lui donner plus de temps n'y changerait rien.
    /// </summary>
    public string? LastPrestigeReason { get; set; }
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
        "ResearchCompleted,UniqueBuildings,WonderLevel,WondersUnlocked,WonderPlaced,NpcCivsAlive," +
        "HasDeepestMine,HasCorruptionSpire,HasAbyssGate,AbyssGateEverUnlocked";

    public static EndlessRunResult Run(MainGameController controller, StrategyDefinition strategy, ObjectiveSpec globalObjective,
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
        long abandonIslandTicks = (long)Math.Round(endlessOptions.AbandonIslandAfterHours * 3600.0 * 100.0);

        var fullCsvPath = Path.GetFullPath(endlessOptions.CsvPath);
        var csvDir = Path.GetDirectoryName(fullCsvPath);
        if (!string.IsNullOrEmpty(csvDir))
            Directory.CreateDirectory(csvDir);

        using var csv = new StreamWriter(fullCsvPath, append: false);
        csv.WriteLine(CsvHeader);
        csv.Flush();

        Console.WriteLine($"Endless run started for strategy '{strategy.Name}' — objective: {globalObjective.Kind}.");
        Console.WriteLine($"CSV: {fullCsvPath}");

        var result = new EndlessRunResult();
        long iterationsUsed = 0;
        int prestigeCount = 0;
        long nextCheckpointTick = checkpointIntervalTicks;
        int lastAchievedPoints = 0;

        for (long cycle = 1; cycle <= endlessOptions.MaxCycles; cycle++)
        {
            int cycleIdx0 = (int)Math.Min(cycle - 1, int.MaxValue);
            bool pastFixedTargets = cycleIdx0 >= endlessOptions.PrestigePointTargets.Count;
            bool hasTimeCap = pastFixedTargets || endlessOptions.TimeCapAllIslands;
            int pointsTarget = pastFixedTargets
                ? Math.Max(1, lastAchievedPoints * 2)
                : endlessOptions.PrestigePointTargets[cycleIdx0];

            // Dernière île jugée sur ses points : ne pas la laisser prestiger sous le plancher demandé
            // simplement parce que sa cible calculée était plus basse (voir LastCyclePointsFloor).
            bool flooredLastCycle = cycle == endlessOptions.MaxCycles
                && endlessOptions.LastCyclePointsFloor > pointsTarget;
            if (flooredLastCycle) pointsTarget = endlessOptions.LastCyclePointsFloor;

            long islandStartTick = mainState.CurrentWorldState?.StartTick ?? clock.CurrentTick;

            string targetOrigin = flooredLastCycle ? " (plancher dernière île)"
                : pastFixedTargets ? " (2x previous)"
                : "";
            Console.WriteLine(hasTimeCap
                ? $"== Cycle {cycle}: target {pointsTarget} prestige points{targetOrigin}, or {endlessOptions.MaxIslandHoursAfterFixedTargets}h simulated — whichever comes first =="
                : $"== Cycle {cycle}: target {pointsTarget} prestige points{targetOrigin} ==");

            bool prestigedThisCycle = false;
            int pointsAtLastPassBoundary = -1;
            int stagnantPassCount = 0;
            // Retenues hors des boucles pour le diagnostic d'abandon, qui se déclenche une fois
            // toutes les passes épuisées, donc hors de la boucle de phases (voir DumpBlockedState).
            PriorityAutoplayStrategy? lastPriorityStrategy = null;
            StrategyPhase? lastPhase = null;

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
                    lastPriorityStrategy = priorityStrategy ?? lastPriorityStrategy;
                    lastPhase = phase;

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
                            result.ObjectiveReached = true;
                            return Finish(result, prestigeCount, clock, iterationsUsed);
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
                            // Avant le prestige : après, le monde courant est déjà l'île suivante.
                            var diagnostics = WonderDiagnostics.Capture(controller, mainState);
                            auto.TryPrestigeOnce();
                            if (mainState.GameRecord.TotalPrestigesPerformed > prestigesBefore)
                            {
                                prestigeCount++;
                                result.LastPrestigeReason = reason;
                                Console.WriteLine($"[trigger] {reason}");
                                var stats = LogPrestige(csv, cycle, phaseIndex, phase.Kind, iterationsUsed, prestigeCount, pointsTarget, clock, mainState, diagnostics);
                                lastAchievedPoints = stats?.PrestigePoints ?? 0;
                                if (stats != null)
                                    result.IslandStats.Add(stats);
                            }
                            prestigedThisCycle = true;
                            break;
                        }

                        // Hard stop: this island has run long enough that it clearly isn't going to
                        // prestige at all (an Imperial Port it can't build, points that never reach 20).
                        // Without it the cycle would keep grinding until MaxPassesPerCycle — see
                        // EndlessRunOptions.AbandonIslandAfterHours.
                        if (abandonIslandTicks > 0 && islandAge >= abandonIslandTicks && !controller.PrestigeController.PrestigeIsAvailable())
                        {
                            string why = DescribeMissingPrestigeRequirements(controller, currentPoints);
                            Console.WriteLine(
                                $"Cycle {cycle}: abandoned after {FormatHours(islandAge)}h on this island without ever reaching " +
                                $"Prestige-available ({why}).");
                            DumpBlockedState(controller, phase, priorityStrategy);
                            WriteRow(csv, "Abandoned", cycle, phaseIndex, phase.Kind, iterationsUsed, prestigeCount, pointsTarget, clock, controller, mainState);
                            csv.Flush();
                            result.FailureReason =
                                $"island {cycle}: no prestige after {FormatHours(islandAge)}h simulated ({why})";
                            return Finish(result, prestigeCount, clock, iterationsUsed);
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
                string why = DescribeMissingPrestigeRequirements(controller, controller.PrestigeController.CalculatePrestigePoints());
                Console.WriteLine(
                    $"Cycle {cycle}: gave up after {endlessOptions.MaxPassesPerCycle} passes without ever reaching " +
                    $"Prestige-available ({why}) — aborting endless run.");
                DumpBlockedState(controller, lastPhase, lastPriorityStrategy);
                csv.Flush();
                result.FailureReason = $"island {cycle}: no prestige after {endlessOptions.MaxPassesPerCycle} passes ({why})";
                return Finish(result, prestigeCount, clock, iterationsUsed);
            }
        }

        Console.WriteLine(
            $"Stopped after {endlessOptions.MaxCycles} cycles (--max-cycles). " +
            $"{prestigeCount} prestiges performed, tick {clock.CurrentTick} ({FormatHours(clock.CurrentTick)} simulated hours).");
        return Finish(result, prestigeCount, clock, iterationsUsed);
    }

    private static EndlessRunResult Finish(EndlessRunResult result, int prestigeCount, GameClock clock, long iterationsUsed)
    {
        result.PrestigeCount = prestigeCount;
        result.Tick = clock.CurrentTick;
        result.Iterations = iterationsUsed;
        return result;
    }

    /// <summary>
    /// Why this island never became prestigeable. Two things are worth telling apart, and neither is
    /// visible from the points count alone:
    /// - which half of PrestigeController.PrestigeIsAvailable() is missing — "20 points short" is a
    ///   pacing problem, "no Imperial Port" is usually structural (no coastal city this civilization
    ///   can build here, or a Seaport it cannot level to 4);
    /// - whether the map still offers anywhere to expand. Zero buildable vertices *and* zero buildable
    ///   roads means the run is genuinely walled in (city-placement rules, terrain); a healthy count
    ///   alongside a stalled city count means the autoplayer is the one not getting there.
    /// </summary>
    private static string DescribeMissingPrestigeRequirements(MainGameController controller, int currentPoints)
    {
        var civ = controller.CurrentMainState?.CurrentWorldState?.Civilizations.FirstOrDefault(c => !c.IsNpc);
        bool hasPort = civ?.UniqueBuildings.Contains(BuildingType.ImperialPort) ?? false;
        bool hasPoints = currentPoints >= PrestigeControllerRequiredPoints;

        string missing =
            !hasPort && !hasPoints ? $"no Imperial Port, and {currentPoints}/{PrestigeControllerRequiredPoints} points"
            : !hasPort ? $"no Imperial Port ({currentPoints} points)"
            : $"{currentPoints}/{PrestigeControllerRequiredPoints} points";

        if (civ == null) return missing;

        int buildableVertices = controller.CityBuilderController.GetBuildableVertices(civ.Index).Count;
        int buildableRoads = controller.RoadController.GetBuildableRoads(civ.Index).Count;
        return $"{missing}; {civ.Cities.Count} cities, {buildableVertices} buildable vertices, {buildableRoads} buildable roads";
    }

    /// <summary>
    /// Ce qu'il faut savoir d'une île abandonnée pour ne pas avoir à rouvrir la sauvegarde exportée :
    /// l'objectif exact sur lequel la stratégie s'est arrêtée (tout ce qui le suit dans la liste de
    /// priorités est gelé avec lui), l'état du stock et de la production — un blocage d'autoplay est le
    /// plus souvent une ressource dont la production est nulle —, et le contenu de chaque ville.
    /// </summary>
    private static void DumpBlockedState(MainGameController controller, StrategyPhase? phase,
        PriorityAutoplayStrategy? priorityStrategy)
    {
        var civ = controller.CurrentMainState?.CurrentWorldState?.Civilizations.FirstOrDefault(c => !c.IsNpc);
        if (civ == null) return;

        var diagnosticStrategy = phase == null ? priorityStrategy
            : StrategyRunner.BuildDiagnosticStrategy(phase, controller, priorityStrategy);
        if (diagnosticStrategy != null)
            Console.WriteLine($"  bloqué sur l'objectif {diagnosticStrategy.DescribeFirstIncomplete()}");

        var autoplayer = StrategyRunner.BuildAutoplayer(controller);
        Console.WriteLine("  expansion : " + autoplayer.DescribeExpansionState());

        // Militaire : « il reste des PNJ » ne dit pas si la guerre est perdue, mal armée ou jamais
        // livrée. Une cible prioritaire hors de portée avec des soldats en caserne est le cas qui
        // trompe le plus — la liste de priorités bascule alors sur son repli d'expansion, qui s'agite
        // beaucoup sans jamais rapprocher le front d'un seul cran.
        var target = autoplayer.FindPriorityAttackTarget();
        autoplayer.PriorityTargetCivilization = target;
        int soldiers = 0;
        foreach (var city in civ.Cities) soldiers += city.Soldiers;
        var npcs = controller.CurrentMainState?.CurrentWorldState?.Civilizations.Where(c => c.IsNpc && c.Cities.Count > 0).ToList() ?? new();
        Console.WriteLine($"  militaire : {soldiers} soldats, {npcs.Count} civ PNJ vivantes " +
                          $"({string.Join(", ", npcs.Select(c => $"{c.Cities.Count} villes"))}), " +
                          $"cible prioritaire : {(target == null ? "aucune" : $"{target.Cities.Count} villes")}, " +
                          $"à portée : {autoplayer.HasAttackableTargetInRange()}");

        var production = controller.HarvestController.GetAverageProductionRatesPerSecond(civ.Index);
        Console.WriteLine("  stock (production/s) : " + string.Join(", ", civ.Resources
            .Select(kv => $"{kv.Key} {kv.Value:0.#}" + (production.TryGetValue(kv.Key, out var rate) && rate > 0 ? $" (+{rate:0.###})" : " (+0)"))));

        foreach (var city in civ.Cities)
            Console.WriteLine($"  ville {city.Position} : " +
                              string.Join(", ", city.Buildings.Select(b => $"{b.Type}{b.Level}")));
    }

    private const int PrestigeControllerRequiredPoints = 20; // SettlersOfIdlestan.Controller.Expand.PrestigeController.PrestigeRequiredPoints
    private const int IdleBreakThreshold = 300; // consecutive no-op iterations (≈150 simulated seconds at the default time step) before giving up on a phase early
    private const int StagnantPassLimit = 8; // full passes through every phase with zero prestige-point movement before giving up on the whole cycle

    /// <summary>
    /// État de la Merveille et des PNJ à un instant donné. <c>WonderLevel</c> à 0 ne dit pas laquelle
    /// des trois marches a manqué : <see cref="WondersUnlocked"/> (recherche Architecture, persistante
    /// d'une île à l'autre) sépare « jamais débloquée » de « débloquée mais jamais posée », et
    /// <see cref="NpcCivsAlive"/> donne la raison du second cas — en mode agressif,
    /// WonderInvestmentObjective est gaté sur l'élimination de toutes les civilisations PNJ.
    ///
    /// <para>Capturé, et non recalculé au moment d'écrire la ligne : sur une ligne Prestige, le monde
    /// courant est déjà l'île <i>suivante</i>, donc lire l'état vivant y décrirait la mauvaise île.</para>
    /// </summary>
    private readonly record struct WonderDiagnostics(bool WondersUnlocked, bool WonderPlaced, int NpcCivsAlive)
    {
        public static WonderDiagnostics Capture(MainGameController controller, MainGameState mainState)
        {
            var worldState = mainState.CurrentWorldState;
            if (worldState == null) return default;

            var civ = worldState.Civilizations.FirstOrDefault(c => !c.IsNpc);
            int npcCivsAlive = 0;
            foreach (var c in worldState.Civilizations)
                if (c.IsNpc && c.Cities.Count > 0) npcCivsAlive++;

            return new WonderDiagnostics(
                civ != null && controller.WonderController.HasWondersUnlocked(civ),
                worldState.Features.OfType<Wonder>().Any(),
                npcCivsAlive);
        }
    }

    /// <summary>Logs the prestige that just happened and returns its recorded stats (null if the run
    /// history somehow has no entry — the caller then just has nothing to report for this island).</summary>
    private static PrestigeRunStats? LogPrestige(StreamWriter csv, long cycle, int phaseIndex, PhaseKind phaseKind,
        long iterationsUsed, int prestigeCount, int pointsTarget, GameClock clock, MainGameState mainState,
        WonderDiagnostics diagnostics)
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
            diagnostics.WondersUnlocked, diagnostics.WonderPlaced, diagnostics.NpcCivsAlive,
            stats?.HasDeepestMine ?? false, stats?.HasCorruptionSpire ?? false, stats?.HasAbyssGate ?? false,
            mainState.GameRecord.HasBuiltAbyssGate,
        }));
        csv.Flush();

        return stats;
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

        // La ligne CSV elle-même passe par WriteRow : ces deux writers étaient des copies l'une de
        // l'autre, et n'en ayant mis à jour qu'une en ajoutant des colonnes, les lignes Checkpoint se
        // sont retrouvées avec trois champs de moins que l'en-tête — décalage silencieux à la lecture.
        WriteRow(csv, "Checkpoint", cycle, phaseIndex, phaseKind, iterationsUsed, prestigeCount, pointsTarget,
            clock, controller, mainState);
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
        var diagnostics = WonderDiagnostics.Capture(controller, mainState);

        csv.WriteLine(string.Join(',', new object?[]
        {
            eventType, cycle, phaseIndex, phaseKind, iterationsUsed, clock.CurrentTick, FormatHours(clock.CurrentTick),
            prestigeCount, pointsTarget,
            islandTicks, worldState?.WorldId ?? 0, mainState.PrestigeState?.Tier ?? 0,
            civ?.Cities.Count ?? 0, allBuildings.Count, allBuildings.Sum(b => b.Level),
            controller.PrestigeController.CalculatePrestigePoints(),
            worldState?.RunRecord?.ResearchCompleted ?? 0, allBuildings.Count(b => b.IsUnique), wonder?.Level ?? 0,
            diagnostics.WondersUnlocked, diagnostics.WonderPlaced, diagnostics.NpcCivsAlive,
            hasDeepestMine, hasCorruptionSpire, hasAbyssGate,
            mainState.GameRecord.HasBuiltAbyssGate,
        }));
    }

    /// <summary>Ticks (1 tick = 0.01 simulated second) formatted as simulated hours, 2 decimals.</summary>
    private static string FormatHours(long ticks)
        => (ticks / 100.0 / 3600.0).ToString("F2", CultureInfo.InvariantCulture);
}
