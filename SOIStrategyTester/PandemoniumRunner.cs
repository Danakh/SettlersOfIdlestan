using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Races;

namespace SOIStrategyTester;

public class PandemoniumRunOptions
{
    public EndGameStateOptions State { get; set; } = new();

    /// <summary>Heures simulées accordées à l'assaut avant de le déclarer perdu.</summary>
    public double MaxHours { get; set; } = 24.0;

    public double CheckpointHours { get; set; } = 0.5;

    /// <summary>Secondes simulées par itération — même sens que <see cref="StrategyRunOptions.TimeStep"/>.</summary>
    public double TimeStep { get; set; } = 0.5;

    public string OutputDirectory { get; set; } = "pandemonium";
}

public class PandemoniumRunResult
{
    public RaceId Race { get; set; }
    public bool DemonGodDefeated { get; set; }
    public int TentaclesKilled { get; set; }
    public int TentaclesTotal { get; set; }
    public long Ticks { get; set; }
    public long Iterations { get; set; }

    /// <summary>PV restants au dieu démon à la fin, et son maximum — la moitié du verdict quand il
    /// survit : « 4 900/5 000 » et « 200/5 000 » ne se corrigent pas du tout de la même façon.</summary>
    public int DemonGodHp { get; set; }
    public int DemonGodMaxHp { get; set; }

    public int PandemoniumCities { get; set; }
    public int Soldiers { get; set; }
    public int CitiesLostInPandemonium { get; set; }
    public string? FailureReason { get; set; }
    public string? FinalSavePath { get; set; }

    /// <summary>Vrai pour la manche ascensionnée — celle dont on attend une victoire.</summary>
    public bool Ascended { get; set; }

    public bool Passed => DemonGodDefeated;

    /// <summary>
    /// L'issue est-elle celle qu'on attendait ? La manche de base est un <b>échec attendu</b> : sans
    /// Ascension le siège n'a aucun pouvoir divin, et l'arène n'offre aucun emplacement hors de portée
    /// des monstres. C'est cette valeur, et non <see cref="Passed"/>, que le code de sortie reflète —
    /// ce qu'on veut détecter, c'est le jour où l'un des deux résultats change.
    /// </summary>
    public bool MatchedExpectation => Passed == Ascended;
}

