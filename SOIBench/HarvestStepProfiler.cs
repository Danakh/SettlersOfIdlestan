using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SettlersOfIdlestan.Controller.Island;

namespace SOIBench;

/// <summary>
/// Découpe le coût de <c>HarvestController</c> en ses neuf étapes de production (récolte
/// automatique, ports, or des marchés, fonderies, génération passive, forgerons, alchimistes).
///
/// <para>Même principe que <see cref="ClockProfiler"/> : le tableau <c>_steps</c> est remplacé par
/// le même tableau, dans le même ordre, chaque <c>Action</c> enveloppée dans un chronomètre. Sans
/// ça la répartition s'arrête au contrôleur, qui pèse à lui seul près de la moitié d'un saut de
/// temps — savoir <b>quelle</b> étape le fait est le premier pas utile.</para>
/// </summary>
public sealed class HarvestStepProfiler : IDisposable
{
    private readonly HarvestController _controller;
    private readonly FieldInfo? _field;
    private readonly Array? _original;
    private readonly Dictionary<string, ControllerCost> _costs = new();

    public bool IsAttached => _field != null && _original != null;

    public IReadOnlyList<ControllerCost> Costs => _costs.Values.OrderByDescending(c => c.ElapsedTicks).ToList();

    public HarvestStepProfiler(HarvestController controller)
    {
        _controller = controller;
        _field = typeof(HarvestController).GetField("_steps", BindingFlags.Instance | BindingFlags.NonPublic);
        _original = _field?.GetValue(controller) as Array;
        if (_field == null || _original == null) return;

        var stepType = _original.GetType().GetElementType()!;
        var nameProperty = stepType.GetProperty("Name")!;
        var tickProperty = stepType.GetProperty("Tick")!;
        var constructor = stepType.GetConstructor(new[] { typeof(string), typeof(Action<long>) })!;

        var wrapped = Array.CreateInstance(stepType, _original.Length);
        for (int i = 0; i < _original.Length; i++)
        {
            object step = _original.GetValue(i)!;
            string name = (string)nameProperty.GetValue(step)!;
            var inner = (Action<long>)tickProperty.GetValue(step)!;
            var cost = GetOrAdd(name);

            Action<long> timed = tick =>
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = Stopwatch.GetTimestamp();
                try { inner(tick); }
                finally
                {
                    cost.ElapsedTicks += Stopwatch.GetTimestamp() - start;
                    cost.AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                    cost.Calls++;
                }
            };

            wrapped.SetValue(constructor.Invoke(new object[] { name, timed }), i);
        }

        _field.SetValue(controller, wrapped);
    }

    public void Dispose()
    {
        if (_field != null && _original != null)
            _field.SetValue(_controller, _original);
    }

    private ControllerCost GetOrAdd(string name)
    {
        if (_costs.TryGetValue(name, out var existing)) return existing;
        var cost = new ControllerCost { Name = name };
        _costs[name] = cost;
        return cost;
    }
}
