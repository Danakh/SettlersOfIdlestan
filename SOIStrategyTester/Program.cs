using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Races;
using SOIStrategyTester.Model;

namespace SOIStrategyTester;

public static class Program
{
    /// <summary>Default --max-island-hours in endless mode (only applies past the fixed point targets).</summary>
    private const double DefaultEndlessIslandHours = 24.0;

    /// <summary>Default --max-island-hours in gauntlet mode, where it caps <i>every</i> island: lower,
    /// since 9 races x 4 islands run back to back and the question is reachability, not depth.</summary>
    private const double DefaultGauntletIslandHours = 8.0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var options = CliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var strategies = JsonSerializer.Deserialize<List<StrategyDefinition>>(File.ReadAllText(options.StrategiesPath!), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse strategies file: {options.StrategiesPath}");

        if (strategies.Count == 0)
            throw new InvalidOperationException("Strategies file contains no strategies.");

        var runOptions = new StrategyRunOptions
        {
            DefaultMaxIterationsPerPhase = options.MaxIterationsPerPhase,
            TimeStep = options.TimeStep,
        };

        if (options.RaceGauntlet)
        {
            if (strategies.Count > 1)
                Console.WriteLine($"Warning: --race-gauntlet only drives one strategy — using the first ('{strategies[0].Name}') and ignoring the other {strategies.Count - 1}.");

            bool allPassed = RaceGauntletRunner.Run(strategies[0], runOptions, new RaceGauntletOptions
            {
                Races = options.Races,
                Islands = options.Islands,
                Seed = options.Seed,
                OutputDirectory = options.GauntletOutputDirectory,
                PrestigePointTargets = options.PrestigePointTargets,
                MaxIslandHours = options.MaxIslandHours ?? DefaultGauntletIslandHours,
                AbandonIslandAfterHours = options.AbandonIslandHours,
                CheckpointHours = options.CheckpointHours,
                MinFinalIslandPrestigePoints = options.FinalIslandPoints,
            });
            return allPassed ? 0 : 1;
        }

        var globalObjective = JsonSerializer.Deserialize<ObjectiveSpec>(File.ReadAllText(options.ObjectivePath!), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse objective file: {options.ObjectivePath}");

        if (options.Endless)
        {
            if (strategies.Count > 1)
                Console.WriteLine($"Warning: --endless only drives one strategy — using the first ('{strategies[0].Name}') and ignoring the other {strategies.Count - 1}.");

            var endlessController = BuildController(options);
            var endlessOptions = new EndlessRunOptions
            {
                CsvPath = options.CsvOutputPath,
                CheckpointIntervalHours = options.CheckpointHours,
                MaxCycles = options.MaxCycles,
                PrestigePointTargets = options.PrestigePointTargets,
                MaxIslandHoursAfterFixedTargets = options.MaxIslandHours ?? DefaultEndlessIslandHours,
            };
            EndlessRunner.Run(endlessController, strategies[0], globalObjective, runOptions, endlessOptions);
            return 0;
        }

        var results = new List<StrategyRunResult>();
        foreach (var strategy in strategies)
        {
            Console.WriteLine($"Running strategy '{strategy.Name}'...");
            var controller = BuildController(options);
            var result = StrategyRunner.Run(controller, strategy, globalObjective, runOptions);
            results.Add(result);

            Console.WriteLine(result.Success
                ? $"  -> success in {result.Ticks} ticks ({result.Iterations} iterations)"
                : $"  -> FAILED after {result.Iterations} iterations: {result.FailureReason}");
        }

        var ranked = results
            .OrderBy(r => r.Success ? 0 : 1)
            .ThenBy(r => r.Ticks)
            .ToList();

        var outputPath = options.OutputPath ?? "results.json";
        File.WriteAllText(outputPath, JsonSerializer.Serialize(ranked, JsonOptions));
        Console.WriteLine($"Wrote results for {ranked.Count} strategies to {outputPath}");

        var winner = ranked.FirstOrDefault(r => r.Success);
        if (winner == null)
        {
            Console.WriteLine("No strategy reached the objective — no winner to record.");
            return 1;
        }

        var winningStrategy = strategies.First(s => s.Name == winner.StrategyName);
        var bestOutputPath = options.BestOutputPath
            ?? Path.Combine("Data", "Best", Path.GetFileNameWithoutExtension(options.StrategiesPath!) + ".best.json");
        Directory.CreateDirectory(Path.GetDirectoryName(bestOutputPath) is { Length: > 0 } dir ? dir : ".");
        File.WriteAllText(bestOutputPath, JsonSerializer.Serialize(new BestStrategyRecord
        {
            Objective = globalObjective,
            Strategy = winningStrategy,
            Result = winner,
        }, JsonOptions));

        Console.WriteLine($"Winner: '{winner.StrategyName}' in {winner.Ticks} ticks — recorded to {bestOutputPath}");
        return 0;
    }

    private static MainGameController BuildController(CliOptions options)
    {
        return options.SavePath != null
            ? GameStateFactory.FromSaveFile(options.SavePath)
            : GameStateFactory.NewGame(options.WorldId, options.Seed);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            SOIStrategyTester — runs autoplay strategies against a save (or a new game) and scores
            each one by how many game ticks it took to reach a given objective.

            Usage:
              SOIStrategyTester --objective <objective.json> --strategies <strategies.json> [options]

            Starting state (choose one):
              --save <path>            Load a save file (same format as MainGameController.ExportMainState).
              --new-game                Start a fresh game instead (default if --save is omitted).
              --world-id <n>            World id for --new-game (default: AtlasController.GetFirstWorldId()).
              --seed <n>                PRNG seed for --new-game (default: random).

            Other options:
              --output <path>          Where to write the ranked results (default: results.json).
              --best-output <path>     Where to record the winning strategy (default: Data/Best/<strategies-file>.best.json).
              --max-iterations <n>     Default max iterations per phase (default: 20000).
              --time-step <seconds>    Simulated seconds advanced per iteration (default: 0.5).
              --help                   Show this message.

            Endless mode — loops a single strategy across prestige cycles until its objective is met
            (e.g. Data/Objectives/abyss-gate-unlocked.json) instead of racing several strategies once:
              --endless                 Drive --strategies' first strategy on a loop instead of racing
                                         every strategy in the file once. Cycles back to phase 0 whenever
                                         the strategy runs out of phases and the global objective still
                                         isn't met — pair with a strategy whose last phase is Prestige.
              --csv-output <path>       Where to continuously append progress rows (default: run_current.csv).
              --checkpoint-hours <n>    Simulated-hours interval between checkpoint rows/console lines
                                         (default: 1).
              --max-cycles <n>          Safety cap on the number of phase-loop cycles (default: 100000).
              --prestige-point-targets <a,b,c,...>
                                         Comma-separated prestige-point target for the 1st, 2nd, 3rd...
                                         prestige (default: 35,80,500,1000). Once past the list, each
                                         island's target instead doubles the previous island's actual
                                         points.
              --max-island-hours <n>    Once past --prestige-point-targets, each island prestiges as soon
                                         as it reaches its doubled target OR this many simulated hours have
                                         passed, whichever comes first (default: 24).

            Race gauntlet — plays the first N islands once per race, each starting with the divine powers
            its tier requires, and reports which races get through (see RaceGauntletRunner):
              --race-gauntlet           Run the gauntlet instead of racing strategies. --objective is not
                                         used (the goal is fixed: one prestige per island); --strategies
                                         defaults to Data/Strategies/race-gauntlet.json.
              --races <a,b,...>         Races to run (default: every implemented race).
              --islands <n>             Islands each race must clear (default: 4).
              --seed <n>                Shared seed — same for every race, which is what makes the run
                                         comparable. Omit for a random one.
              --gauntlet-output <dir>   Per-race CSVs + summary.csv/summary.json (default: race-gauntlet).
              --max-island-hours <n>    Simulated-hours cap on EVERY island here, not just the ones past
                                         the fixed targets (default: 8).
              --abandon-island-hours <n> Simulated hours on one island without ever reaching
                                         Prestige-available before the race is declared blocked
                                         (default: 24).
              --final-island-points <n> Also require the LAST island to be worth n prestige points for
                                         the race to pass, and raise that island's own prestige target
                                         to n so it isn't cut short below it (default: 0, verdict on
                                         the island count alone). The end-game round is
                                         `--islands 5 --final-island-points 100`.
            """);
    }
}

internal class BestStrategyRecord
{
    public ObjectiveSpec Objective { get; set; } = new();
    public StrategyDefinition Strategy { get; set; } = new();
    public StrategyRunResult Result { get; set; } = new();
}

internal class CliOptions
{
    public bool ShowHelp { get; set; }
    public string? SavePath { get; set; }
    public int? WorldId { get; set; }
    public int? Seed { get; set; }
    public string? ObjectivePath { get; set; }
    public string? StrategiesPath { get; set; }
    public string? OutputPath { get; set; }
    public string? BestOutputPath { get; set; }
    public int MaxIterationsPerPhase { get; set; } = 20000;
    public double TimeStep { get; set; } = 0.5;
    public bool Endless { get; set; }
    public string CsvOutputPath { get; set; } = "run_current.csv";
    public double CheckpointHours { get; set; } = 1.0;
    public long MaxCycles { get; set; } = 100_000;
    public List<int> PrestigePointTargets { get; set; } = new() { 35, 80, 500, 1000 };

    /// <summary>Null when unspecified — endless and gauntlet modes have different defaults for it.</summary>
    public double? MaxIslandHours { get; set; }

    public bool RaceGauntlet { get; set; }
    public List<RaceId> Races { get; set; } = new();
    public int Islands { get; set; } = 4;
    public string GauntletOutputDirectory { get; set; } = "race-gauntlet";
    public double AbandonIslandHours { get; set; } = 24.0;

    /// <summary>0 = verdict sur le seul nombre d'îles (voir RaceGauntletOptions.MinFinalIslandPrestigePoints).</summary>
    public int FinalIslandPoints { get; set; }

    private const string DefaultGauntletStrategiesPath = "Data/Strategies/race-gauntlet.json";

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    options.ShowHelp = true;
                    break;
                case "--save":
                    options.SavePath = RequireValue(args, ref i);
                    break;
                case "--new-game":
                    options.SavePath = null;
                    break;
                case "--world-id":
                    options.WorldId = int.Parse(RequireValue(args, ref i));
                    break;
                case "--seed":
                    options.Seed = int.Parse(RequireValue(args, ref i));
                    break;
                case "--objective":
                    options.ObjectivePath = RequireValue(args, ref i);
                    break;
                case "--strategies":
                    options.StrategiesPath = RequireValue(args, ref i);
                    break;
                case "--output":
                    options.OutputPath = RequireValue(args, ref i);
                    break;
                case "--best-output":
                    options.BestOutputPath = RequireValue(args, ref i);
                    break;
                case "--max-iterations":
                    options.MaxIterationsPerPhase = int.Parse(RequireValue(args, ref i));
                    break;
                case "--time-step":
                    options.TimeStep = double.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--endless":
                    options.Endless = true;
                    break;
                case "--csv-output":
                    options.CsvOutputPath = RequireValue(args, ref i);
                    break;
                case "--checkpoint-hours":
                    options.CheckpointHours = double.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--max-cycles":
                    options.MaxCycles = long.Parse(RequireValue(args, ref i));
                    break;
                case "--prestige-point-targets":
                    options.PrestigePointTargets = RequireValue(args, ref i)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(int.Parse)
                        .ToList();
                    break;
                case "--max-island-hours":
                    options.MaxIslandHours = double.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--race-gauntlet":
                    options.RaceGauntlet = true;
                    break;
                case "--races":
                    options.Races = RequireValue(args, ref i)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(ParseRace)
                        .ToList();
                    break;
                case "--islands":
                    options.Islands = int.Parse(RequireValue(args, ref i));
                    break;
                case "--gauntlet-output":
                    options.GauntletOutputDirectory = RequireValue(args, ref i);
                    break;
                case "--abandon-island-hours":
                    options.AbandonIslandHours = double.Parse(RequireValue(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--final-island-points":
                    options.FinalIslandPoints = int.Parse(RequireValue(args, ref i));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (options.ShowHelp) return options;

        // The gauntlet's stopping condition is fixed (N prestiges, see RaceGauntletRunner), and it
        // ships its own strategy — neither has to be passed on the command line.
        if (options.RaceGauntlet)
        {
            options.StrategiesPath ??= DefaultGauntletStrategiesPath;
            if (options.Islands < 1)
                throw new ArgumentException("--islands must be at least 1.");
        }
        else if (string.IsNullOrEmpty(options.ObjectivePath))
        {
            throw new ArgumentException("--objective is required.");
        }

        if (string.IsNullOrEmpty(options.StrategiesPath))
            throw new ArgumentException("--strategies is required.");

        return options;
    }

    private static RaceId ParseRace(string name)
        => Enum.TryParse<RaceId>(name, ignoreCase: true, out var race)
            ? race
            : throw new ArgumentException($"Unknown race '{name}'. Known races: {string.Join(", ", Enum.GetNames<RaceId>())}.");

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for argument: {args[i]}");
        return args[++i];
    }
}
