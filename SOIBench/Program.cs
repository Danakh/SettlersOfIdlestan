using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SOIBench;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Erreur : {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var options = BenchCliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var results = new List<TickBenchmarkResult>();

        Console.Write("Préchauffage du JIT… ");
        TickBenchmark.WarmUpProcess();
        Console.WriteLine("ok");

        foreach (int cityCount in options.CityCounts)
        {
            var islandOptions = options.ToIslandOptions(cityCount);

            Console.Write($"Génération {cityCount} villes… ");
            var buildStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var fixture = EndGameStateFactory.Build(islandOptions);
            buildStopwatch.Stop();
            Console.WriteLine($"{buildStopwatch.Elapsed.TotalSeconds:F1} s");
            Console.WriteLine($"  {fixture.Describe()}");

            if (options.SaveFixtureDirectory != null)
            {
                Directory.CreateDirectory(options.SaveFixtureDirectory);
                string path = Path.Combine(options.SaveFixtureDirectory,
                    $"endgame_w{islandOptions.WorldId}_c{fixture.CityCount}_s{islandOptions.Seed}.json");
                File.WriteAllText(path, fixture.Controller.ExportMainState());
                Console.WriteLine($"  sauvegarde → {path}");
            }

            var result = TickBenchmark.Run(fixture, options.ToBenchmarkOptions());
            results.Add(result);
            PrintResult(result, options.BreakdownTop);
        }

        if (results.Count > 1) PrintScaling(results);
        if (options.CsvPath != null) WriteCsv(results, options.CsvPath);

        return 0;
    }

    private static void PrintResult(TickBenchmarkResult result, int breakdownTop)
    {
        Console.WriteLine();
        Console.WriteLine($"  ── simulation : {result.Options.Rounds} × {result.Options.Events} événements de {result.Options.TicksPerEvent} ticks ──");
        Console.WriteLine($"  ms / événement (médiane)  : {result.MsPerEvent,10:F3}   [min {result.MinMsPerEvent:F3} / max {result.MaxMsPerEvent:F3}]");
        Console.WriteLine($"  µs / événement / ville    : {result.MicrosecondsPerCity,10:F2}");
        Console.WriteLine($"  allocations / événement   : {FormatBytes(result.AllocatedBytesPerEvent),10}");
        Console.WriteLine($"  événements / s            : {result.EventsPerSecond,10:F0}");
        Console.WriteLine(result.Options.TicksPerEvent == TickBenchmarkResult.OfflineChunkTicks
            ? $"  rattrapage 8 h hors-ligne : {result.OfflineCatchUpSeconds(8),10:F1} s de CPU"
            : $"  rattrapage 8 h hors-ligne :        n/a   (rejouer avec --ticks-per-event {TickBenchmarkResult.OfflineChunkTicks})");

        if (!result.HasControllerBreakdown)
        {
            Console.WriteLine("  (répartition par contrôleur indisponible : champ GameClock.Advanced introuvable)");
            Console.WriteLine();
            return;
        }

        double total = result.ControllerCosts.Sum(c => c.TotalMs);
        Console.WriteLine();
        Console.WriteLine($"  {"contrôleur",-34}{"ms/évt",10}{"%",8}{"alloc/évt",14}");
        foreach (var cost in result.ControllerCosts.Take(breakdownTop))
        {
            double msPerEvent = cost.Calls == 0 ? 0 : cost.TotalMs / cost.Calls;
            double share = total <= 0 ? 0 : cost.TotalMs / total * 100.0;
            double allocPerEvent = cost.Calls == 0 ? 0 : (double)cost.AllocatedBytes / cost.Calls;
            Console.WriteLine($"  {cost.Name,-34}{msPerEvent,10:F3}{share,7:F1}%{FormatBytes(allocPerEvent),14}");
        }

        double measured = result.ControllerCosts.Sum(c => c.TotalMs);
        double unattributed = result.TotalMs - measured;
        if (unattributed > 0.01 * result.TotalMs)
            Console.WriteLine($"  {"(non attribué)",-34}{unattributed / result.MeasuredEvents,10:F3}{unattributed / result.TotalMs * 100.0,7:F1}%");
        Console.WriteLine();
    }

    /// <summary>
    /// Table de montée en charge. La pente log-log est ce qu'on cherche vraiment : ≈1 = coût linéaire
    /// par ville (acceptable, on optimise les constantes), &gt;1,3 = quelque chose de quadratique se
    /// cache dans le chemin chaud et aucune micro-optimisation ne le sauvera.
    /// </summary>
    private static void PrintScaling(IReadOnlyList<TickBenchmarkResult> results)
    {
        Console.WriteLine("── montée en charge ──");
        Console.WriteLine($"  {"villes",8}{"hexes",10}{"routes",10}{"ms/évt",12}{"µs/ville",12}{"alloc/évt",14}{"pente",8}");

        var ordered = results.OrderBy(r => r.Fixture.CityCount).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var r = ordered[i];
            // Pente locale entre ce point et le précédent : plus parlante que l'ajustement global dès
            // que le coût fixe domine les petites tailles et écrase la pente moyenne.
            string localSlope = "—";
            if (i > 0)
            {
                var previous = ordered[i - 1];
                if (previous.Fixture.CityCount > 0 && previous.MsPerEvent > 0 && r.Fixture.CityCount != previous.Fixture.CityCount)
                    localSlope = (Math.Log(r.MsPerEvent / previous.MsPerEvent)
                                / Math.Log((double)r.Fixture.CityCount / previous.Fixture.CityCount)).ToString("F2");
            }

            Console.WriteLine($"  {r.Fixture.CityCount,8}{r.Fixture.HexCount,10}{r.Fixture.RoadCount,10}"
                            + $"{r.MsPerEvent,12:F3}{r.MicrosecondsPerCity,12:F2}{FormatBytes(r.AllocatedBytesPerEvent),14}{localSlope,8}");
        }

        var points = results
            .Where(r => r.Fixture.CityCount > 0 && r.MsPerEvent > 0)
            .Select(r => (X: Math.Log(r.Fixture.CityCount), Y: Math.Log(r.MsPerEvent)))
            .ToList();

        if (points.Count < 2) return;

        double meanX = points.Average(p => p.X);
        double meanY = points.Average(p => p.Y);
        double covariance = points.Sum(p => (p.X - meanX) * (p.Y - meanY));
        double variance = points.Sum(p => (p.X - meanX) * (p.X - meanX));
        if (variance <= 0) return;

        double slope = covariance / variance;
        string verdict = slope switch
        {
            < 0.7 => "sous-linéaire — le coût fixe domine",
            < 1.2 => "linéaire en nombre de villes",
            < 1.6 => "super-linéaire — un chemin chaud parcourt plus que ses propres villes",
            _ => "quadratique — chercher un produit cartésien villes × villes",
        };
        Console.WriteLine($"  pente log-log : {slope:F2}  ({verdict})");
        Console.WriteLine();
    }

    private static void WriteCsv(IReadOnlyList<TickBenchmarkResult> results, string path)
    {
        var csv = new StringBuilder();
        csv.AppendLine("cities;playerCities;civs;hexes;roads;buildings;ticksPerEvent;msPerEvent;usPerCity;allocBytesPerEvent;eventsPerSecond;controller;controllerMsPerEvent");

        foreach (var r in results)
        {
            string prefix = string.Join(';', new[]
            {
                r.Fixture.CityCount.ToString(CultureInfo.InvariantCulture),
                r.Fixture.PlayerCityCount.ToString(CultureInfo.InvariantCulture),
                r.Fixture.CivilizationCount.ToString(CultureInfo.InvariantCulture),
                r.Fixture.HexCount.ToString(CultureInfo.InvariantCulture),
                r.Fixture.RoadCount.ToString(CultureInfo.InvariantCulture),
                r.Fixture.BuildingCount.ToString(CultureInfo.InvariantCulture),
                r.Options.TicksPerEvent.ToString(CultureInfo.InvariantCulture),
                r.MsPerEvent.ToString("F4", CultureInfo.InvariantCulture),
                r.MicrosecondsPerCity.ToString("F3", CultureInfo.InvariantCulture),
                r.AllocatedBytesPerEvent.ToString("F0", CultureInfo.InvariantCulture),
                r.EventsPerSecond.ToString("F1", CultureInfo.InvariantCulture),
            });

            csv.AppendLine($"{prefix};TOTAL;{r.MsPerEvent.ToString("F4", CultureInfo.InvariantCulture)}");
            foreach (var cost in r.ControllerCosts)
            {
                double msPerEvent = cost.Calls == 0 ? 0 : cost.TotalMs / cost.Calls;
                csv.AppendLine($"{prefix};{cost.Name};{msPerEvent.ToString("F4", CultureInfo.InvariantCulture)}");
            }
        }

        File.WriteAllText(path, csv.ToString());
        Console.WriteLine($"CSV écrit dans {path}");
    }

    private static string FormatBytes(double bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (1024 * 1024):F1} Mo",
        >= 1024 => $"{bytes / 1024:F1} Ko",
        _ => $"{bytes:F0} o",
    };

    private static void PrintHelp()
    {
        Console.WriteLine("""
            SOIBench — génère des îles de fin de partie synthétiques et mesure le coût de la
            simulation (un événement GameClock.Advanced) en fonction de leur taille.

            Usage :
              SOIBench [options]

            Génération de l'état :
              --cities <a,b,c>          Nombres de villes à comparer (défaut : 50,100,200,400).
                                         Plusieurs valeurs = table de montée en charge + pente log-log.
              --seed <n>                Graine de la partie (défaut : 20260809).
              --world-id <n>            Île de l'atlas (défaut : 7 ; >= 5 = île endgame).
              --prestige-points <n>     Points de prestige cumulés → Tier (défaut : 250000).
              --surface-radius <n>      Rayon de la couche de surface (défaut : 16).
              --underworld-radius <n>   Rayon de l'Inframonde (défaut : 14).
              --no-underworld           Ne génère que la surface.
              --building-level <n>      Niveau visé pour chaque bâtiment (défaut : 4).
              --soldiers <n>            Garnison par ville (défaut : 5).
              --player-share <0..1>     Part des villes revenant au joueur (défaut : 0.5).

            Mesure :
              --events <n>              Événements mesurés par manche (défaut : 500).
              --rounds <n>              Manches ; la médiane est retenue (défaut : 5).
              --warmup <n>              Événements de préchauffage (défaut : 200).
              --ticks-per-event <n>     Ticks par événement (défaut : 100 = découpage de
                                         SimulateAdvance ; 2 ≈ une frame à 60 fps en vitesse x1).
              --breakdown-top <n>       Contrôleurs affichés dans la répartition (défaut : 12).

            Sorties :
              --csv <path>              Écrit les résultats (total + par contrôleur) en CSV.
              --save-fixture <dir>      Exporte chaque état généré en sauvegarde chargeable par le jeu.
              --help                    Affiche cette aide.
            """);
    }
}

