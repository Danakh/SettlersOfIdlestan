using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.PrestigeMap;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Generates a fixed prestige map with 3 prestige tiles.
/// </summary>
public class PrestigeGenerator
{
    /// <summary>
    /// Generates the prestige map, always identical with 3 tiles.
    /// </summary>
    /// <returns>The generated prestige map.</returns>
    public PrestigeMap GeneratePrestigeMap()
    {
        var tiles = new List<PrestigeTile>
        {
            new PrestigeTile(new HexCoord(0, 0), PrestigeType.Basic),
            new PrestigeTile(new HexCoord(1, 0), PrestigeType.Advanced),
            new PrestigeTile(new HexCoord(0, 1), PrestigeType.Elite)
        };

        return new PrestigeMap(tiles);
    }
}