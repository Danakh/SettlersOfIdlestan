using System;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Game;

/// <summary>
/// Journal des erreurs d'exécution attrapées par les gardes des contrôleurs (chaque
/// <c>OnClockAdvanced</c> encadre ses sous-étapes d'un try/catch pour qu'une exception dans l'une
/// n'annule pas les autres).
///
/// <para>Ces gardes écrivaient auparavant dans <c>System.Diagnostics.Debug.WriteLine</c>, qui porte
/// <c>[Conditional("DEBUG")]</c> : l'appel est <b>supprimé à la compilation en Release</b>. Dans un
/// build livré, un sous-système qui levait à chaque tick s'arrêtait donc de produire sans laisser la
/// moindre trace — ni log, ni compteur, ni message. C'est la raison d'être de cette classe.</para>
///
/// <para>Les erreurs sont dédupliquées par (source, opération, type d'exception, message) : une
/// exception levée à chaque tick produit une seule entrée dont le <see cref="ErrorEntry.Count"/>
/// augmente, plutôt que des milliers d'entrées identiques. Seule la <b>première</b> occurrence d'une
/// clé déclenche <see cref="OnFirstOccurrence"/>, ce qui rend le signalement au joueur supportable.</para>
///
/// <para>Statique et global à dessein : c'est un canal de diagnostic, pas de l'état de jeu. Il n'est
/// jamais sérialisé, ne participe pas au déterminisme (rien ne le lit pendant la simulation) et n'a
/// donc pas à être câblé à travers les 35 <c>Initialize</c> des contrôleurs.</para>
/// </summary>
public static class GameLog
{
    /// <summary>
    /// Nombre maximal d'erreurs <b>distinctes</b> conservées. Au-delà, la plus ancienne est évincée.
    /// La déduplication faisant l'essentiel du travail, ce plafond n'est atteint que si le jeu
    /// produit 50 pannes différentes — auquel cas les 50 dernières sont largement suffisantes.
    /// </summary>
    public const int MaxEntries = 50;

    /// <summary>Une panne distincte, avec le nombre de fois où elle s'est reproduite.</summary>
    public sealed class ErrorEntry
    {
        /// <summary>Contrôleur ou système à l'origine de l'erreur (ex. <c>HarvestController</c>).</summary>
        public string Source { get; }

        /// <summary>Opération en cours au moment de l'erreur (ex. <c>PerformSeaportGenerations</c>).</summary>
        public string Operation { get; }

        public string ExceptionType { get; }
        public string Message { get; }

        /// <summary>Pile d'appels de la première occurrence — la seule conservée (les suivantes sont identiques).</summary>
        public string? StackTrace { get; }

        public DateTimeOffset FirstSeen { get; }
        public DateTimeOffset LastSeen { get; internal set; }

        /// <summary>Nombre total d'occurrences de cette erreur depuis le démarrage du processus.</summary>
        public int Count { get; internal set; }

        internal ErrorEntry(string source, string operation, string exceptionType, string message,
            string? stackTrace, DateTimeOffset now)
        {
            Source = source;
            Operation = operation;
            ExceptionType = exceptionType;
            Message = message;
            StackTrace = stackTrace;
            FirstSeen = now;
            LastSeen = now;
            Count = 1;
        }

        /// <summary>Résumé sur une ligne, destiné au journal d'événements et aux rapports de bug.</summary>
        public override string ToString()
            => Count > 1
                ? $"{Source}.{Operation}: {ExceptionType}: {Message} (x{Count})"
                : $"{Source}.{Operation}: {ExceptionType}: {Message}";
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<string, ErrorEntry> EntriesByKey = new();
    private static readonly List<ErrorEntry> Ordered = new();
    private static long _totalOccurrences;