internal sealed class BenchCliOptions
{
    public bool ShowHelp { get; set; }
    public List<int> CityCounts { get; set; } = new() { 50, 100, 200, 400 };
    public int Seed { get; set; } = 20260809;
    public int WorldId { get; set; } = 7;
    public int PrestigePoints { get; set; } = 250_000;
    public int SurfaceRadius { get; set; } = 16;
    public int UnderworldRadius { get; set; } = 14;
    public bool IncludeUnderworld { get; set; } = true;
    public int BuildingLevel { get; set; } = 4;
    public int SoldiersPerCity { get; set; } = 5;
    public double PlayerCityShare { get; set; } = 0.5;
    public int Events { get; set; } = 500;
    public int Rounds { get; set; } = 5;
    public int WarmupEvents { get; set; } = 200;
    public int TicksPerEvent { get; set; } = 100;
    public int BreakdownTop { get; set; } = 12;
    public string? CsvPath { get; set; }
    public string? SaveFixtureDirectory { get; set; }

    public EndGameIslandOptions ToIslandOptions(int cityCount) => new()
    {
        Seed = Seed,
        WorldId = WorldId,
        TotalPrestigePointsEarned = PrestigePoints,
        TargetCityCount = cityCount,
        SurfaceRadius = SurfaceRadius,
        UnderworldRadius = UnderworldRadius,
        IncludeUnderworld = IncludeUnderworld,
        BuildingLevel = BuildingLevel,
        SoldiersPerCity = SoldiersPerCity,
        PlayerCityShare = PlayerCityShare,
    };

