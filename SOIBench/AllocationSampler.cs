using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

namespace SOIBench;

/// <summary>Volume alloué échantillonné pour un type.</summary>
public sealed record AllocationSample(string TypeName, long Bytes, long Hits);

/// <summary>
/// Échantillonne les allocations par type.
///
/// <para>S'abonne à <c>GCAllocationTick</c> du runtime .NET, levé une fois tous les ~100 Ko alloués
/// avec le type de l'objet qui a franchi le seuil. C'est un échantillonnage pondéré par le volume :
/// sur les millions d'octets qu'un événement d'horloge alloue, la répartition par type obtenue est
/// fiable, même si aucun octet individuel n'est compté. Le rapport par contrôleur de
/// <see cref="ClockProfiler"/> dit <i>où</i> ça alloue ; celui-ci dit <i>quoi</i>, ce qui suffit
/// presque toujours à retrouver la ligne (un <c>HashSet&lt;Vertex&gt;</c> ou une classe de fermeture
/// ne laissent pas beaucoup de candidats).</para>
///
/// <para><b>Pas d'attribution par contrôleur ici, volontairement.</b> Les événements GC ne viennent
/// pas d'un <c>EventSource</c> managé mais du runtime natif, et sont acheminés vers les
/// <see cref="EventListener"/> par EventPipe, de façon <i>asynchrone</i> : au moment où le callback
/// s'exécute, le contrôleur en cours n'est plus celui qui a fait l'allocation. Une première version
/// croisait les deux et produisait une répartition crédible mais fausse — en pratique un simple
/// tirage pondéré par le temps passé dans chaque contrôleur, ce qui « expliquait » par exemple que
/// HarvestController allouait des Vertex. Pour savoir <i>où</i>, se fier aux octets par contrôleur de
/// <see cref="ClockProfiler"/>, qui sont mesurés de façon synchrone sur le même thread.</para>
/// </summary>
public sealed class AllocationSampler : EventListener
{
    private const string RuntimeEventSourceName = "Microsoft-Windows-DotNETRuntime";
    private const EventKeywords GcKeyword = (EventKeywords)0x1;

    private readonly Dictionary<string, (long Bytes, long Hits)> _samples = new();
    private EventSource? _runtimeSource;
    private bool _recording;

    /// <summary>Vrai si l'écouteur a bien trouvé la source d'événements du runtime.</summary>
    public bool IsAttached => _runtimeSource != null;

    public void Start()
    {
        _samples.Clear();
        _recording = true;
    }

    public void Stop() => _recording = false;

    public IReadOnlyList<AllocationSample> Samples => _samples
        .Select(kv => new AllocationSample(kv.Key, kv.Value.Bytes, kv.Value.Hits))
        .OrderByDescending(s => s.Bytes)
        .ToList();

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name != RuntimeEventSourceName) return;
        _runtimeSource = eventSource;
        EnableEvents(eventSource, EventLevel.Verbose, GcKeyword);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!_recording) return;
        if (eventData.EventName is not ("GCAllocationTick_V4" or "GCAllocationTick_V3" or "GCAllocationTick_V2")) return;

        string? typeName = null;
        long amount = 0;

        var names = eventData.PayloadNames;
        if (names == null) return;
        for (int i = 0; i < names.Count; i++)
        {
            var value = eventData.Payload?[i];
            if (names[i] == "TypeName")
                typeName = value as string;
            // AllocationAmount64 est le champ fiable ; AllocationAmount (32 bits) sert de repli.
            else if (names[i] == "AllocationAmount64" && value is ulong wide && wide > 0)
                amount = (long)wide;
            else if (names[i] == "AllocationAmount" && amount == 0 && value is uint narrow)
                amount = narrow;
        }

        if (typeName == null) return;

        var key = Simplify(typeName);
        _samples.TryGetValue(key, out var current);
        _samples[key] = (current.Bytes + amount, current.Hits + 1);
    }

    /// <summary>
    /// Raccourcit les noms de types génériques, illisibles bruts
    /// (<c>System.Collections.Generic.HashSet`1[[SettlersOfIdlestan.Model.HexGrid.Vertex, …]]</c>).
    /// </summary>
    private static string Simplify(string typeName)
    {
        var simplified = typeName;
        int assemblyQualifier = simplified.IndexOf(", Settlers", StringComparison.Ordinal);
        if (assemblyQualifier >= 0) simplified = simplified[..assemblyQualifier] + "]]";

        simplified = simplified.Replace("System.Collections.Generic.", "")
                               .Replace("SettlersOfIdlestan.Model.", "")
                               .Replace("SettlersOfIdlestan.Controller.", "")
                               .Replace("System.", "");

        return simplified.Length > 70 ? simplified[..70] + "…" : simplified;
    }
}
