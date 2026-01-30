using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller;

/// <summary>
/// Generates a random island map based on a list of land tiles.
/// The land tiles are placed in a connected hexagonal layout with each having at least two neighbors,
/// then surrounded by water tiles.
/// </summary>
public class IslandGenerator
{
    /// <summary>
    /// Generates an island map from the provided land tile data.
    /// The tiles are shuffled and assigned to coordinates in a spiral order to ensure connectivity.
    /// Water tiles are added around the land tiles.
    /// </summary>
    /// <param name="tileData">The list of land tile data (resource and tile count).</param>
    /// <returns>The generated island map.</returns>
    public IslandMap GenerateIsland(IEnumerable<(TerrainType terrainType, int tileCount)> tileData)
    {
        var tileList = new List<TerrainType>();
        foreach (var (terrainType, tileCount) in tileData)
        {
            for (int i = 0; i < tileCount; i++)
            {
                tileList.Add(terrainType);
            }
        }
        if (tileList.Count == 0)
        {
            return new IslandMap([]);
        }

        // Shuffle the land tiles for randomness
        var shuffledTiles = Shuffle(tileList);

        // Generate coordinates in spiral order
        var coords = GenerateSpiralCoords(shuffledTiles.Count).ToList();

        // Create new land tiles with assigned coordinates
        var tiles = new List<HexTile>();
        for (int i = 0; i < shuffledTiles.Count; i++)
        {
            var terrainType = shuffledTiles[i];

            var newTile = new HexTile(coords[i], terrainType, null);
            tiles.Add(newTile);
        }

        // Find water coordinates: neighbors of land that are not land
        var coordset = new HashSet<HexCoord>(coords);
        var waterCoords = new HashSet<HexCoord>();
        foreach (var landCoord in coords)
        {
            foreach (var direction in HexDirectionUtils.AllHexDirections)
            {
                var neighbor = landCoord.Neighbor(direction);
                if (!coordset.Contains(neighbor))
                {
                    waterCoords.Add(neighbor);
                }
            }
        }

        // Add water tiles
        foreach (var waterCoord in waterCoords)
        {
            tiles.Add(new HexTile(waterCoord, TerrainType.Water));
        }

        return new IslandMap(tiles);
    }

    /// <summary>
    /// Generates coordinates in spiral order starting from the center.
    /// </summary>
    private static IEnumerable<HexCoord> GenerateSpiralCoords(int count)
    {
        if (count <= 0) yield break;
        yield return new HexCoord(0, 0);
        if (count == 1) yield break;

        int radius = 1;
        int yielded = 1;
        while (yielded < count)
        {
            foreach (var coord in GenerateRingCoords(radius))
            {
                yield return coord;
                yielded++;
                if (yielded >= count) yield break;
            }
            radius++;
        }
    }

    /// <summary>
    /// Generates coordinates for a given radius ring.
    /// </summary>
    private static IEnumerable<HexCoord> GenerateRingCoords(int radius)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Math.Max(-radius, -q - radius);
            int r2 = Math.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                int s = -q - r;
                if (Math.Abs(q) + Math.Abs(r) + Math.Abs(s) == 2 * radius)
                {
                    yield return new HexCoord(q, r);
                }
            }
        }
    }

    /// <summary>
    /// Shuffles a list randomly.
    /// </summary>
    private static List<T> Shuffle<T>(List<T> list)
    {
        var shuffled = new List<T>(list);
        var random = new Random();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        return shuffled;
    }
}
