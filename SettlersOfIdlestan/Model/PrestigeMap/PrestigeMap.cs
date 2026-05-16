using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.PrestigeMap;

/// <summary>
/// Represents the prestige map, containing a collection of prestige tiles.
/// </summary>
public class PrestigeMap
{
    private readonly Dictionary<HexCoord, PrestigeTile> _tiles = new();

    public PrestigeMap(IEnumerable<PrestigeTile> tiles)
    {
        foreach (var tile in tiles)
        {
            _tiles[tile.Coord] = tile;
        }
    }

    public IReadOnlyDictionary<HexCoord, PrestigeTile> Tiles => _tiles;

    public PrestigeTile? GetTile(HexCoord coord)
    {
        return _tiles.GetValueOrDefault(coord);
    }

    public bool HasTile(HexCoord coord)
    {
        return _tiles.ContainsKey(coord);
    }

    public IEnumerable<PrestigeTile> GetNeighbors(HexCoord coord)
    {
        foreach (var direction in HexDirectionUtils.AllHexDirections)
        {
            var neighborCoord = coord.Neighbor(direction);
            if (GetTile(neighborCoord) is PrestigeTile tile)
            {
                yield return tile;
            }
        }
    }
}