    /// <summary>
    /// Appelé pour la <b>première</b> occurrence de chaque erreur distincte, jamais pour les
    /// répétitions. Câblé par <c>MainGameController.InitializeControllersForCurrentIsland</c> vers le
    /// journal d'événements de l'île en cours, pour que la panne soit visible en jeu.
    ///
    /// <para>Emplacement unique (propriété assignable) plutôt qu'un événement : plusieurs
    /// <c>MainGameController</c> peuvent coexister brièvement — voir
    /// <c>NpcCivilizationPlacer.PlaceNpcCivilizations</c>, qui en crée un jetable — et des
    /// abonnements cumulés provoqueraient des signalements en double, sans personne pour se
    /// désabonner. Ici le dernier câblage gagne, ce qui est exactement la sémantique voulue :
    /// l'erreur va au monde actuellement chargé.</para>
    ///
    /// <para>Invoqué hors du verrou interne, et toute exception qu'il lève est ignorée : un
    /// signalement défaillant ne doit surtout pas relever depuis le <c>catch</c> d'un contrôleur,
    /// qui laisserait alors tomber le reste du tick — exactement ce que ce mécanisme existe pour
    /// éviter.</para>
    /// </summary>
    public static Action<ErrorEntry>? OnFirstOccurrence { get; set; }

    /// <summary>Nombre total d'occurrences, répétitions comprises.</summary>
    public static long TotalOccurrences { get { lock (Gate) return _totalOccurrences; } }

    /// <summary>Nombre d'erreurs distinctes actuellement conservées.</summary>
    public static int DistinctCount { get { lock (Gate) return Ordered.Count; } }

    /// <summary>
    /// Enregistre une exception attrapée par une garde de contrôleur. Ne relève jamais : l'appelant
    /// est déjà dans un <c>catch</c> dont le rôle est de laisser tourner le reste du tick.
    /// </summary>
    /// <param name="source">Contrôleur ou système appelant, typiquement <c>nameof(MonControleur)</c>.</param>
    /// <param name="operation">Opération en cours, typiquement <c>nameof(MaMethode)</c>.</param>
    public static void Error(string source, string operation, Exception ex)
    {
        if (ex == null) return;
        Record(source, operation, ex.GetType().Name, ex.Message, ex.ToString());
    }

    /// <summary>
    /// Variante sans exception, pour une incohérence détectée sans qu'un throw ait eu lieu
    /// (invariant violé, état impossible).
    /// </summary>
    public static void Error(string source, string operation, string message)
        => Record(source, operation, "Inconsistency", message, stackTrace: null);

    private static void Record(string source, string operation, string exceptionType, string message,
        string? stackTrace)
    {
        var now = DateTimeOffset.UtcNow;
        string key = $"{source}|{operation}|{exceptionType}|{message}";

        ErrorEntry entry;

        lock (Gate)
        {
            _totalOccurrences++;

            if (EntriesByKey.TryGetValue(key, out var existing))
            {
                existing.Count++;
                existing.LastSeen = now;
                // Rien à signaler : le joueur a déjà été prévenu à la première occurrence, et c'est
                // précisément le cas d'une panne qui se répète à chaque tick.
                return;
            }

            entry = new ErrorEntry(source, operation, exceptionType, message, stackTrace, now);
            EntriesByKey[key] = entry;
            Ordered.Add(entry);

            while (Ordered.Count > MaxEntries)
            {
                var evicted = Ordered[0];
                Ordered.RemoveAt(0);
                EntriesByKey.Remove($"{evicted.Source}|{evicted.Operation}|{evicted.ExceptionType}|{evicted.Message}");
            }
        }

        // Toujours utile quand un débogueur est attaché ; supprimé en Release, d'où tout le reste.
        System.Diagnostics.Debug.WriteLine($"[GameLog] {entry}");

        try { OnFirstOccurrence?.Invoke(entry); }
        catch { /* voir OnFirstOccurrence : un signalement défaillant ne doit pas casser le tick. */ }
    }

    /// <summary>Copie des erreurs conservées, de la plus ancienne à la plus récente.</summary>
    public static IReadOnlyList<ErrorEntry> Snapshot()
    {
        lock (Gate) return Ordered.ToArray();
    }

    /// <summary>
    /// Vide le journal. Réservé aux tests et aux outils hors jeu (SOIBench, SOIStrategyTester) :
    /// en jeu, l'historique doit survivre aux changements d'île pour rester exploitable dans un
    /// rapport de bug.
    /// </summary>
    public static void Reset()
    {
        lock (Gate)
        {
            EntriesByKey.Clear();
            Ordered.Clear();
            _totalOccurrences = 0;
        }
    }
}
