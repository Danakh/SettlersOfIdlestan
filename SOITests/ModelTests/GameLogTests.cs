using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using Xunit;

namespace SOITests.ModelTests;

/// <summary>
/// Couvre le journal d'erreurs d'exécution introduit pour remplacer les <c>Debug.WriteLine</c> des
/// gardes des contrôleurs, supprimés à la compilation en Release et donc muets dans un build livré.
///
/// <para><see cref="GameLog"/> est statique et global (canal de diagnostic, pas d'état de jeu), et
/// xUnit exécute les classes de test en parallèle. Chaque test utilise donc un nom de source qui lui
/// est propre et n'affirme que sur ses propres entrées, jamais sur l'état global du journal.</para>
/// </summary>
public class GameLogTests
{
    private static string UniqueSource([System.Runtime.CompilerServices.CallerMemberName] string caller = "")
        => $"GameLogTests.{caller}.{Guid.NewGuid():N}";

    private static GameLog.ErrorEntry? EntryFor(string source)
        => GameLog.Snapshot().SingleOrDefault(e => e.Source == source);

    [Fact]
    public void Error_RecordsSourceOperationAndException()
    {
        string source = UniqueSource();

        GameLog.Error(source, "MonOperation", new InvalidOperationException("boum"));

        var entry = EntryFor(source);
        Assert.NotNull(entry);
        Assert.Equal("MonOperation", entry!.Operation);
        Assert.Equal(nameof(InvalidOperationException), entry.ExceptionType);
        Assert.Equal("boum", entry.Message);
        Assert.Equal(1, entry.Count);
        Assert.NotNull(entry.StackTrace);
    }

    [Fact]
    public void Error_RepeatedIdenticalFailure_ProducesOneEntryWithCount()
    {
        string source = UniqueSource();

        for (int i = 0; i < 500; i++)
            GameLog.Error(source, "Tick", new InvalidOperationException("meme panne"));

        // C'est le comportement qui rend le journal utilisable : une panne levée à chaque tick doit
        // rester une seule ligne, sinon elle chasse tout le reste du journal en quelques secondes.
        var entry = EntryFor(source);
        Assert.NotNull(entry);
        Assert.Equal(500, entry!.Count);
        Assert.True(entry.LastSeen >= entry.FirstSeen);
    }

    [Fact]
    public void Error_DifferentOperationsOrMessages_ProduceDistinctEntries()
    {
        string source = UniqueSource();

        GameLog.Error(source, "OperationA", new InvalidOperationException("x"));
        GameLog.Error(source, "OperationB", new InvalidOperationException("x"));
        GameLog.Error(source, "OperationA", new InvalidOperationException("y"));
        GameLog.Error(source, "OperationA", new ArgumentException("x"));

        var entries = GameLog.Snapshot().Where(e => e.Source == source).ToList();
        Assert.Equal(4, entries.Count);
        Assert.All(entries, e => Assert.Equal(1, e.Count));
    }

    [Fact]
    public void Error_WithoutException_RecordsInconsistency()
    {
        string source = UniqueSource();

        GameLog.Error(source, "Invariant", "la ville n'a pas d'hôtel de ville");

        var entry = EntryFor(source);
        Assert.NotNull(entry);
        Assert.Equal("Inconsistency", entry!.ExceptionType);
        Assert.Equal("la ville n'a pas d'hôtel de ville", entry.Message);
        Assert.Null(entry.StackTrace);
    }

    [Fact]
    public void OnFirstOccurrence_FiresOnceForARepeatedFailure()
    {
        string source = UniqueSource();
        var previous = GameLog.OnFirstOccurrence;
        int calls = 0;

        try
        {
            // Le sink est un emplacement unique et global : un test parallèle qui initialise un
            // MainGameController le réassigne. On le pose donc juste avant le premier déclenchement,
            // et on n'affirme que sur le fait qu'une répétition ne redéclenche pas.
            GameLog.OnFirstOccurrence = _ => calls++;
            GameLog.Error(source, "Tick", new InvalidOperationException("boum"));
            GameLog.OnFirstOccurrence = _ => calls++;
            for (int i = 0; i < 10; i++)
                GameLog.Error(source, "Tick", new InvalidOperationException("boum"));
        }
        finally
        {
            GameLog.OnFirstOccurrence = previous;
        }

        Assert.Equal(1, calls);
        Assert.Equal(11, EntryFor(source)!.Count);
    }

