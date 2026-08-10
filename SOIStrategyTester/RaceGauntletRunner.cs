using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
using SOIStrategyTester.Model;

namespace SOIStrategyTester;

public class RaceGauntletOptions
{
    /// <summary>Races to run, in order. Defaults to every implemented race (see RaceDefinitions).</summary>
    public List<RaceId> Races { get; set; } = new();

    /// <summary>How many islands each race must clear — one prestige per island.</summary>
    public int Islands { get; set; } = 4;

    /// <summary>PRNG seed. The same seed for every race is what makes the results comparable; leave
    /// null for a random one (useful to check a failure isn't specific to one generated map).</summary>
    public int? Seed { get; set; }

    /// <summary>Directory for the per-race CSVs and the summary files.</summary>
    public string OutputDirectory { get; set; } = "race-gauntlet";

    public List<int> PrestigePointTargets { get; set; } = new() { 35, 80, 500, 1000 };

    /// <summary>Simulated-hours cap on every island (see EndlessRunOptions.TimeCapAllIslands).</summary>
    public double MaxIslandHours { get; set; } = 8.0;

    /// <summary>Simulated hours on a single island after which the race is declared stuck.</summary>
    public double AbandonIslandAfterHours { get; set; } = 24.0;

    public double CheckpointHours { get; set; } = 1.0;
}

