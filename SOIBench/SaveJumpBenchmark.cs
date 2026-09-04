using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;

namespace SOIBench;

/// <summary>Coût d'un saut de temps rejoué sur une vraie sauvegarde.</summary>
public sealed class SaveJumpResult
{
    public required string SavePath { get; init; }
    public required string Describe { get; init; }
    public required long TotalTicks { get; init; }
    public required long ChunkTicks { get; init; }
    public required double TotalMs { get; init; }
    public required long AllocatedBytes { get; init; }
    public required IReadOnlyList<ControllerCost> ControllerCosts { get; init; }
    public bool HasControllerBreakdown { get; init; }
    public required IReadOnlyList<ControllerCost> HarvestStepCosts { get; init; }
    public required IReadOnlyList<AllocationSample> AllocationSamples { get; init; }

    public long Events => TotalTicks / ChunkTicks;
}

/// <summary>
/// Rejoue un saut de temps (celui de <c>TimeJumpService</c>) sur une sauvegarde réelle et attribue
/// le temps à chaque abonné de l'horloge.
///
/// <para><b>Pourquoi une vraie sauvegarde plutôt que la fixture synthétique.</b>
/// <see cref="EndGameStateFactory"/> ne pose que ~15 features là où une partie avancée en compte
/// des milliers (Dominion, Corruption), ne fait pas croître la carte via <c>AutoExtendController</c>
/// et ne sème aucune civilisation agressive en Inframonde. Tout ce qui parcourt <c>Features</c> ou
/// les civs PNJ y est donc massivement sous-estimé — exactement les postes qui dominent un saut
/// d'une heure.</para>
///
/// <para><b>Le régime mesuré est celui du jeu.</b> <c>TimeJumpService</c> découpe en tranches de
/// 10 000 ticks, pas en 100 comme le rattrapage hors-ligne : une heure = 360 000 ticks = 36
/// événements <c>Advanced</c> seulement. Le coût par événement n'a rien à voir avec celui d'une
/// tranche de 100 ticks, et extrapoler de l'un à l'autre donne un chiffre faux.</para>
/// </summary>
public static class SaveJumpBenchmark
{
    /// <summary>Même découpage que <c>TimeJumpService.ChunkTicks</c>.</summary>
    public const long TimeJumpChunkTicks = 10_000;

    /// <summary>1 tick = 0,01 s réelle.</summary>
    public const long TicksPerSecond = 100;

    public static MainGameController Load(string path)
    {
        string data = File.ReadAllText(path);
        var controller = new MainGameController();
        controller.ImportMainState(data);
        return controller;
    }

    public static string Describe(MainGameController controller)
    {
        var state = controller.CurrentMainState ?? throw new InvalidOperationException("Sauvegarde sans état.");
        var world = state.CurrentWorldState ?? throw new InvalidOperationException("Sauvegarde sans monde.");

        int cities = world.Civilizations.Sum(c => c.Cities.Count);
        int playerCities = world.PlayerCivilization?.Cities.Count ?? 0;
        int roads = world.Civilizations.Sum(c => c.Roads.Count);
        int buildings = world.Civilizations.Sum(c => c.Cities.Sum(city => city.Buildings.Count));

        var layers = world.Layers
            .OrderBy(kv => kv.Key)
            .Select(kv => $"z{kv.Key}:{kv.Value.Map.Tiles.Count}h")
            .ToList();

        return $"île {world.WorldId} — {cities} villes ({playerCities} joueur) / {world.Civilizations.Count} civs / "
             + $"{roads} routes / {buildings} bâtiments / {world.Features.Count} features / "
             + $"tick {state.Clock.CurrentTick} [{string.Join(", ", layers)}]";
    }

    public static SaveJumpResult Run(string path, double hours, long chunkTicks, bool sampleAllocationTypes)
    {
        var controller = Load(path);
        string describe = Describe(controller);
        var clock = controller.Clock ?? throw new InvalidOperationException("Sauvegarde sans horloge.");

        long totalTicks = (long)(hours * 3600 * TicksPerSecond);
        totalTicks -= totalTicks % chunkTicks;
        if (totalTicks <= 0) throw new ArgumentException("Durée de saut trop courte pour le découpage demandé.");

        // La banque est remplie d'office : on mesure le coût de la simulation, pas la légitimité du
        // saut. TimeJumpService, lui, refuse un saut que la banque ne couvre pas.
        clock.OfflineBankTicks = Math.Max(clock.OfflineBankTicks, totalTicks);

        // Avant le ClockProfiler : celui-ci capture les délégués abonnés à l'horloge, dont
        // HarvestController.OnClockAdvanced, qui lira _steps à chaque appel — l'ordre n'a donc pas
        // d'importance ici, mais rester dans cet ordre garde les deux profileurs indépendants.
        using var stepProfiler = new HarvestStepProfiler(controller.HarvestController);
        using var profiler = new ClockProfiler(clock);

        AllocationSampler? sampler = null;
        if (sampleAllocationTypes)
        {
            sampler = new AllocationSampler();
            sampler.Start();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        long done = 0;
        while (done < totalTicks)
        {
            long chunk = Math.Min(chunkTicks, totalTicks - done);
            if (!clock.AdvanceFromBank(chunk, chunk)) break;
            done += chunk;
        }
        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        sampler?.Stop();
        var samples = sampler?.Samples ?? (IReadOnlyList<AllocationSample>)Array.Empty<AllocationSample>();
        sampler?.Dispose();

        return new SaveJumpResult
        {
            SavePath = path,
            Describe = describe,
            TotalTicks = done,
            ChunkTicks = chunkTicks,
            TotalMs = stopwatch.Elapsed.TotalMilliseconds,
            AllocatedBytes = allocated,
            HasControllerBreakdown = profiler.IsAttached,
            ControllerCosts = profiler.Costs.OrderByDescending(c => c.ElapsedTicks).ToList(),
            HarvestStepCosts = stepProfiler.Costs,
            AllocationSamples = samples,
        };
    }
}
