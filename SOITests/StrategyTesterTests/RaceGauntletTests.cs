using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Races;
using SOIStrategyTester;
using SOIStrategyTester.Model;
using SOITests.PerformanceTests;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.StrategyTesterTests;

/// <summary>
/// Le race gauntlet de SOIStrategyTester joué comme test, <b>une race par méthode</b> : chacune part de l'île
/// où l'Ascension la dépose avec la méta-progression de son palier (île 4 pour une race de base,
/// île 5 pour une avancée — voir GameStateFactory.NewGameForRace) et doit enchaîner les prestiges
/// jusqu'à terminer l'<b>île 6</b>. Le verdict est celui du gauntlet : avoir atteint le prestige de
/// chaque île, rien de plus fin — les jalons par étape de FullIslandTest (12 villes, Bibliothèque
/// partout) sont hostiles à certaines races par construction et ne diraient rien de leur jouabilité.
///
/// <para><b>Désactivés par défaut</b> (<see cref="ManualFactAttribute"/> sur
/// <see cref="EnvironmentVariable"/>), et ce n'est pas de la prudence : une manche prend des minutes
/// à des dizaines de minutes par race, son résultat dépend du seed, et un échec se lit et se juge
/// (race bloquée ? faible ? carte ingrate ?) plutôt qu'il ne casse un build. Les jouer toutes :</para>
/// <code>
/// SOI_RACE_GAUNTLET=1 dotnet test SOITests --filter "FullyQualifiedName~RaceGauntletTests"
/// </code>
/// <para>Une méthode par race plutôt qu'une <c>[Theory]</c> paramétrée : c'est ce qui rend une race
/// seule filtrable par son nom, sans avoir à viser un cas de théorie par son libellé d'affichage —
/// la seule façon de rejouer en 9 minutes la race qui a bloqué une manche de 25.</para>
/// <code>
/// SOI_RACE_GAUNTLET=1 dotnet test SOITests --filter "FullyQualifiedName~RaceGauntlet_Dwarf"
/// </code>
/// <para>La liste n'est plus dérivée de RaceDefinitions, donc plus auto-extensible : c'est
/// <see cref="EveryImplementedRace_HasItsOwnGauntletCase"/>, lui joué à chaque manche ordinaire, qui
/// échoue si une race implémentée n'a pas sa méthode (ou l'inverse).</para>
///
/// <para>La sortie détaillée (CSV par race, sauvegarde finale chargeable dans le Desktop) atterrit
/// dans <see cref="OutputRoot"/>, et la console porte le récapitulatif par île. En cas d'échec, la
/// sauvegarde finale est <b>en plus</b> recopiée sous <c>SOITests/saves/<see cref="SavesFolder"/>/</c>
/// — voir <see cref="RunGauntletFor"/>.</para>
///
/// <para>Pas de parallélisation, pour la raison décrite sur <see cref="EndGameFixtureCollection"/> :
/// une manche achète des vertex de prestige à chaque prestige, et
/// <c>PrestigeMap.DefaultMap.VertexPurchased</c> est un événement statique auquel s'abonne le
/// PrestigeModifierProvider de <i>chaque</i> MainGameController vivant — de quoi faire échouer par
/// intermittence les tests qui tournent en même temps.</para>
/// </summary>
[Collection(EndGameFixtureCollection.Name)]
public class RaceGauntletTests
{
    /// <summary>Interrupteur dédié : voir <see cref="ManualTheoryAttribute"/> pour pourquoi ces
    /// manches ne partagent pas celui des tests manuels ordinaires.</summary>
    private const string EnvironmentVariable = "SOI_RACE_GAUNTLET";

    /// <summary>Dernière île à terminer — deux îles pour une race de base (4 → 6), une de plus que
    /// la manche par défaut pour que les races avancées, déposées sur l'île 5, en jouent deux elles
    /// aussi (5 → 6) plutôt qu'une seule.</summary>
    private const int LastIsland = 6;

    /// <summary>Seed fixe : sans elle deux exécutions ne seraient pas comparables, et un échec ne
    /// serait pas rejouable. Le même seed pour toutes les races est aussi ce qui rend le tableau
    /// final comparable d'une race à l'autre.</summary>
    private const int Seed = 1;

    private static string OutputRoot => Path.Combine(Path.GetTempPath(), "soi-race-gauntlet-tests");

    /// <summary>Sous-dossier de <c>SOITests/saves/</c> où atterrit la sauvegarde finale d'une race en
    /// échec, un fichier par race (écrasé à chaque manche). <c>saves/</c> est gitignoré : ces exports
    /// ne salissent pas le dépôt.</summary>
    private const string SavesFolder = "race-gauntlet";

    /// <summary>Préfixe des méthodes de cas, lu par <see cref="EveryImplementedRace_HasItsOwnGauntletCase"/>
    /// pour retrouver la race couverte par chacune : le suffixe est le nom du <see cref="RaceId"/>.</summary>
    private const string CaseMethodPrefix = "RaceGauntlet_";

    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Human() => RunGauntletFor(RaceId.Human);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Elf() => RunGauntletFor(RaceId.Elf);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Dwarf() => RunGauntletFor(RaceId.Dwarf);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Goblin() => RunGauntletFor(RaceId.Goblin);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Orc() => RunGauntletFor(RaceId.Orc);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Mermaid() => RunGauntletFor(RaceId.Mermaid);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_DarkElf() => RunGauntletFor(RaceId.DarkElf);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Giant() => RunGauntletFor(RaceId.Giant);
    [ManualFact(EnvironmentVariable)] public void RaceGauntlet_Garuda() => RunGauntletFor(RaceId.Garuda);