    public TickBenchmarkOptions ToBenchmarkOptions() => new()
    {
        Events = Events,
        Rounds = Rounds,
        WarmupEvents = WarmupEvents,
        TicksPerEvent = TicksPerEvent,
    };

    public static BenchCliOptions Parse(string[] args)
    {
        var options = new BenchCliOptions();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h": options.ShowHelp = true; break;
                case "--cities":
                    options.CityCounts = RequireValue(args, ref i)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(int.Parse).ToList();
                    break;
                case "--seed": options.Seed = int.Parse(RequireValue(args, ref i)); break;
                case "--world-id": options.WorldId = int.Parse(RequireValue(args, ref i)); break;
                case "--prestige-points": options.PrestigePoints = int.Parse(RequireValue(args, ref i)); break;
                case "--surface-radius": options.SurfaceRadius = int.Parse(RequireValue(args, ref i)); break;
                case "--underworld-radius": options.UnderworldRadius = int.Parse(RequireValue(args, ref i)); break;
                case "--no-underworld": options.IncludeUnderworld = false; break;
                case "--building-level": options.BuildingLevel = int.Parse(RequireValue(args, ref i)); break;
                case "--soldiers": options.SoldiersPerCity = int.Parse(RequireValue(args, ref i)); break;
                case "--player-share":
                    options.PlayerCityShare = double.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--events": options.Events = int.Parse(RequireValue(args, ref i)); break;
                case "--rounds": options.Rounds = int.Parse(RequireValue(args, ref i)); break;
                case "--warmup": options.WarmupEvents = int.Parse(RequireValue(args, ref i)); break;
                case "--ticks-per-event": options.TicksPerEvent = int.Parse(RequireValue(args, ref i)); break;
                case "--breakdown-top": options.BreakdownTop = int.Parse(RequireValue(args, ref i)); break;
                case "--csv": options.CsvPath = RequireValue(args, ref i); break;
                case "--save-fixture": options.SaveFixtureDirectory = RequireValue(args, ref i); break;
                default: throw new ArgumentException($"Argument inconnu : {args[i]}");
            }
        }

        if (!options.ShowHelp && options.CityCounts.Count == 0)
            throw new ArgumentException("--cities ne contient aucune valeur.");

        return options;
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException($"Valeur manquante pour {args[i]}");
        return args[++i];
    }
}
