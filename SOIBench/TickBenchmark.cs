using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SOIBench;

public sealed class TickBenchmarkOptions
{
    /// <summary>Nombre d'événements <c>GameClock.Advanced</c> mesurés par manche.</summary>
    public int Events { get; set; } = 500;

    /// <summary>
    /// Manches indépendantes ; la valeur retenue est la médiane, pour que ni une collecte du GC ni
    /// une préemption de l'OS ne décide du résultat.
    /// </summary>
    public int Rounds { get; set; } = 5;

    /// <summary>Événements joués avant la mesure (JIT, caches froids, premières allocations).</summary>
    public int WarmupEvents { get; set; } = 200;

    /// <summary>
    /// Ticks de simulation par événement. 100 = le découpage par défaut de
    /// <c>GameClock.SimulateAdvance</c> (rattrapage hors-ligne). En jeu, l'horloge lève un seul
    /// événement par frame, soit ~1,7 tick à 60 fps en vitesse x1.
    /// </summary>
    public int TicksPerEvent { get; set; } = 100;
}

public sealed class TickBenchmarkResult
{
    public required EndGameFixture Fixture { get; init; }
    public required TickBenchmarkOptions Options { get; init; }
    public int MeasuredEvents { get; init; }
    public double TotalMs { get; init; }
    public long AllocatedBytes { get; init; }

    /// <summary>Moyenne par événement de chaque manche, dans l'ordre d'exécution.</summary>
    public required IReadOnlyList<double> RoundMsPerEvent { get; init; }

    public required IReadOnlyList<ControllerCost> ControllerCosts { get; init; }
    public bool HasControllerBreakdown { get; init; }

    /// <summary>Médiane des manches — la valeur à citer.</summary>
    public double MsPerEvent => Median(RoundMsPerEvent);

    public double MinMsPerEvent => RoundMsPerEvent.Count == 0 ? 0 : RoundMsPerEvent.Min();
    public double MaxMsPerEvent => RoundMsPerEvent.Count == 0 ? 0 : RoundMsPerEvent.Max();

    public double EventsPerSecond => MsPerEvent <= 0 ? 0 : 1000.0 / MsPerEvent;
    public double MicrosecondsPerCity => Fixture.CityCount == 0 ? 0 : MsPerEvent * 1000.0 / Fixture.CityCount;
    public double AllocatedBytesPerEvent => MeasuredEvents == 0 ? 0 : (double)AllocatedBytes / MeasuredEvents;

    /// <summary>Découpage utilisé par <c>GameClock.AdvanceFromBank</c> pour le rattrapage hors-ligne.</summary>
    public const int OfflineChunkTicks = 100;

    /// <summary>
    /// Secondes de CPU nécessaires pour rattraper <paramref name="hours"/> d'absence. C'est le pire
    /// cas réel : <c>GameClock.AdvanceFromBank</c> découpe la banque hors-ligne en tranches de
    /// <see cref="OfflineChunkTicks"/> ticks, donc le joueur qui revient déclenche un événement par
    /// tranche, d'affilée. N'a de sens que si <c>TicksPerEvent</c> vaut cette même valeur : le coût
    /// d'un événement dépend du nombre de ticks qu'il couvre, extrapoler depuis une autre taille de
    /// tranche donnerait un chiffre faux.
    /// </summary>
    public double OfflineCatchUpSeconds(double hours)
        => hours * 3600.0 * 100.0 / OfflineChunkTicks * MsPerEvent / 1000.0;

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}

public static class TickBenchmark
{
    /// <summary>
    /// Fait passer le JIT sur tout le chemin de simulation avec un état minuscule, avant toute
    /// mesure. Sans ça le premier état mesuré du processus paie la compilation de la quinzaine de
    /// contrôleurs pour tous les suivants, et sort artificiellement plus lent — ce qui rend la table
    /// de montée en charge non monotone et illisible.
    /// </summary>
    public static void WarmUpProcess()
    {
        var fixture = EndGameStateFactory.Build(new EndGameIslandOptions
        {
            TargetCityCount = 12,
            SurfaceRadius = 5,
            UnderworldRadius = 4,
            BuildingLevel = 2,
        });

        var clock = fixture.Controller.Clock!;
        for (int i = 0; i < 200; i++)
            clock.SimulateAdvance(100, 100);
    }

    public static TickBenchmarkResult Run(EndGameFixture fixture, TickBenchmarkOptions options)
    {
        var clock = fixture.Controller.Clock
            ?? throw new InvalidOperationException("L'état généré n'a pas d'horloge.");

        using var profiler = new ClockProfiler(clock);

        for (int i = 0; i < options.WarmupEvents; i++)
            clock.SimulateAdvance(options.TicksPerEvent, options.TicksPerEvent);

        // Un GC complet avant la mesure évite qu'une collecte due au préchauffage soit facturée aux
        // premiers événements mesurés.
        Collect();
        profiler.Reset();

        var roundMsPerEvent = new List<double>(options.Rounds);
        long allocated = 0;
        double totalMs = 0;

        for (int round = 0; round < options.Rounds; round++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < options.Events; i++)
                clock.SimulateAdvance(options.TicksPerEvent, options.TicksPerEvent);
            stopwatch.Stop();

            allocated += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            totalMs += stopwatch.Elapsed.TotalMilliseconds;
            roundMsPerEvent.Add(stopwatch.Elapsed.TotalMilliseconds / options.Events);
        }

        return new TickBenchmarkResult
        {
            Fixture = fixture,
            Options = options,
            MeasuredEvents = options.Events * options.Rounds,
            TotalMs = totalMs,
            AllocatedBytes = allocated,
            RoundMsPerEvent = roundMsPerEvent,
            HasControllerBreakdown = profiler.IsAttached,
            ControllerCosts = profiler.Costs.OrderByDescending(c => c.ElapsedTicks).ToList(),
        };
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
