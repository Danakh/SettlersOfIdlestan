using System;
using System.Collections.Generic;
using System.Linq;
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
    public const int SurfaceLayer = 0;

    private readonly Dictionary<HexCoord, HexTile> _tiles = new();

    public IslandMap(IEnumerable<HexTile> tiles, int z = SurfaceLayer)
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

    /// <summary>
    /// Vrai si ce vertex est visible sur cette carte : au moins un de ses hexagones visibles n'est
    /// pas de l'eau, ou (vertex entouré uniquement d'eau, ex. balise maritime) si ses 3 hexagones
    /// sont tous visibles.
    /// </summary>
    public bool IsVertexVisible(Vertex vertex)
    {
        if (!IsOnSameLayer(vertex)) return false;
        var hexes = vertex.GetHexes();
        if (hexes.Any(h => GetTile(h) is { } tile && !tile.TerrainType.IsWater()))
            return true;
        return hexes.All(HasTile);
    }

    /// <summary>
    /// Ajoute ou remplace une tuile sur cette carte (utilisé par l'AutoExtend).
    /// </summary>
    public void AddTile(HexTile tile)
    {
        if (tile.Coord.Z != Z)
            throw new ArgumentException($"Tile layer {tile.Coord.Z} does not match map layer {Z}.", nameof(tile));
        _tiles[tile.Coord] = tile;
    }

    private void EnsureCoordOnMap(HexCoord coord)
    {
        if (!IsOnSameLayer(coord))
            throw new ArgumentException($"Hex layer {coord.Z} does not match map layer {Z}.", nameof(coord));
    }
}