    /// <summary>
    /// Joue la manche d'une race et, si elle échoue, ramène sa sauvegarde finale sous
    /// <c>SOITests/saves/<see cref="SavesFolder"/>/{race}.json</c> avant de lever l'assertion : c'est
    /// l'état exact sur lequel la race a calé, chargeable tel quel dans le head Desktop pour aller
    /// regarder la carte. Le dossier temporaire du runner garde les CSV et le même export, mais il
    /// est balayé au nettoyage de %TEMP% et il n'est pas là où l'on va chercher une sauvegarde.
    /// </summary>
    private static void RunGauntletFor(RaceId race)
    {
        var outputDirectory = Path.Combine(OutputRoot, race.ToString());
        Directory.CreateDirectory(outputDirectory);

        var results = RaceGauntletRunner.RunAll(LoadGauntletStrategy(), new StrategyRunOptions(), new RaceGauntletOptions
        {
            Races = new List<RaceId> { race },
            LastIsland = LastIsland,
            Seed = Seed,
            OutputDirectory = outputDirectory,
        });

        var result = Assert.Single(results);
        if (result.Passed) return;

        // L'export peut manquer : une manche qui meurt sur une exception (génération d'île impossible
        // pour la race sur ce seed) n'atteint jamais l'écriture de la sauvegarde. Ce n'est pas une
        // raison de masquer le vrai échec derrière une erreur de copie — on le dit et on continue.
        string savedTo = result.FinalSavePath != null && File.Exists(result.FinalSavePath)
            ? SaveUtils.CopyIntoSaves(result.FinalSavePath, SavesFolder, race.ToString())
            : "(aucune sauvegarde finale — la manche s'est arrêtée avant de pouvoir l'écrire)";

        Assert.Fail(
            $"{race} n'a pas enchaîné ses îles jusqu'à l'île {LastIsland} (seed {Seed}) : " +
            $"{result.FailureReason ?? "(aucune raison rapportée)"}{Environment.NewLine}" +
            $"Îles terminées : {result.IslandsCleared}/{result.IslandsRequested}.{Environment.NewLine}" +
            $"Sauvegarde finale, chargeable dans le Desktop : {savedTo}{Environment.NewLine}" +
            $"CSV et récapitulatif par île : {outputDirectory}");
    }

    /// <summary>
    /// Le filet de la liste écrite à la main : une race nouvellement implémentée doit avoir sa
    /// méthode, sinon elle ne serait jouée par personne et la manche resterait verte en l'ignorant —
    /// exactement ce que la <c>[Theory]</c> dérivée de RaceDefinitions empêchait avant le découpage.
    /// L'inverse est vérifié aussi : une méthode qui désigne une race retirée ou passée en stub.
    ///
    /// <para>Joué à chaque manche ordinaire (pas de <see cref="ManualFactAttribute"/>) : il ne coûte
    /// qu'une réflexion sur le type, alors que ce qu'il garde ne se voit qu'après 25 minutes.</para>
    /// </summary>
    [Fact]
    public void EveryImplementedRace_HasItsOwnGauntletCase()
    {
        var covered = typeof(RaceGauntletTests)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => n.StartsWith(CaseMethodPrefix, StringComparison.Ordinal))
            .Select(n => n[CaseMethodPrefix.Length..])
            .ToList();

        var implemented = RaceDefinitions.All.Where(r => r.IsImplemented).Select(r => r.Id.ToString()).ToList();

        var missing = implemented.Except(covered, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            $"Races implémentées sans méthode de gauntlet : {string.Join(", ", missing)}. " +
            $"Ajouter un [ManualFact({nameof(EnvironmentVariable)})] public void {CaseMethodPrefix}<Race>() " +
            $"=> {nameof(RunGauntletFor)}(RaceId.<Race>); pour chacune.");

        var stale = covered.Except(implemented, StringComparer.Ordinal).ToList();
        Assert.True(stale.Count == 0,
            $"Méthodes de gauntlet visant une race qui n'est plus implémentée : {string.Join(", ", stale)}.");
    }

    /// <summary>
    /// La stratégie que le mode <c>--race-gauntlet</c> joue par défaut, lue dans le fichier même que
    /// la ligne de commande utilise (copié en sortie par SOIStrategyTester) : la recopier ici ferait
    /// diverger le test de l'outil au premier réglage.
    /// </summary>
    private static StrategyDefinition LoadGauntletStrategy()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "Strategies", "race-gauntlet.json");
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        var strategies = JsonSerializer.Deserialize<List<StrategyDefinition>>(File.ReadAllText(path), options);
        Assert.NotNull(strategies);
        Assert.NotEmpty(strategies!);
        return strategies![0];
    }
}
