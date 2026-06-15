using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Gère l'état du mode sélection de ville pour l'action Raid.
/// </summary>
public sealed class RaidSelectionService
{
    public bool IsActive { get; private set; }
    public IReadOnlyList<Vertex> SelectableTargets { get; private set; } = Array.Empty<Vertex>();
    public Vertex? HoveredTarget { get; set; }

    public event EventHandler? Entered;
    public event EventHandler<Vertex>? RaidTargetConfirmed;
    public event EventHandler? Cancelled;

    public void Enter(IReadOnlyList<Vertex> targets)
    {
        IsActive = true;
        SelectableTargets = targets;
        HoveredTarget = null;
        Entered?.Invoke(this, EventArgs.Empty);
    }

    public void Confirm(Vertex target)
    {
        if (!IsActive) return;
        IsActive = false;
        HoveredTarget = null;
        RaidTargetConfirmed?.Invoke(this, target);
    }

    public void Cancel()
    {
        if (!IsActive) return;
        IsActive = false;
        HoveredTarget = null;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
