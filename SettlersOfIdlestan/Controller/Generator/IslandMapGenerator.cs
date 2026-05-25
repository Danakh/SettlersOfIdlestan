using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Bandits;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates a random island map based on a list of land tiles.
/// The land tiles are placed in a connected hexagonal layout with each having at least two neighbors,
/// then surrounded by water tiles.
/// </summary>
public class IslandMapGenerator
{
    private readonly GamePRNG _prng;

    internal IslandMapGenerator(GamePRNG? prng = null)
    {
        _prng = prng ?? new GamePRNG();
    }
    /// <summary>
    /// Generates an island map from the provided land tile data.
    /// The tiles are shuffled and assigned to coordinates in a spiral order to ensure connectivity.
    /// Water tiles are added around the land tiles.
    /// </summary>
    /// <param name="tileData">The list of land tile data (resource and tile count).</param>
    /// <param name="civilizations">The list of civilizations.</param>
    /// <returns>The generated island map, or null if generation fails.</returns>
    public IslandMap? GenerateIsland(IEnumerable<(TerrainType terrainType, int tileCount)> tileData, List<Civilization> civilizations)
    {
        if (civilizations == null || civilizations.Count == 0)
        {
            return null;
        }

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

        bool hasHill = tileList.Contains(TerrainType.Hill);
        bool hasForest = tileList.Contains(TerrainType.Forest);
        IslandMap map;
        Vertex? vertex = null;
        int attempts = 0;
        do
        {
            // Shuffle the land tiles for randomness
            var shuffledTiles = Shuffle(tileList);

            // Generate coordinates in spiral order
            var coords = GenerateSpiralCoords(shuffledTiles.Count).ToList();

            if (hasHill && hasForest)
            {
                // Find the index of the last Forest
                int lastForestIndex = -1;
                for (int i = shuffledTiles.Count - 1; i >= 0; i--)
                {
                    if (shuffledTiles[i] == TerrainType.Forest)
                    {
                        lastForestIndex = i;
                        break;
                    }
                }
                if (lastForestIndex >= 0 && lastForestIndex < shuffledTiles.Count - 10)
                {
                    // Move it to the last position
                    int targetIndex = shuffledTiles.Count - 1;
                    (shuffledTiles[lastForestIndex], shuffledTiles[targetIndex]) = (shuffledTiles[targetIndex], shuffledTiles[lastForestIndex]);
                    lastForestIndex = targetIndex;
                }

                // Find the index of the last Hill
                int lastHillIndex = -1;
                for (int i = shuffledTiles.Count - 1; i >= 0; i--)
                {
                    if (shuffledTiles[i] == TerrainType.Hill)
                    {
                        lastHillIndex = i;
                        break;
                    }
                }
                if (lastHillIndex >= 0)
                {
                    // Find a position adjacent to the Forest's coord
                    var forestCoord = coords[lastForestIndex];
                    int adjacentIndex = -1;
                    for (int i = 0; i < coords.Count; i++)
                    {
                        if (i != lastForestIndex && forestCoord.DistanceTo(coords[i]) == 1)
                        {
                            adjacentIndex = i;
                            break;
                        }
                    }
                    if (adjacentIndex >= 0 && adjacentIndex != lastHillIndex)
                    {
                        // Move the Hill to the adjacent position
                        (shuffledTiles[lastHillIndex], shuffledTiles[adjacentIndex]) = (shuffledTiles[adjacentIndex], shuffledTiles[lastHillIndex]);
                    }
                }
            }

            // Create new land tiles with assigned coordinates
            var tiles = new List<HexTile>();
            for (int i = 0; i < shuffledTiles.Count; i++)
            {
                var terrainType = shuffledTiles[i];

                var newTile = new HexTile(coords[i], terrainType);
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

            map = new IslandMap(tiles);
            vertex = FindVertexAdjacentToHillForestWater(map);
            attempts++;
        } while (hasHill && hasForest && vertex == null && attempts < 10);

        if (vertex != null)
        {
            PopulatePlayerCivilization(map, civilizations[0], vertex);
        }

        return map;
    }

    public void PopulatePlayerCivilization(IslandMap map, Civilization civilization, Vertex vertex)
    {
        var city = new City(vertex);
        city.CivilizationIndex = civilization.Index;

        var townHall = new TownHall();
        townHall.Level = 1;
        city.Buildings.Add(townHall);

        civilization.Cities.Add(city);
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
    /// Shuffles a list randomly using the configured PRNG if available.
    /// </summary>
    private List<T> Shuffle<T>(List<T> list)
    {
        var shuffled = new List<T>(list);
        _prng.Shuffle(shuffled);
        return shuffled;
    }

    /// <summary>
    /// Places bandits on the island according to the island features.
    /// Each feature specifies a bandit count and a placement strategy relative to the player city.
    /// </summary>
    public List<Bandit> GenerateFeatureBandits(IslandMap map, Civilization playerCiv, IEnumerable<IslandFeature> features, long currentTick)
    {
        var result = new List<Bandit>();

        var landHexes = map.Tiles.Values
            .Where(t => t.TerrainType != TerrainType.Water)
            .Select(t => t.Coord)
            .ToList();

        if (landHexes.Count == 0) return result;

        HexCoord[]? cityHexes = playerCiv.Cities.Count > 0
            ? playerCiv.Cities[0].Position.GetHexes()
            : null;

        foreach (var feature in features)
        {
            for (int i = 0; i < feature.BanditCount; i++)
            {
                var hex = PickBanditHex(landHexes, cityHexes, feature.Placement);
                result.Add(new Bandit(hex, currentTick));
            }
        }

        return result;
    }

    private HexCoord PickBanditHex(List<HexCoord> landHexes, HexCoord[]? cityHexes, IslandFeaturePlacement placement)
    {
        if (placement == IslandFeaturePlacement.Random || cityHexes == null)
            return landHexes[_prng.Next(landHexes.Count)];

        var candidates = new List<HexCoord>(3);
        for (int i = 0; i < 3; i++)
            candidates.Add(landHexes[_prng.Next(landHexes.Count)]);

        int DistanceToCity(HexCoord hex) => cityHexes.Min(ch => hex.DistanceTo(ch));

        return placement == IslandFeaturePlacement.FarFromPlayer
            ? candidates.OrderByDescending(DistanceToCity).First()
            : candidates.OrderBy(DistanceToCity).First();
    }

    /// <summary>
    /// Finds a vertex adjacent to Hill, Forest, and Water tiles.
    /// </summary>
    static public Vertex? FindVertexAdjacentToHillForestWater(IslandMap map)
    {
        var coordToTerrain = map.Tiles.ToDictionary(t => t.Key, t => t.Value.TerrainType);
        foreach (var kvp in map.Tiles)
        {
            var a = kvp.Key;
            var terrainA = kvp.Value.TerrainType;
            if (terrainA != TerrainType.Hill)
            {
                continue;
            }
            foreach (var d in HexDirectionUtils.AllHexDirections)
            {
                var b = a.Neighbor(d);
                var terrainB = coordToTerrain.TryGetValue(b, out var tb) ? tb : TerrainType.Desert;
                if (terrainB != TerrainType.Forest)
                {
                    continue;
                }
                var c = a.Neighbor(d.Next());
                var terrainC = coordToTerrain.TryGetValue(c, out var tc) ? tc : TerrainType.Desert;
                if (terrainC == TerrainType.Water)
                {
                    return Vertex.Create(a, b, c);
                }
                c = a.Neighbor(d.Previous());
                terrainC = coordToTerrain.TryGetValue(c, out tc) ? tc : TerrainType.Desert;
                if (terrainC == TerrainType.Water)
                {
                    return Vertex.Create(a, b, c);
                }
            }
        }
        return null;
    }
}
