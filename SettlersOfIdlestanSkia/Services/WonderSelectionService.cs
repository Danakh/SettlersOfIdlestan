using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>Type de feature en cours de placement sur la carte.</summary>
public enum HexPlacementKind
{
    Wonder,
    DeepestMine,
}

/// <summary>
/// Manages the hex-selection mode state for wonder and deepest-mine placement.
/// </summary>
public sealed class WonderSelectionService
{
    private WonderController? _wonderController;
    private DeepestMineController? _deepestMineController;

    public bool IsActive { get; private set; }
    public HexPlacementKind Kind { get; private set; } = HexPlacementKind.Wonder;
    public IReadOnlyList<HexCoord> PlaceableHexes { get; private set; } = Array.Empty<HexCoord>();
    public HexCoord? HoveredHex { get; set; }

    public event EventHandler? Entered;
    public event EventHandler<HexCoord>? WonderPlacementConfirmed;
    public event EventHandler? Cancelled;

    public void ConnectWonderController(WonderController wonderController)
        => _wonderController = wonderController;

    public void ConnectDeepestMineController(DeepestMineController deepestMineController)
        => _deepestMineController = deepestMineController;

    public void Enter()
    {
        IReadOnlyList<HexCoord> hexes = _wonderController != null
            ? _wonderController.GetPlaceableHexes()
            : Array.Empty<HexCoord>();
        Enter(HexPlacementKind.Wonder, hexes);
    }

    public void EnterDeepestMine()
    {
        IReadOnlyList<HexCoord> hexes = _deepestMineController != null
            ? _deepestMineController.GetPlaceableHexes()
            : Array.Empty<HexCoord>();
        Enter(HexPlacementKind.DeepestMine, hexes);
    }

    private void Enter(HexPlacementKind kind, IReadOnlyList<HexCoord> placeableHexes)
    {
        IsActive = true;
        Kind = kind;
        PlaceableHexes = placeableHexes;
        HoveredHex = null;
        Entered?.Invoke(this, EventArgs.Empty);
    }

    public void Confirm(HexCoord hex)
    {
        if (!IsActive) return;
        IsActive = false;
        HoveredHex = null;
        WonderPlacementConfirmed?.Invoke(this, hex);
    }

    public void Cancel()
    {
        if (!IsActive) return;
        IsActive = false;
        HoveredHex = null;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
}
