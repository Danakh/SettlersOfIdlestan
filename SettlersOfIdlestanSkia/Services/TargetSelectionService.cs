using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>Forme des cibles proposées : ville (Vertex) ou hexagone.</summary>
public enum TargetSelectionShape
{
    Vertex,
    Hex,
}

/// <summary>Thème visuel de la sélection — hostile (rouge) pour une cible ennemie, amical (vert) sinon.</summary>
public enum TargetSelectionTheme
{
    Hostile,
    Friendly,
}

/// <summary>
/// Service générique de ciblage sur la carte : assombrit l'île, propose une liste de cibles
/// (villes ou hexagones) avec survol/confirmation/annulation, et exécute un callback fourni
/// par l'appelant à la confirmation. Remplace les anciens RaidSelectionService / WonderSelectionService,
/// qui dupliquaient cette logique pour chaque action (Raid, Merveille, Mine la Plus Profonde, Sorts...).
/// </summary>
public sealed class TargetSelectionService
{
    public bool IsActive { get; private set; }
    public TargetSelectionShape Shape { get; private set; }
    public TargetSelectionTheme Theme { get; private set; }
    public string TitleKey { get; private set; } = string.Empty;
    public IReadOnlyList<Vertex> VertexTargets { get; private set; } = Array.Empty<Vertex>();
    public IReadOnlyList<HexCoord> HexTargets { get; private set; } = Array.Empty<HexCoord>();
    public Vertex? HoveredVertex { get; set; }
    public HexCoord? HoveredHex { get; set; }

    private Action<Vertex>? _onVertexConfirmed;
    private Action<HexCoord>? _onHexConfirmed;

    /// <summary>Déclenché à l'entrée en mode ciblage (pour mettre en pause/masquer l'overlay).</summary>
    public event EventHandler? Entered;
    /// <summary>Déclenché après l'exécution du callback de confirmation (pour reprendre/réafficher l'overlay).</summary>
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public void EnterVertexSelection(string titleKey, IReadOnlyList<Vertex> targets, Action<Vertex> onConfirmed,
        TargetSelectionTheme theme = TargetSelectionTheme.Hostile)
    {
        if (targets.Count == 0) return;

        Shape = TargetSelectionShape.Vertex;
        Theme = theme;
        TitleKey = titleKey;
        VertexTargets = targets;
        HexTargets = Array.Empty<HexCoord>();
        _onVertexConfirmed = onConfirmed;
        _onHexConfirmed = null;
        HoveredVertex = null;
        HoveredHex = null;
        IsActive = true;
        Entered?.Invoke(this, EventArgs.Empty);
    }

    public void EnterHexSelection(string titleKey, IReadOnlyList<HexCoord> targets, Action<HexCoord> onConfirmed,
        TargetSelectionTheme theme = TargetSelectionTheme.Friendly)
    {
        if (targets.Count == 0) return;

        Shape = TargetSelectionShape.Hex;
        Theme = theme;
        TitleKey = titleKey;
        HexTargets = targets;
        VertexTargets = Array.Empty<Vertex>();
        _onHexConfirmed = onConfirmed;
        _onVertexConfirmed = null;
        HoveredVertex = null;
        HoveredHex = null;
        IsActive = true;
        Entered?.Invoke(this, EventArgs.Empty);
    }

    public void ConfirmVertex(Vertex target)
    {
        if (!IsActive || Shape != TargetSelectionShape.Vertex) return;
        var callback = _onVertexConfirmed;
        Reset();
        callback?.Invoke(target);
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    public void ConfirmHex(HexCoord target)
    {
        if (!IsActive || Shape != TargetSelectionShape.Hex) return;
        var callback = _onHexConfirmed;
        Reset();
        callback?.Invoke(target);
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        if (!IsActive) return;
        Reset();
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void Reset()
    {
        IsActive = false;
        HoveredVertex = null;
        HoveredHex = null;
        _onVertexConfirmed = null;
        _onHexConfirmed = null;
    }
}