/// <summary>
/// La manche « fin de partie » : une civilisation arrivée au bout du jeu peut-elle abattre le dieu
/// démon du Pandémonium ?
///
/// <para>Elle se lit exactement comme le race gauntlet — un état de départ posé, une stratégie unique,
/// un verdict binaire, un CSV — mais elle répond à l'autre bout de la question d'équilibrage. Le
/// gauntlet demande « toutes les races savent-elles jouer les premières îles » ; celle-ci demande « le
/// dernier contenu du jeu est-il battable, et à quel prix ». L'état de départ est fabriqué et non joué
/// (voir <see cref="EndGameStateFactory"/>) : aucun autoplay ne sait aujourd'hui amener une partie
/// jusqu'au Pandémonium, et ce n'est pas ce qu'on mesure ici.</para>
///
/// <para><b>Deux manches, deux verdicts attendus.</b> Sans Ascension (<c>--pandemonium</c>), la
/// civilisation n'a aucun pouvoir divin et l'échec est <i>attendu</i> : le blocage est géométrique —
/// aucun emplacement de l'arène n'est hors de portée des monstres. Avec 20 Ascensions
/// (<c>--pandemonium-ascended</c>), Poing de Dieu frappe à distance à travers l'armure et la victoire
/// est attendue. <see cref="PandemoniumRunResult.MatchedExpectation"/> — et donc le code de sortie —
/// répond « la manche s'est-elle comportée comme prévu ? », pas « a-t-on gagné ? » : ce qui mérite une
/// alerte, c'est le jour où l'un des deux résultats change.</para>
///
/// <para><b>Ce qu'un échec veut dire</b>, et le résumé les distingue :</para>
/// <list type="bullet">
///   <item><b>Tentacules non nettoyées</b> — l'arène n'a jamais été prise : le siège n'arrive pas à
///   poser assez de villes au contact, ou les Tentacules rasent les villes d'appui plus vite qu'elles
///   ne montent. Le problème est dans l'approche, pas dans le boss.</item>
///   <item><b>Tentacules nettoyées, dieu démon encore debout à plein</b> — sa régénération
///   (<c>DemonGod.HpRegenAmount</c>) dépasse ce que la garnison inflige à travers son armure : aucune
///   durée supplémentaire n'y changera rien.</item>
///   <item><b>Dieu démon entamé mais pas tombé dans le temps imparti</b> — c'est un problème de
///   rythme, et le plafond d'heures est le paramètre à bouger.</item>
/// </list>
/// </summary>
public static class PandemoniumRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private const string CsvHeader =
        "EventType,Iterations,Tick,SimulatedHours,PandemoniumCities,Roads,Soldiers,Buildings," +
        "TotalBuildingLevels,TentaclesAlive,TentacleHp,DemonGodHp,DemonGodMaxHp,TargetKind,TargetDistance,Gold";

    public static bool Run(PandemoniumRunOptions options)
    {
        var outputDirectory = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(outputDirectory);

        bool ascended = options.State.Ascensions > 0;
        Console.WriteLine($"Pandémonium — manche {(ascended ? "ASCENSIONNÉE" : "de base")}, {options.State.Race}, seed " +
                          $"{options.State.Seed?.ToString(CultureInfo.InvariantCulture) ?? "aléatoire"}, " +
                          $"{options.MaxHours}h simulées au maximum.");
        Console.WriteLine($"Attendu : {(ascended ? "VICTOIRE" : "DÉFAITE")} — {ExpectationReason(ascended)}");
        Console.WriteLine($"Sortie : {outputDirectory}");
        Console.WriteLine();
        Console.WriteLine("Fabrication de l'état de fin de partie...");

        var controller = EndGameStateFactory.Build(options.State, out var report);
        foreach (var line in report.Lines())
            Console.WriteLine("  " + line);
        Console.WriteLine();

        var result = Assault(controller, options, report, outputDirectory);

        var savePath = Path.Combine(outputDirectory, $"pandemonium-{options.State.Race}-final.json");
        File.WriteAllText(savePath, controller.ExportMainState());
        result.FinalSavePath = savePath;

        File.WriteAllText(Path.Combine(outputDirectory, "summary.json"),
            JsonSerializer.Serialize(new { Options = options, State = report, Result = result }, JsonOptions));

        result.Ascended = ascended;
        PrintVerdict(result);

        // Le code de sortie répond « la manche s'est-elle comportée comme prévu ? », pas « a-t-on
        // gagné ? ». La manche de base est un échec attendu : la faire échouer le build à chaque
        // exécution en ferait un bruit permanent, et masquerait le seul événement qui mérite une
        // alerte — le jour où l'un des deux résultats change.
        return result.MatchedExpectation;
    }

    /// <summary>Pourquoi l'issue est attendue — la phrase qui distingue les deux manches.</summary>
    private static string ExpectationReason(bool ascended) => ascended
        ? "l'Ascension donne Poing de Dieu (100 dégâts à distance, à travers l'armure), Bras de Dieu " +
          "et Courroux de Dieu ; le siège n'a plus besoin d'approcher l'arène."
        : "sans Ascension, aucun pouvoir divin : le siège n'a que ses soldats, et aucun emplacement de " +
          "l'arène n'est hors de portée des monstres (voir SOIStrategyTester/CLAUDE.md).";

    private static PandemoniumRunResult Assault(MainGameController controller, PandemoniumRunOptions options,
        EndGameStateReport report, string outputDirectory)
    {
        var clock = controller.Clock ?? throw new InvalidOperationException("Le contrôleur n'a pas d'horloge.");
        var world = controller.CurrentMainState!.CurrentWorldState!;
        var civ = world.PlayerCivilization;

        var result = new PandemoniumRunResult
        {
            Race = options.State.Race,
            TentaclesTotal = report.Tentacles,
            DemonGodMaxHp = report.DemonGodHp,
        };

        long maxTicks = (long)Math.Round(options.MaxHours * 3600.0 * 100.0);
        long checkpointIntervalTicks = (long)Math.Round(options.CheckpointHours * 3600.0 * 100.0);
        if (checkpointIntervalTicks <= 0)
            throw new ArgumentException("--checkpoint-hours doit être positif.");

        long startTick = clock.CurrentTick;
        long nextCheckpointTick = startTick + checkpointIntervalTicks;

        // Compté sur l'événement de destruction, et non sur un pic moins un solde : une arène qui perd
        // et refonde la même ville dix fois affiche le même solde qu'une arène tranquille, et c'est
        // pourtant le symptôme central — le siège n'arrive pas à tenir ce qu'il fonde.
        int citiesLost = 0;
        controller.CityBuilderController.OnCityDestroyed += (_, e) =>
        {
            if (e.CityVertex.Z == LayerState.PandemoniumZ && e.CivilizationIndex == civ.Index)
                citiesLost++;
        };

        using var csv = new StreamWriter(Path.Combine(outputDirectory, "assault.csv"), append: false);
        csv.WriteLine(CsvHeader);

        var auto = StrategyRunner.BuildAutoplayer(controller);
        var siege = new PandemoniumSiege(auto, controller);
        WriteRow(csv, "Start", 0, clock, controller, siege);

        long iterations = 0;
        while (clock.CurrentTick - startTick < maxTicks)
        {
            if (!world.Features.OfType<DemonGod>().Any(d => d.Hp > 0))
            {
                result.DemonGodDefeated = true;
                break;
            }

            try { siege.TryStepOnce(); }
            catch { /* même politique que StrategyRunner : on avale et on retente au tick suivant */ }

            clock.SimulateAdvance((long)(options.TimeStep * 100));
            iterations++;

            while (clock.CurrentTick >= nextCheckpointTick)
            {
                LogCheckpoint(csv, iterations, clock, startTick, controller, siege);
                nextCheckpointTick += checkpointIntervalTicks;
            }
        }

        var demonGod = world.Features.OfType<DemonGod>().FirstOrDefault();
        int tentaclesAlive = world.Features.OfType<Tentacle>()
            .Count(t => t.Position.Z == LayerState.PandemoniumZ && t.Hp > 0);

        result.Ticks = clock.CurrentTick - startTick;
        result.Iterations = iterations;
        result.TentaclesKilled = report.Tentacles - tentaclesAlive;
        result.DemonGodHp = demonGod?.Hp ?? 0;
        result.PandemoniumCities = civ.Cities.Count(c => c.Position.Z == LayerState.PandemoniumZ);
        result.Soldiers = civ.Cities.Where(c => c.Position.Z == LayerState.PandemoniumZ).Sum(c => c.Soldiers);
        result.CitiesLostInPandemonium = citiesLost;

        if (!result.DemonGodDefeated)
            result.FailureReason = tentaclesAlive > 0
                ? $"{tentaclesAlive}/{report.Tentacles} Tentacules encore debout après {FormatHours(result.Ticks)}h — l'arène n'a jamais été prise"
                : demonGod != null && demonGod.Hp >= demonGod.MaxHp
                    ? $"Tentacules nettoyées, mais le dieu démon est resté à {demonGod.Hp}/{demonGod.MaxHp} PV — la régénération absorbe tout"
                    : $"Tentacules nettoyées, dieu démon descendu à {result.DemonGodHp}/{result.DemonGodMaxHp} PV en {FormatHours(result.Ticks)}h — trop lent";

        WriteRow(csv, result.DemonGodDefeated ? "Victory" : "Timeout", iterations, clock, controller, siege);
        csv.Flush();

        return result;
    }

    private static void LogCheckpoint(StreamWriter csv, long iterations, GameClock clock,
        long startTick, MainGameController controller, PandemoniumSiege siege)
    {
        var snapshot = Snapshot.Capture(controller, siege);
        Console.WriteLine(
            $"[{FormatHours(clock.CurrentTick - startTick)}h] {snapshot.Cities} villes, {snapshot.Soldiers} soldats, " +
            $"{snapshot.TentaclesAlive} Tentacules ({snapshot.TentacleHp} PV), " +
            $"dieu démon {snapshot.DemonGodHp}/{snapshot.DemonGodMaxHp} PV — {siege.Describe()}");

        WriteRow(csv, "Checkpoint", iterations, clock, controller, siege);
        csv.Flush();
    }

    private static void WriteRow(StreamWriter csv, string eventType, long iterations,
        GameClock clock, MainGameController controller, PandemoniumSiege siege)
    {
        var snapshot = Snapshot.Capture(controller, siege);
        csv.WriteLine(string.Join(',', new object?[]
        {
            eventType, iterations, clock.CurrentTick, FormatHours(clock.CurrentTick),
            snapshot.Cities, snapshot.Roads, snapshot.Soldiers, snapshot.Buildings, snapshot.TotalBuildingLevels,
            snapshot.TentaclesAlive, snapshot.TentacleHp, snapshot.DemonGodHp, snapshot.DemonGodMaxHp,
            snapshot.TargetKind, snapshot.TargetDistance, snapshot.Gold,
        }));
    }

    /// <summary>
    /// Une ligne de CSV et une ligne de console, prises au même instant. Les deux colonnes qui portent
    /// le diagnostic sont <c>TargetKind</c>/<c>TargetDistance</c> : un siège qui n'avance pas garde la
    /// même cible à la même distance pendant des heures, ce qu'aucun compteur de PV ne montre — les
    /// monstres régénèrent, donc « PV stables » ressemble à « on tape » alors qu'on ne tape pas.
    /// </summary>
    private readonly record struct Snapshot(int Cities, int Roads, int Soldiers, int Buildings, int TotalBuildingLevels,
        int TentaclesAlive, int TentacleHp, int DemonGodHp, int DemonGodMaxHp, string TargetKind, int TargetDistance, int Gold)
    {
        public static Snapshot Capture(MainGameController controller, PandemoniumSiege siege)
        {
            var world = controller.CurrentMainState!.CurrentWorldState!;
            var civ = world.PlayerCivilization;
            var cities = civ.Cities.Where(c => c.Position.Z == LayerState.PandemoniumZ).ToList();
            var buildings = cities.SelectMany(c => c.Buildings).ToList();
            var tentacles = world.Features.OfType<Tentacle>()
                .Where(t => t.Position.Z == LayerState.PandemoniumZ && t.Hp > 0).ToList();
            var demonGod = world.Features.OfType<DemonGod>().FirstOrDefault();
            var target = siege.CurrentTarget;

            return new Snapshot(
                cities.Count,
                civ.Roads.Count(r => r.Position.Z == LayerState.PandemoniumZ),
                cities.Sum(c => c.Soldiers),
                buildings.Count,
                buildings.Sum(b => b.Level),
                tentacles.Count,
                tentacles.Sum(t => t.Hp),
                demonGod?.Hp ?? 0,
                demonGod?.MaxHp ?? 0,
                target?.GetType().Name ?? "none",
                target == null ? -1 : NearestCityDistance(cities, target),
                civ.GetResourceQuantity(Resource.Gold));
        }

        private static int NearestCityDistance(List<City> cities, MonsterFeature target)
        {
            int best = int.MaxValue;
            foreach (var city in cities)
                best = Math.Min(best, city.Position.GetHexes().Max(h => h.DistanceTo(target.Position)));
            return best == int.MaxValue ? -1 : best;
        }
    }

    private static void PrintVerdict(PandemoniumRunResult result)
    {
        Console.WriteLine();
        Console.WriteLine($"======== Verdict Pandémonium ({(result.Ascended ? "ascensionnée" : "de base")}) ========");
        Console.WriteLine(result.Passed
            ? $"{result.Race} : VICTOIRE — dieu démon abattu en {FormatHours(result.Ticks)}h simulées " +
              $"({result.Iterations} itérations), {result.TentaclesKilled}/{result.TentaclesTotal} Tentacules."
            : $"{result.Race} : DÉFAITE — {result.FailureReason}");
        Console.WriteLine(result.MatchedExpectation
            ? $"  conforme à l'attendu ({(result.Ascended ? "victoire" : "défaite")})."
            : $"  ⚠ INATTENDU : cette manche était censée se solder par une {(result.Ascended ? "victoire" : "défaite")}.");
        Console.WriteLine($"  arène : {result.PandemoniumCities} villes ({result.CitiesLostInPandemonium} perdues), " +
                          $"{result.Soldiers} soldats en garnison.");
        Console.WriteLine($"  sauvegarde finale : {result.FinalSavePath}");
    }

    /// <summary>Ticks (1 tick = 0.01 s simulée) en heures simulées, 2 décimales.</summary>
    private static string FormatHours(long ticks)
        => (ticks / 100.0 / 3600.0).ToString("F2", CultureInfo.InvariantCulture);
}
