using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Controller;
using SOIStrategyTester.Model;

namespace SOIStrategyTester;

public static class Program
{
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

        var globalObjective = JsonSerializer.Deserialize<ObjectiveSpec>(File.ReadAllText(options.ObjectivePath!), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse objective file: {options.ObjectivePath}");
        var strategies = JsonSerializer.Deserialize<List<StrategyDefinition>>(File.ReadAllText(options.StrategiesPath!), JsonOptions)
            ?? throw new InvalidOperationException($"Could not parse strategies file: {options.StrategiesPath}");

        if (strategies.Count == 0)
            throw new InvalidOperationException("Strategies file contains no strategies.");

        var runOptions = new StrategyRunOptions
        {
            DefaultMaxIterationsPerPhase = options.MaxIterationsPerPhase,
            TimeStep = options.TimeStep,
        };

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
                    options.TimeStep = double.Parse(RequireValue(args, ref i));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        if (options.ShowHelp) return options;

        if (string.IsNullOrEmpty(options.ObjectivePath))
            throw new ArgumentException("--objective is required.");
        if (string.IsNullOrEmpty(options.StrategiesPath))
            throw new ArgumentException("--strategies is required.");

        return options;
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for argument: {args[i]}");
        return args[++i];
    }
}
