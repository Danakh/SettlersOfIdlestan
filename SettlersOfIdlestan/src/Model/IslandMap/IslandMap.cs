using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the island map, containing a collection of hex tiles.
/// </summary>
[Serializable]
public class IslandMap
{
    private readonly Dictionary<HexCoord, HexTile> _tiles = new();

    public IslandMap(IEnumerable<HexTile> tiles)
    {
        foreach (var tile in tiles)
        {
            _tiles[tile.Coord] = tile;
        }
    }

    public IReadOnlyDictionary<HexCoord, HexTile> Tiles => _tiles;

    public HexTile? GetTile(HexCoord coord)
    {
        return _tiles.GetValueOrDefault(coord);
    }

    public bool HasTile(HexCoord coord)
    {
        return _tiles.ContainsKey(coord);
    }

    public IEnumerable<HexTile> GetNeighbors(HexCoord coord)
    {
        foreach (var direction in HexDirectionUtils.AllHexDirections)
        {
            var neighborCoord = coord.Neighbor(direction);
            if (GetTile(neighborCoord) is HexTile tile)
            {
                yield return tile;
            }
        }
    }
}