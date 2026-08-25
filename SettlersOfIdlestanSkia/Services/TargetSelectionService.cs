using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>Forme des cibles proposées : ville (Vertex), hexagone, ou les deux à la fois (ex: Raid sur ville ou monstre).</summary>
public enum TargetSelectionShape
{
    Vertex,
    Hex,
    Mixed,
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

    /// <summary>Libellé optionnel affiché sur chaque hexagone ciblable (ex: niveau de corruption).</summary>
    public IReadOnlyDictionary<HexCoord, string>? HexLabels { get; private set; }

    /// <summary>Clé de localisation d'un bouton optionnel affiché à côté d'Annuler (ex: "Détruire la ville"
    /// pendant le ciblage de destination de la relocalisation). Null si aucune action secondaire.</summary>
    public string? SecondaryActionLabelKey { get; private set; }

    private Action<Vertex>? _onVertexConfirmed;
    private Action<HexCoord>? _onHexConfirmed;
    private Action? _secondaryAction;

    /// <summary>Déclenché à l'entrée en mode ciblage (pour mettre en pause/masquer l'overlay).</summary>
    public event EventHandler? Entered;
    /// <summary>Déclenché après l'exécution du callback de confirmation (pour reprendre/réafficher l'overlay).</summary>
    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    public void EnterVertexSelection(string titleKey, IReadOnlyList<Vertex> targets, Action<Vertex> onConfirmed,
        TargetSelectionTheme theme = TargetSelectionTheme.Hostile,
        string? secondaryActionLabelKey = null, Action? secondaryAction = null)
    {
        if (targets.Count == 0) return;

        bool wasActive = IsActive;
        Shape = TargetSelectionShape.Vertex;
        Theme = theme;
        TitleKey = titleKey;
        VertexTargets = targets;
        HexTargets = Array.Empty<HexCoord>();
        _onVertexConfirmed = onConfirmed;
        _onHexConfirmed = null;
        SecondaryActionLabelKey = secondaryActionLabelKey;
        _secondaryAction = secondaryAction;
        HoveredVertex = null;
        HoveredHex = null;
        IsActive = true;
        if (!wasActive) Entered?.Invoke(this, EventArgs.Empty);
    }

    public void EnterHexSelection(string titleKey, IReadOnlyList<HexCoord> targets, Action<HexCoord> onConfirmed,
        TargetSelectionTheme theme = TargetSelectionTheme.Friendly, IReadOnlyDictionary<HexCoord, string>? hexLabels = null)
    {
        if (targets.Count == 0) return;

        bool wasActive = IsActive;
        Shape = TargetSelectionShape.Hex;
        Theme = theme;
        TitleKey = titleKey;
        HexTargets = targets;
        VertexTargets = Array.Empty<Vertex>();
        HexLabels = hexLabels;
        _onHexConfirmed = onConfirmed;
        _onVertexConfirmed = null;
        SecondaryActionLabelKey = null;
        _secondaryAction = null;
        HoveredVertex = null;
        HoveredHex = null;
        IsActive = true;
        if (!wasActive) Entered?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Entre en mode ciblage mixte : propose simultanément des villes (Vertex) et des hexagones (ex: MonsterFeature),
    /// chacun avec son propre callback de confirmation. Utilisé par le Raid, qui peut cibler une ville ennemie ou un monstre.
    /// </summary>
    public void EnterMixedSelection(string titleKey,
        IReadOnlyList<Vertex> vertexTargets, Action<Vertex> onVertexConfirmed,
        IReadOnlyList<HexCoord> hexTargets, Action<HexCoord> onHexConfirmed,
        TargetSelectionTheme theme = TargetSelectionTheme.Hostile)
    {
        if (vertexTargets.Count == 0 && hexTargets.Count == 0) return;

        bool wasActive = IsActive;
        Shape = TargetSelectionShape.Mixed;
        Theme = theme;
        TitleKey = titleKey;
        VertexTargets = vertexTargets;
        HexTargets = hexTargets;
        _onVertexConfirmed = onVertexConfirmed;
        _onHexConfirmed = onHexConfirmed;
        SecondaryActionLabelKey = null;
        _secondaryAction = null;
        HoveredVertex = null;
        HoveredHex = null;
        IsActive = true;
        if (!wasActive) Entered?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Confirme la cible et exécute le callback. Le callback peut lui-même ré-entrer en mode ciblage
    /// (ex: relocalisation — sélection de la ville puis de la destination) : dans ce cas IsActive reste
    /// vrai après l'appel et on ne déclenche pas Confirmed, pour ne pas signaler la fin d'une session
    /// qui en réalité continue (voir OnTargetSelectionEntered/Confirmed dans GameScreen).
    /// </summary>
    public void ConfirmVertex(Vertex target)
    {
        if (!IsActive || Shape == TargetSelectionShape.Hex) return;
        var callback = _onVertexConfirmed;
        _onVertexConfirmed = null;
        _onHexConfirmed = null;
        callback?.Invoke(target);
        if (_onVertexConfirmed == null && _onHexConfirmed == null)
        {
            Reset();
            Confirmed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ConfirmHex(HexCoord target)
    {
        if (!IsActive || Shape == TargetSelectionShape.Vertex) return;
        var callback = _onHexConfirmed;
        _onVertexConfirmed = null;
        _onHexConfirmed = null;
        callback?.Invoke(target);
        if (_onVertexConfirmed == null && _onHexConfirmed == null)
        {
            Reset();
            Confirmed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Déclenche le bouton d'action secondaire (ex: "Détruire la ville") au lieu de choisir
    /// une cible. Contrairement à Annuler, l'action est exécutée avant de refermer le mode ciblage,
    /// et c'est Confirmed qui se déclenche — le ciblage se résout, il n'est pas annulé.</summary>
    public void InvokeSecondaryAction()
    {
        if (!IsActive || _secondaryAction == null) return;
        var action = _secondaryAction;
        Reset();
        action.Invoke();
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
        HexLabels = null;
        _onVertexConfirmed = null;
        _onHexConfirmed = null;
        SecondaryActionLabelKey = null;
        _secondaryAction = null;
    }
}
