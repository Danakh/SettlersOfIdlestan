using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// Le race gauntlet de SOIStrategyTester joué comme test, une race par cas : chacune part de l'île
/// où l'Ascension la dépose avec la méta-progression de son palier (île 4 pour une race de base,
/// île 5 pour une avancée — voir GameStateFactory.NewGameForRace) et doit enchaîner les prestiges
/// jusqu'à terminer l'<b>île 6</b>. Le verdict est celui du gauntlet : avoir atteint le prestige de
/// chaque île, rien de plus fin — les jalons par étape de FullIslandTest (12 villes, Bibliothèque
/// partout) sont hostiles à certaines races par construction et ne diraient rien de leur jouabilité.
///
/// <para><b>Désactivés par défaut</b> (<see cref="ManualTheoryAttribute"/> sur
/// <see cref="EnvironmentVariable"/>), et ce n'est pas de la prudence : une manche prend des minutes
/// à des dizaines de minutes par race, son résultat dépend du seed, et un échec se lit et se juge
/// (race bloquée ? faible ? carte ingrate ?) plutôt qu'il ne casse un build. Les jouer :</para>
/// <code>
/// SOI_RACE_GAUNTLET=1 dotnet test SOITests --filter "FullyQualifiedName~RaceGauntletTests"
/// </code>
/// <para>La sortie détaillée (CSV par race, sauvegarde finale chargeable dans le Desktop) atterrit
/// dans <see cref="OutputRoot"/>, et la console porte le récapitulatif par île.</para>
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

    /// <summary>Un cas par race implémentée — dérivé de RaceDefinitions plutôt qu'énuméré, pour
    /// qu'une race nouvellement implémentée entre dans la manche sans qu'on y pense.</summary>
    public static IEnumerable<object[]> ImplementedRaces =>
        RaceDefinitions.All.Where(r => r.IsImplemented).Select(r => new object[] { r.Id });

    [ManualTheory(EnvironmentVariable)]
    [MemberData(nameof(ImplementedRaces))]
    public void RaceGauntlet_ClearsEveryIslandUpToSix(RaceId race)
    {
        var outputDirectory = Path.Combine(OutputRoot, race.ToString());
        Directory.CreateDirectory(outputDirectory);

        bool passed = RaceGauntletRunner.Run(LoadGauntletStrategy(), new StrategyRunOptions(), new RaceGauntletOptions
        {
            Races = new List<RaceId> { race },
            LastIsland = LastIsland,
            Seed = Seed,
            OutputDirectory = outputDirectory,
        });

        Assert.True(passed,
            $"{race} n'a pas enchaîné ses îles jusqu'à l'île {LastIsland} (seed {Seed}). " +
            $"La raison est sur la ligne de verdict de la sortie console ; les CSV et la sauvegarde " +
            $"de l'état final sont dans {outputDirectory}.");
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