    [Fact]
    public void OnFirstOccurrence_ThrowingHandler_DoesNotPropagate()
    {
        string source = UniqueSource();
        var previous = GameLog.OnFirstOccurrence;

        try
        {
            GameLog.OnFirstOccurrence = _ => throw new InvalidOperationException("sink cassé");
            // Ne doit pas relever : GameLog est appelé depuis le catch d'un contrôleur, une exception
            // ici ferait tomber le reste du tick — exactement ce que le mécanisme évite.
            GameLog.Error(source, "Tick", new InvalidOperationException("boum"));
        }
        finally
        {
            GameLog.OnFirstOccurrence = previous;
        }

        Assert.NotNull(EntryFor(source));
    }

    [Fact]
    public void GameClock_SubscriberThrowing_IsRecordedInsteadOfSilentlySwallowed()
    {
        // Régression : GameClock.Advance/SimulateAdvance encadraient l'invocation d'un `catch { }`
        // totalement muet. Une exception franchissant les gardes des contrôleurs disparaissait sans
        // laisser la moindre trace.
        var clock = new GameClock();
        var marker = new InvalidOperationException($"GameLogTests {Guid.NewGuid():N}");
        clock.Advanced += (_, _) => throw marker;

        clock.SimulateAdvance(1);

        var entry = GameLog.Snapshot().SingleOrDefault(
            e => e.Source == nameof(GameClock) && e.Message == marker.Message);
        Assert.NotNull(entry);
        Assert.Equal(nameof(GameClock.SimulateAdvance), entry!.Operation);
    }

    [Fact]
    public void MainGameController_RoutesFirstOccurrenceToTheEventLogOfTheCurrentWorld()
    {
        var worldState = SOITests.TestUtilities.IslandTestFactory.CreateSevenHexIslandState();
        var controller = new SettlersOfIdlestan.Controller.MainGameController();
        controller.SetGame(new MainGameState(worldState, new GameClock(), new GamePRNG(42)));

        // On appelle directement le routage du contrôleur plutôt que de passer par GameLog.Error ou
        // par GameLog.OnFirstOccurrence : cet emplacement est global, et un test parallèle qui
        // initialise sa propre partie le réassigne. La ligne ci-dessous vérifie que le câblage existe,
        // l'appel direct vérifie ce qu'il fait — les deux sans dépendre de l'ordonnancement.
        Assert.NotNull(GameLog.OnFirstOccurrence);

        // Marqueur unique plutôt qu'un Single() sur le type d'entrée : ce contrôleur est, le temps du
        // test, le dernier à avoir posé GameLog.OnFirstOccurrence, et reçoit donc aussi les erreurs
        // que les tests parallèles font attraper à leurs propres contrôleurs.
        string marker = $"boum {Guid.NewGuid():N}";
        controller.ReportRuntimeError(new GameLog.ErrorEntry("HarvestController",
            "PerformSeaportGenerations", nameof(InvalidOperationException), marker,
            stackTrace: null, DateTimeOffset.UtcNow));

        var logged = worldState.EventLog.Entries.SingleOrDefault(
            e => e.Type == GameEventType.RuntimeError && e.Message != null && e.Message.Contains(marker));
        Assert.NotNull(logged);
        Assert.True(logged!.Toast);
        Assert.Contains("HarvestController.PerformSeaportGenerations", logged.Message);
    }

    [Fact]
    public void GameClock_SubscriberThrowing_SkipsLaterSubscribers()
    {
        // Documente la sémantique du délégué multicast, qui est la raison d'être des gardes
        // par sous-étape dans chaque contrôleur : l'abonné suivant ne tourne pas du tout.
        var clock = new GameClock();
        bool laterSubscriberRan = false;
        clock.Advanced += (_, _) => throw new InvalidOperationException($"GameLogTests {Guid.NewGuid():N}");
        clock.Advanced += (_, _) => laterSubscriberRan = true;

        clock.SimulateAdvance(1);

        Assert.False(laterSubscriberRan);
    }
}
