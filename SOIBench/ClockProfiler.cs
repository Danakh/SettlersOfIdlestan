using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SettlersOfIdlestan.Model.Game;

namespace SOIBench;

/// <summary>Coût mesuré d'un abonné à <see cref="GameClock.Advanced"/>.</summary>
public sealed class ControllerCost
{
    public required string Name { get; init; }
    public long Calls { get; set; }
    public long ElapsedTicks { get; set; }
    public long AllocatedBytes { get; set; }

    public double TotalMs => ElapsedTicks * 1000.0 / Stopwatch.Frequency;
}

/// <summary>
/// Attribue le temps d'un événement <see cref="GameClock.Advanced"/> à chacun de ses abonnés.
///
/// <para>Une quinzaine de contrôleurs s'abonnent à l'horloge (voir
/// <c>MainGameController.InitializeControllersForCurrentIsland</c>), et l'ordre d'abonnement est
/// significatif — le combat doit être résolu avant le déplacement des monstres, par exemple. Le
/// profileur remplace la liste d'invocation par la même liste, dans le même ordre, chaque délégué
/// étant enveloppé dans un chronomètre : l'ordre et le comportement sont préservés, seul le temps
/// est observé.</para>
///
/// <para>L'accès passe par réflexion sur le champ de sauvegarde de l'événement (un événement
/// « field-like » en C# a un champ privé du même nom). C'est le prix à payer pour ne pas ajouter de
/// crochet de profilage dans le code du jeu. Si le champ est introuvable (renommage,
/// réimplémentation de l'événement avec add/remove explicites), <see cref="IsAttached"/> vaut false
/// et seul le temps total reste mesurable.</para>
/// </summary>
public sealed class ClockProfiler : IDisposable
{
    private readonly GameClock _clock;
    private readonly FieldInfo? _field;
    private readonly EventHandler<GameClockAdvancedEventArgs>? _original;
    private readonly Dictionary<string, ControllerCost> _costs = new();

    public bool IsAttached => _field != null && _original != null;

    public IReadOnlyCollection<ControllerCost> Costs => _costs.Values;

    public ClockProfiler(GameClock clock)
    {
        _clock = clock;
        _field = typeof(GameClock).GetField(nameof(GameClock.Advanced), BindingFlags.Instance | BindingFlags.NonPublic);
        _original = _field?.GetValue(clock) as EventHandler<GameClockAdvancedEventArgs>;
        if (_field == null || _original == null) return;

        EventHandler<GameClockAdvancedEventArgs>? wrapped = null;
        foreach (var handler in _original.GetInvocationList().Cast<EventHandler<GameClockAdvancedEventArgs>>())
        {
            var inner = handler;
            var cost = GetOrAdd(DescribeTarget(inner));
            wrapped += (sender, args) =>
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                inner(sender, args);
                cost.ElapsedTicks += Stopwatch.GetTimestamp() - start;
                cost.AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                cost.Calls++;
            };
        }

        _field.SetValue(clock, wrapped);
    }

    /// <summary>Remet à zéro les compteurs sans détacher le profileur (fin du préchauffage).</summary>
    public void Reset()
    {
        foreach (var cost in _costs.Values)
        {
            cost.Calls = 0;
            cost.ElapsedTicks = 0;
            cost.AllocatedBytes = 0;
        }
    }

    /// <summary>Restaure la liste d'invocation d'origine — le jeu reprend son coût normal.</summary>
    public void Dispose()
    {
        if (_field != null && _original != null)
            _field.SetValue(_clock, _original);
    }

    private ControllerCost GetOrAdd(string name)
    {
        if (_costs.TryGetValue(name, out var existing)) return existing;
        var cost = new ControllerCost { Name = name };
        _costs[name] = cost;
        return cost;
    }

    /// <summary>
    /// Nom lisible de l'abonné : le type de l'instance sur laquelle la méthode est appelée. Pour un
    /// gestionnaire statique (aucune cible), on retombe sur le type déclarant, et pour les fermetures
    /// générées par le compilateur (<c>&lt;&gt;c__DisplayClass…</c>) sur le type englobant.
    /// </summary>
    private static string DescribeTarget(EventHandler<GameClockAdvancedEventArgs> handler)
    {
        var type = handler.Target?.GetType() ?? handler.Method.DeclaringType;
        while (type != null && type.Name.StartsWith('<'))
            type = type.DeclaringType;
        return type?.Name ?? "?";
    }
}
