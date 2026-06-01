using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the island map, containing a collection of hex tiles.
/// </summary>
[Serializable]
[JsonConverter(typeof(IslandMapJsonConverter))]
public class IslandMap
{
    private readonly Dictionary<HexCoord, HexTile> _tiles = new();

    public IslandMap(IEnumerable<HexTile> tiles, int z = HexCoord.SurfaceZ)
    {
        foreach (var tile in tiles)
        {
            if (_tiles.Count == 0)
                Z = tile.Coord.Z;
            else if (tile.Coord.Z != Z)
                throw new ArgumentException("All tiles in an IslandMap must be on the same map layer.", nameof(tiles));

            _tiles[tile.Coord] = tile;
        }

        if (_tiles.Count == 0)
            Z = z;
    }

    public int Z { get; private set; }

    public IReadOnlyDictionary<HexCoord, HexTile> Tiles => _tiles;

    public HexTile? GetTile(HexCoord coord)
    {
        EnsureCoordOnMap(coord);
        return _tiles.GetValueOrDefault(coord);
    }

    public bool HasTile(HexCoord coord)
    {
        EnsureCoordOnMap(coord);
        return _tiles.ContainsKey(coord);
    }

    public IEnumerable<HexTile> GetNeighbors(HexCoord coord)
    {
        EnsureCoordOnMap(coord);
        foreach (var direction in HexDirectionUtils.AllHexDirections)
        {
            var neighborCoord = coord.Neighbor(direction);
            if (GetTile(neighborCoord) is HexTile tile)
            {
                yield return tile;
            }
        }
    }

    public bool VertexHasTerrainType(Vertex vertex, TerrainType terrainType)
    {
        if (vertex.Z != Z)
            throw new ArgumentException($"Vertex layer {vertex.Z} does not match map layer {Z}.", nameof(vertex));

        var hexes = vertex.GetHexes();
        foreach (var hex in hexes)
        {
            if (GetTile(hex) is HexTile tile && tile.TerrainType == terrainType)
            {
                return true;
            }
        }
        return false;
    }

    public bool IsOnSameLayer(HexCoord coord) => coord.Z == Z;
    public bool IsOnSameLayer(Vertex vertex) => vertex.Z == Z;
    public bool IsOnSameLayer(Edge edge) => edge.Z == Z;

    private void EnsureCoordOnMap(HexCoord coord)
    {
        if (!IsOnSameLayer(coord))
            throw new ArgumentException($"Hex layer {coord.Z} does not match map layer {Z}.", nameof(coord));
    }
}
