using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

public abstract class IslandShapeGenerator
{
    public abstract IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer);

    /// <summary>
    /// Returns a preferred land hex near the island edge where the player city should be placed.
    /// Returns null to use any valid edge vertex.
    /// </summary>
    public virtual HexCoord? GetPreferredStartHex(IReadOnlyList<HexCoord> coords) => null;
}
