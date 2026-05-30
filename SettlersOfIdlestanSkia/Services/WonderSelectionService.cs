using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Manages the wonder hex-selection mode state.
/// </summary>
public sealed class WonderSelectionService
{
    private WonderController? _wonderController;

    public bool IsActive { get; private set; }
    public IReadOnlyList<HexCoord> PlaceableHexes { get; private set; } = Array.Empty<HexCoord>();
    public HexCoord? HoveredHex { get; set; }

    public event EventHandler? Entered;
    public event EventHandler<HexCoord>? WonderPlacementConfirmed;
    public event EventHandler? Cancelled;

    public void ConnectWonderController(WonderController wonderController)
        => _wonderController = wonderController;

    public void Enter()
    {
        IReadOnlyList<HexCoord> hexes = _wonderController != null
            ? _wonderController.GetPlaceableHexes()
            : Array.Empty<HexCoord>();
        Enter(hexes);
    }

    private void Enter(IReadOnlyList<HexCoord> placeableHexes)
    {
        IsActive = true;
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
