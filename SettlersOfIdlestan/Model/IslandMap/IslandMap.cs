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
    /// ni de l'eau ni du Vide, ou (vertex entouré uniquement d'eau et/ou de Vide, ex. balise
    /// maritime ou bord de carte proche de l'Abysse) si ses 3 hexagones sont tous visibles.
    /// </summary>
    /// <remarks>
    /// Boucles indexées et non <c>Any</c>/<c>All</c> : le premier lambda capturait <c>this</c> et le
    /// second était une conversion de groupe de méthodes — deux délégués alloués à <b>chaque</b>
    /// appel. Cette méthode est appelée en boucles imbriquées (découverte des civilisations, IA des
    /// PNJ, visibilité des cibles d'attaque) sur toutes les villes de la carte à chaque événement
    /// d'horloge : le profilage d'allocations la donnait comme premier poste du budget d'image.
    /// </remarks>
    public bool IsVertexVisible(Vertex vertex)
    {
        if (!IsOnSameLayer(vertex)) return false;

        var hexes = vertex.GetHexes();
        bool allPresent = true;
        for (int i = 0; i < hexes.Length; i++)
        {
            var tile = GetTile(hexes[i]);
            if (tile == null) { allPresent = false; continue; }
            if (!tile.TerrainType.IsWater() && !tile.TerrainType.IsVoid()) return true;
        }
        return allPresent;
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