/// <summary>
/// Plays the first N islands (4 by default) once per race and reports which races make it through —
/// the "can everyone actually play the game?" check that FullIslandTest only ever answers for Humans.
///
/// <para>Each race starts from <see cref="GameStateFactory.NewGameForRace"/>: the divine powers its
/// tier requires are unlocked and island 1 is generated for that race, so a race is measured on the
/// state a player reaches it in, not on a Human start with the racial modifiers bolted on.</para>
///
/// <para>Islands are driven by <see cref="EndlessRunner"/>, exactly like <c>--endless</c>: it decides
/// when to prestige (points target, or the per-island time cap) rather than the strategy. The verdict
/// is deliberately coarse — <b>did this race reach the next prestige N times</b> — because that is the
/// only goal every race shares. Per-island checkpoints of the FullIslandTest kind ("12 cities",
/// "Library everywhere") are race-hostile by construction: Giants cannot reach 12 cities at minimum
/// distance 4, Mermaids cannot level inland cities past the Town Hall cap, and failing them would say
/// nothing about whether the race can be played. What each island actually achieved (cities, points,
/// research, Wonder) is reported alongside the verdict instead, which is where a race being merely
/// weak — rather than blocked — shows up.</para>
/// </summary>
public static class RaceGauntletRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private const string SummaryCsvHeader =
        "Race,Tier,Verdict,IslandsCleared,IslandsRequested,SimulatedHours,Iterations,FailureReason," +
        "Island,WorldId,IslandHours,Cities,Buildings,TotalBuildingLevels,PrestigePoints,Research,UniqueBuildings,WonderLevel";

    public static bool Run(StrategyDefinition strategy, StrategyRunOptions runOptions, RaceGauntletOptions options)
    {
        var races = options.Races.Count > 0
            ? options.Races
            : RaceDefinitions.All.Where(r => r.IsImplemented).Select(r => r.Id).ToList();

        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        Console.WriteLine($"Race gauntlet — {races.Count} races x {options.Islands} islands, strategy '{strategy.Name}', " +
                          $"seed {(options.Seed?.ToString(CultureInfo.InvariantCulture) ?? "random")}.");
        Console.WriteLine($"Output: {outputDirectory}");

        var results = new List<RaceGauntletResult>();
        foreach (var race in races)
        {
            Console.WriteLine();
            Console.WriteLine($"######## {race} ({RaceDefinitions.Get(race).Tier}) ########");
            results.Add(RunRace(race, strategy, runOptions, options, outputDirectory));
        }

        WriteSummary(results, options, outputDirectory);
        PrintSummary(results, options);

        return results.All(r => r.Passed);
    }

    private static RaceGauntletResult RunRace(RaceId race, StrategyDefinition strategy, StrategyRunOptions runOptions,
        RaceGauntletOptions options, string outputDirectory)
    {
        var result = new RaceGauntletResult
        {
            Race = race,
            Tier = RaceDefinitions.Get(race).Tier,
            IslandsRequested = options.Islands,
        };

        try
        {
            var controller = GameStateFactory.NewGameForRace(race, options.Seed);
            var endlessOptions = new EndlessRunOptions
            {
                CsvPath = Path.Combine(outputDirectory, $"race-{race}.csv"),
                CheckpointIntervalHours = options.CheckpointHours,
                MaxCycles = options.Islands,
                PrestigePointTargets = options.PrestigePointTargets,
                MaxIslandHoursAfterFixedTargets = options.MaxIslandHours,
                TimeCapAllIslands = true,
                AbandonIslandAfterHours = options.AbandonIslandAfterHours,
            };
            var objective = new ObjectiveSpec { Kind = ObjectiveKind.PrestigeRunCountAtLeast, Count = options.Islands };

            var run = EndlessRunner.Run(controller, strategy, objective, runOptions, endlessOptions);

            result.IslandsCleared = run.PrestigeCount;
            result.Ticks = run.Tick;
            result.Iterations = run.Iterations;
            result.FailureReason = run.FailureReason;
            result.Islands.AddRange(run.IslandStats);

            // The state the race ended on, in the game's own save format: loadable in the Desktop head
            // to look at the map that blocked it, which is the only practical way to tell "this island
            // has nowhere left this race can build" from "the autoplayer didn't get there".
            var savePath = Path.Combine(outputDirectory, $"race-{race}-final.json");
            File.WriteAllText(savePath, controller.ExportMainState());
            result.FinalSavePath = savePath;
        }
        catch (Exception ex)
        {
            // Island generation itself can fail for a race whose start constraints a given seed can't
            // satisfy — that is a result, not a crash of the gauntlet: report it and run the next race.
            result.FailureReason = $"{ex.GetType().Name}: {ex.Message}";
        }

        Console.WriteLine(result.Passed
            ? $"-> {race}: PASS — {result.IslandsCleared}/{result.IslandsRequested} islands in {FormatHours(result.Ticks)}h simulated."
            : $"-> {race}: FAIL — cleared {result.IslandsCleared}/{result.IslandsRequested} islands. {result.FailureReason}");

        return result;
    }

    private static void WriteSummary(List<RaceGauntletResult> results, RaceGauntletOptions options, string outputDirectory)
    {
        var csvPath = Path.Combine(outputDirectory, "summary.csv");
        using (var csv = new StreamWriter(csvPath, append: false))
        {
            csv.WriteLine(SummaryCsvHeader);
            foreach (var r in results)
            {
                // One row per island, plus a bare row for a race that cleared none — so a failure is
                // never a silently missing line in the CSV.
                if (r.Islands.Count == 0)
                {
                    csv.WriteLine(string.Join(',', RaceColumns(r).Concat(new object?[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })));
                    continue;
                }

                for (int i = 0; i < r.Islands.Count; i++)
                {
                    var s = r.Islands[i];
                    csv.WriteLine(string.Join(',', RaceColumns(r).Concat(new object?[]
                    {
                        i + 1, s.WorldId, FormatHours(s.TickDuration), s.CityCount, s.BuildingCount,
                        s.TotalBuildingLevels, s.PrestigePoints, s.ResearchCompleted, s.UniqueBuildings, s.WonderLevel,
                    })));
                }
            }
        }

        var jsonPath = Path.Combine(outputDirectory, "summary.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            options.Seed,
            options.Islands,
            options.PrestigePointTargets,
            options.MaxIslandHours,
            options.AbandonIslandAfterHours,
            Results = results,
        }, JsonOptions));

        Console.WriteLine();
        Console.WriteLine($"Summary written to {csvPath} and {jsonPath}");
    }

    private static IEnumerable<object?> RaceColumns(RaceGauntletResult r) => new object?[]
    {
        r.Race, r.Tier, r.Passed ? "PASS" : "FAIL", r.IslandsCleared, r.IslandsRequested,
        FormatHours(r.Ticks), r.Iterations, Escape(r.FailureReason),
    };

    private static void PrintSummary(List<RaceGauntletResult> results, RaceGauntletOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("======== Race gauntlet summary ========");
        Console.WriteLine($"{"Race",-9} {"Tier",-8} {"Verdict",-7} {"Islands",-8} {"Sim h",-8}  Per island (points / cities / hours)");

        foreach (var r in results)
        {
            var perIsland = r.Islands.Count == 0
                ? "-"
                : string.Join("  ", r.Islands.Select(s => $"{s.PrestigePoints}p/{s.CityCount}c/{FormatHours(s.TickDuration)}h"));

            Console.WriteLine($"{r.Race,-9} {r.Tier,-8} {(r.Passed ? "PASS" : "FAIL"),-7} " +
                              $"{$"{r.IslandsCleared}/{r.IslandsRequested}",-8} {FormatHours(r.Ticks),-8}  {perIsland}");

            if (!r.Passed && !string.IsNullOrEmpty(r.FailureReason))
                Console.WriteLine($"{"",-9} {"",-8} {"",-7} {"",-8} {"",-8}  ^ {r.FailureReason}");
        }

        int passed = results.Count(r => r.Passed);
        Console.WriteLine();
        Console.WriteLine(passed == results.Count
            ? $"All {results.Count} races cleared the first {options.Islands} islands."
            : $"{passed}/{results.Count} races cleared the first {options.Islands} islands — " +
              $"blocked: {string.Join(", ", results.Where(r => !r.Passed).Select(r => r.Race))}.");
    }

    /// <summary>Ticks (1 tick = 0.01 simulated second) formatted as simulated hours, 2 decimals.</summary>
    private static string FormatHours(long ticks)
        => (ticks / 100.0 / 3600.0).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>Failure reasons contain commas — keep the summary CSV one field per column.</summary>
    private static string? Escape(string? value)
        => value == null ? null : "\"" + value.Replace("\"", "\"\"") + "\"";
}

public class RaceGauntletResult
{
    public RaceId Race { get; set; }
    public RaceTier Tier { get; set; }
    public int IslandsRequested { get; set; }
    public int IslandsCleared { get; set; }
    public long Ticks { get; set; }
    public long Iterations { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Save of the state this race ended on, or null if it never got that far.</summary>
    public string? FinalSavePath { get; set; }

    public List<PrestigeRunStats> Islands { get; } = new();

    public bool Passed => IslandsCleared >= IslandsRequested;
}
