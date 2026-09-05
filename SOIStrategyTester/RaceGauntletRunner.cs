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

    /// <summary>
    /// Numéro de la <b>dernière</b> île que chaque race doit terminer — une île par prestige, en
    /// partant de celle où l'Ascension dépose la race (île 3 avec le premier jalon, voir
    /// AscensionController.AscensionSkippedIslandCount et GameStateFactory.NewGameForRace). Le
    /// nombre de cycles s'en déduit : 3 → 5 fait trois îles.
    ///
    /// <para>Exprimé en numéro d'île et non plus en nombre d'îles parce que c'est le numéro qui porte
    /// le sens : la calibration des cibles de points (<see cref="PrestigePointTargets"/>) est par
    /// île, et l'île 5 est la première que l'atlas marque <c>IsEndgameIsland</c> — la borne dont
    /// dépend le verdict de la manche de fin de partie.</para>
    /// </summary>
    public int LastIsland { get; set; } = 5;

    /// <summary>PRNG seed. The same seed for every race is what makes the results comparable; leave
    /// null for a random one (useful to check a failure isn't specific to one generated map).</summary>
    public int? Seed { get; set; }

    /// <summary>Directory for the per-race CSVs and the summary files.</summary>
    public string OutputDirectory { get; set; } = "race-gauntlet";

    /// <summary>Cibles de points de prestige <b>par numéro d'île</b> (position 1 = île 1), passées
    /// telles quelles à EndlessRunOptions.PrestigePointTargets : une manche qui démarre à l'île 3
    /// prend donc la 3ᵉ entrée pour son premier cycle — voir EndlessRunOptions.FirstIslandNumber.</summary>
    public List<int> PrestigePointTargets { get; set; } = new() { 35, 80, 500, 1000 };

    /// <summary>Simulated-hours cap on every island (see EndlessRunOptions.TimeCapAllIslands).</summary>
    public double MaxIslandHours { get; set; } = 8.0;

    /// <summary>Simulated hours on a single island after which the race is declared stuck.</summary>
    public double AbandonIslandAfterHours { get; set; } = 24.0;

    public double CheckpointHours { get; set; } = 1.0;

    /// <summary>
    /// Points de prestige que l'île <see cref="LastIsland"/> doit rapporter pour que la race passe, en
    /// plus d'avoir enchaîné toutes les îles jusque-là. 0 (défaut) : verdict sur le seul nombre d'îles.
    ///
    /// <para>C'est le critère de la manche « fin de partie » : la question intéressante n'est plus
    /// « la race arrive-t-elle à prestiger » — à ce stade toutes y arrivent — mais « produit-elle
    /// encore des points à un rythme d'end game ». D'où une manche qui s'arrête à l'île 5, la
    /// première que l'atlas marque <c>IsEndgameIsland</c>, en lui demandant 100 points.</para>
    ///
    /// <para>Câblé sur <see cref="EndlessRunOptions.LastCyclePointsFloor"/>, sans quoi la mesure
    /// serait truquée : l'île prestigerait à sa propre cible calculée, plus basse, et le critère
    /// mesurerait cette cible plutôt que la race. Le plafond de temps par île (<see
    /// cref="MaxIslandHours"/>) reste appliqué : un échec ici se lit « pas 100 points dans le budget
    /// de temps d'une île », pas « incapable de prestiger ».</para>
    /// </summary>
    public int MinFinalIslandPrestigePoints { get; set; }
}

/// <summary>
/// Plays one race at a time from the island its Ascension starts on up to <see
/// cref="RaceGauntletOptions.LastIsland"/> (island 3 → 5 by default) and reports which races make it
/// through — the "can everyone actually play the game?" check that FullIslandTest only ever answers
/// for Humans.
///
/// <para>Each race starts from <see cref="GameStateFactory.NewGameForRace"/>: the divine powers its
/// tier requires are unlocked and the Ascension's own island is generated for that race, so a race is
/// measured on the state a player reaches it in, not on a Human start with the racial modifiers
/// bolted on. That island is island 3 as soon as the first Ascension milestone skips the two opening
/// islands (AscensionController.AscensionSkippedIslandCount) — the run is therefore three islands
/// long by default, not five, and every per-island target is read from the island's own number.</para>
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
        "Race,Tier,Verdict,IslandsCleared,IslandsRequested,FirstIsland,LastIsland,SimulatedHours,Iterations,FailureReason," +
        "Island,WorldId,IslandHours,Cities,Buildings,TotalBuildingLevels,PrestigePoints,Research,UniqueBuildings,WonderLevel";

    public static bool Run(StrategyDefinition strategy, StrategyRunOptions runOptions, RaceGauntletOptions options)
    {
        var races = options.Races.Count > 0
            ? options.Races
            : RaceDefinitions.All.Where(r => r.IsImplemented).Select(r => r.Id).ToList();

        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        Console.WriteLine($"Race gauntlet — {races.Count} races, from the Ascension island through island {options.LastIsland}, " +
                          $"strategy '{strategy.Name}', seed {(options.Seed?.ToString(CultureInfo.InvariantCulture) ?? "random")}.");
        Console.WriteLine(options.MinFinalIslandPrestigePoints > 0
            ? $"Pass: clear every island up to island {options.LastIsland} AND earn {options.MinFinalIslandPrestigePoints}+ prestige points on it."
            : $"Pass: clear every island up to island {options.LastIsland}.");
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
            LastIsland = options.LastIsland,
            // Provisoire, remplacé dès que l'île de départ est connue : majorant, pour qu'une race qui
            // meurt avant même d'avoir une partie se lise FAIL et non PASS sur 0 île demandée.
            IslandsRequested = Math.Max(1, options.LastIsland),
            MinFinalIslandPrestigePoints = options.MinFinalIslandPrestigePoints,
        };

        try
        {
            var controller = GameStateFactory.NewGameForRace(race, options.Seed);

            // L'Ascension décide de l'île de départ (jalons — voir GameStateFactory.NewGameForRace) :
            // la manche joue de là jusqu'à LastIsland, soit une île de moins par jalon acquis.
            int firstIsland = controller.CurrentMainState!.CurrentWorldState!.WorldId;
            int islandCount = Math.Max(1, options.LastIsland - firstIsland + 1);
            result.FirstIsland = firstIsland;
            result.IslandsRequested = islandCount;
            Console.WriteLine($"-- islands {firstIsland}..{options.LastIsland} ({islandCount} prestige{(islandCount > 1 ? "s" : "")})");

            var endlessOptions = new EndlessRunOptions
            {
                CsvPath = Path.Combine(outputDirectory, $"race-{race}.csv"),
                CheckpointIntervalHours = options.CheckpointHours,
                MaxCycles = islandCount,
                PrestigePointTargets = options.PrestigePointTargets,
                FirstIslandNumber = firstIsland,
                MaxIslandHoursAfterFixedTargets = options.MaxIslandHours,
                TimeCapAllIslands = true,
                AbandonIslandAfterHours = options.AbandonIslandAfterHours,
                LastCyclePointsFloor = options.MinFinalIslandPrestigePoints,
            };
            var objective = new ObjectiveSpec { Kind = ObjectiveKind.PrestigeRunCountAtLeast, Count = islandCount };

            var run = EndlessRunner.Run(controller, strategy, objective, runOptions, endlessOptions);

            result.IslandsCleared = run.PrestigeCount;
            result.Ticks = run.Tick;
            result.Iterations = run.Iterations;
            result.FailureReason = run.FailureReason;
            result.Islands.AddRange(run.IslandStats);

            // Échec sur les points de la dernière île : le run lui-même s'est bien terminé (toutes les
            // îles enchaînées), il n'a donc aucune raison d'échec à donner — c'est le verdict du
            // gauntlet, pas celui d'EndlessRunner, qui est négatif ici.
            if (string.IsNullOrEmpty(result.FailureReason) && result.IslandsCleared >= result.IslandsRequested
                && !result.FinalIslandPointsReached)
                result.FailureReason =
                    $"island {options.LastIsland}: {result.FinalIslandPoints}/{options.MinFinalIslandPrestigePoints} " +
                    $"prestige points after {FormatHours(result.Islands[^1].TickDuration)}h — {run.LastPrestigeReason}";

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

        string finalPoints = options.MinFinalIslandPrestigePoints > 0 && result.ReachedFinalIsland
            ? $", {result.FinalIslandPoints}/{options.MinFinalIslandPrestigePoints} pts on island {options.LastIsland}"
            : "";
        Console.WriteLine(result.Passed
            ? $"-> {race}: PASS — {result.IslandsCleared}/{result.IslandsRequested} islands{finalPoints} in {FormatHours(result.Ticks)}h simulated."
            : $"-> {race}: FAIL — cleared {result.IslandsCleared}/{result.IslandsRequested} islands{finalPoints}. {result.FailureReason}");

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
                    // Colonne Island : le numéro d'île réel (la manche ne démarre plus à 1), qui vaut
                    // WorldId — gardé à côté, c'est lui que porte PrestigeRunStats.
                    csv.WriteLine(string.Join(',', RaceColumns(r).Concat(new object?[]
                    {
                        r.FirstIsland + i, s.WorldId, FormatHours(s.TickDuration), s.CityCount, s.BuildingCount,
                        s.TotalBuildingLevels, s.PrestigePoints, s.ResearchCompleted, s.UniqueBuildings, s.WonderLevel,
                    })));
                }
            }
        }

        var jsonPath = Path.Combine(outputDirectory, "summary.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new
        {
            options.Seed,
            options.LastIsland,
            options.PrestigePointTargets,
            options.MaxIslandHours,
            options.AbandonIslandAfterHours,
            options.MinFinalIslandPrestigePoints,
            Results = results,
        }, JsonOptions));

        Console.WriteLine();
        Console.WriteLine($"Summary written to {csvPath} and {jsonPath}");
    }

    private static IEnumerable<object?> RaceColumns(RaceGauntletResult r) => new object?[]
    {
        r.Race, r.Tier, r.Passed ? "PASS" : "FAIL", r.IslandsCleared, r.IslandsRequested,
        r.FirstIsland, r.LastIsland, FormatHours(r.Ticks), r.Iterations, Escape(r.FailureReason),
    };

    private static void PrintSummary(List<RaceGauntletResult> results, RaceGauntletOptions options)
    {
        Console.WriteLine();
        Console.WriteLine("======== Race gauntlet summary ========");
        Console.WriteLine($"{"Race",-9} {"Tier",-8} {"Verdict",-7} {"Islands",-8} {"Sim h",-8}  Per island (island: points / cities / hours)");

        foreach (var r in results)
        {
            // Le numéro d'île est préfixé : la manche ne commence plus à l'île 1, une colonne nue se
            // lirait comme un rang de cycle.
            var perIsland = r.Islands.Count == 0
                ? "-"
                : string.Join("  ", r.Islands.Select((s, i) =>
                    $"i{r.FirstIsland + i}: {s.PrestigePoints}p/{s.CityCount}c/{FormatHours(s.TickDuration)}h"));

            Console.WriteLine($"{r.Race,-9} {r.Tier,-8} {(r.Passed ? "PASS" : "FAIL"),-7} " +
                              $"{$"{r.IslandsCleared}/{r.IslandsRequested}",-8} {FormatHours(r.Ticks),-8}  {perIsland}");

            if (!r.Passed && !string.IsNullOrEmpty(r.FailureReason))
                Console.WriteLine($"{"",-9} {"",-8} {"",-7} {"",-8} {"",-8}  ^ {r.FailureReason}");
        }

        if (options.MinFinalIslandPrestigePoints > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Island {options.LastIsland} — {options.MinFinalIslandPrestigePoints}+ prestige points required:");
            foreach (var r in results)
                Console.WriteLine(r.ReachedFinalIsland
                    ? $"{"",-9} {r.Race,-9} {r.FinalIslandPoints,5} pts  {(r.FinalIslandPointsReached ? "ok" : "short")}"
                    : $"{"",-9} {r.Race,-9} {"—",5}      never got there");
        }

        int passed = results.Count(r => r.Passed);
        Console.WriteLine();
        Console.WriteLine(passed == results.Count
            ? $"All {results.Count} races passed."
            // Deux échecs distincts une fois le critère de points actif, et les confondre sous
            // « blocked » serait faux : ne pas atteindre l'île N est un blocage, la finir sous le
            // seuil de points est un problème de rythme.
            : $"{passed}/{results.Count} races passed — " +
              $"blocked: {Names(results.Where(r => !r.ReachedFinalIsland)) ?? "none"}; " +
              $"points short: {Names(results.Where(r => r.ReachedFinalIsland && !r.Passed)) ?? "none"}.");
    }

    /// <summary>Liste de races pour la ligne de conclusion, ou null si la sélection est vide.</summary>
    private static string? Names(IEnumerable<RaceGauntletResult> results)
    {
        var names = string.Join(", ", results.Select(r => r.Race));
        return names.Length == 0 ? null : names;
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

    /// <summary>Île sur laquelle l'Ascension a déposé la race — début de la manche (voir
    /// RaceGauntletOptions.LastIsland). 0 tant que la partie n'a pas pu être créée.</summary>
    public int FirstIsland { get; set; }

    /// <summary>Dernière île à terminer (copie de RaceGauntletOptions.LastIsland).</summary>
    public int LastIsland { get; set; }

    /// <summary>Nombre d'îles à enchaîner : <see cref="LastIsland"/> - <see cref="FirstIsland"/> + 1.</summary>
    public int IslandsRequested { get; set; }
    public int IslandsCleared { get; set; }
    public long Ticks { get; set; }
    public long Iterations { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>Save of the state this race ended on, or null if it never got that far.</summary>
    public string? FinalSavePath { get; set; }

    public List<PrestigeRunStats> Islands { get; } = new();

    /// <summary>Copie de <see cref="RaceGauntletOptions.MinFinalIslandPrestigePoints"/> — 0 si le
    /// verdict ne porte que sur le nombre d'îles.</summary>
    public int MinFinalIslandPrestigePoints { get; set; }

    /// <summary>
    /// Points rapportés par l'île <see cref="LastIsland"/> — celle sur laquelle porte le critère —,
    /// 0 si la race n'est jamais allée jusque-là. Volontairement pas « la dernière île terminée » :
    /// pour une race qui cale sur l'île 5, ce serait les points de l'île 4, affichés dans la colonne
    /// de l'île 5.
    /// </summary>
    public int FinalIslandPoints =>
        Islands.Count >= IslandsRequested && IslandsRequested > 0 ? Islands[IslandsRequested - 1].PrestigePoints : 0;

    /// <summary>Faux pour une race qui n'a pas atteint l'île sur laquelle porte le critère : ses
    /// points ne sont pas « insuffisants », ils n'existent pas.</summary>
    public bool ReachedFinalIsland => Islands.Count >= IslandsRequested;

    public bool FinalIslandPointsReached =>
        MinFinalIslandPrestigePoints <= 0 || FinalIslandPoints >= MinFinalIslandPrestigePoints;

    public bool Passed => IslandsCleared >= IslandsRequested && FinalIslandPointsReached;
}
