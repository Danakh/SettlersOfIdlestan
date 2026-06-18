using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Monsters;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates a random island map. The shape of the land is provided by an IslandShapeGenerator;
/// terrain is shuffled onto the shape coordinates, then swapped as needed to guarantee a
/// Hill/Forest/Water vertex for the player's starting city.
/// </summary>
public class IslandMapGenerator
{
    private readonly GamePRNG _prng;

    internal IslandMapGenerator(GamePRNG prng)
    {
        _prng = prng;
    }

    /// <summary>
    /// Generates an island map for the given terrain data and civilization list.
    /// An optional shape generator controls the land footprint; defaults to compact spiral.
    /// An optional preferred start hex biases the Hill/Forest/Water vertex placement.
    /// </summary>
    public IslandMap? GenerateIsland(
        IEnumerable<(TerrainType terrainType, int tileCount)> tileData,
        List<Civilization> civilizations,
        IslandShapeGenerator? shapeGenerator = null,
        HexCoord? preferredStartHex = null)
    {
        if (civilizations == null || civilizations.Count == 0)
            return null;

        var tileList = new List<TerrainType>();
        foreach (var (terrainType, tileCount) in tileData)
            for (int i = 0; i < tileCount; i++)
                tileList.Add(terrainType);

        if (tileList.Count == 0)
            return new IslandMap([]);

        bool hasHill = tileList.Contains(TerrainType.Hill);
        bool hasForest = tileList.Contains(TerrainType.Forest);

        shapeGenerator ??= new IslandShapeGeneratorCompact(_prng);

        // Generate land coordinates from shape
        var coords = shapeGenerator.GenerateCoords(tileList.Count);
        var coordSet = new HashSet<HexCoord>(coords);

        // Shuffle and assign terrain to coordinates
        var shuffledTiles = Shuffle(tileList);
        var terrainDict = new Dictionary<HexCoord, TerrainType>(coords.Count);
        for (int i = 0; i < coords.Count; i++)
            terrainDict[coords[i]] = shuffledTiles[i];

        // Swap terrain to guarantee a Hill/Forest/Water vertex near the preferred start
        HexCoord? startHex = preferredStartHex ?? (hasHill && hasForest
            ? shapeGenerator.GetPreferredStartHex(coords)
            : null);

        if (hasHill && hasForest)
            EnsureHillForestNearEdge(terrainDict, coordSet, startHex);

        // Build land tiles
        var tiles = new List<HexTile>(terrainDict.Count);
        foreach (var (coord, terrain) in terrainDict)
            tiles.Add(new HexTile(coord, terrain));

        // Add water ring around all land
        var waterCoords = new HashSet<HexCoord>();
        foreach (var coord in coordSet)
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = coord.Neighbor(dir);
                if (!coordSet.Contains(nb))
                    waterCoords.Add(nb);
            }
        foreach (var wc in waterCoords)
            tiles.Add(new HexTile(wc, TerrainType.Water));

        var map = new IslandMap(tiles);
        var vertex = hasHill && hasForest ? FindVertexAdjacentToHillForestWater(map) : null;

        if (vertex != null)
            PopulatePlayerCivilization(map, civilizations[0], vertex);

        return map;
    }

    /// <summary>
    /// Creates a complete WorldState: civilizations, map generation, and feature placement.
    /// The shape generator is chosen from parameters.ShapeType.
    /// </summary>
    public WorldState? GenerateWorldState(IslandParameters parameters, long currentTick, long startTick = 0)
    {
        IslandShapeGenerator shapeGenerator = parameters.ShapeType switch
        {
            IslandShapeType.Crescent    => new IslandShapeGeneratorCrescent(),
            IslandShapeType.Archipelago => new IslandShapeGeneratorArchipelago(_prng),
            IslandShapeType.Elongated   => new IslandShapeGeneratorElongated(_prng),
            _                           => new IslandShapeGeneratorCompact(_prng)
        };

        var civs = new List<Civilization> { new Civilization { Index = 0 } };
        for (int i = 0; i < parameters.NpcCivilizations.Count; i++)
            civs.Add(new Civilization
            {
                Index = i + 1,
                IsNpc = true,
                NpcParameters = parameters.NpcCivilizations[i]
            });

        var map = GenerateIsland(parameters.TileData, civs, shapeGenerator);
        if (map is null) return null;

        var WorldState = new WorldState(map, civs, parameters.WorldId) { StartTick = startTick };

        if (parameters.NpcCivilizations.Count > 0)
            new NpcCivilizationPlacer().PlaceNpcCivilizations(WorldState, _prng);

        if (parameters.Features.Count > 0)
            PlaceFeatures(WorldState, parameters.Features, currentTick);

        return WorldState;
    }

    public void PopulatePlayerCivilization(IslandMap map, Civilization civilization, Vertex vertex)
    {
        var city = new City(vertex);
        city.CivilizationIndex = civilization.Index;
        var townHall = new TownHall { Level = 1 };
        city.Buildings.Add(townHall);
        city.InvalidateLevelCache();
        civilization.AddCity(city);
    }

    /// <summary>
    /// Swaps terrain tiles in terrainDict so that an edge vertex adjacent to the preferred hex
    /// (or any edge vertex if preferredHex is null) has exactly one Hill and one Forest land tile.
    /// </summary>
    private static void EnsureHillForestNearEdge(
        Dictionary<HexCoord, TerrainType> terrainDict,
        HashSet<HexCoord> coordSet,
        HexCoord? preferredHex)
    {
        // Find a suitable edge vertex: 2 land hexes + 1 future-water hex
        (HexCoord hexA, HexCoord hexB)? target = null;

        foreach (var (a, b) in EnumerateEdgeLandPairs(coordSet))
        {
            if (preferredHex != null && (a.Equals(preferredHex) || b.Equals(preferredHex)))
            {
                target = (a, b);
                break;
            }
            target ??= (a, b);
        }

        if (target is null) return;

        var hexA = target.Value.hexA;
        var hexB = target.Value.hexB;

        var tA = terrainDict[hexA];
        var tB = terrainDict[hexB];

        // Already satisfied
        if ((tA == TerrainType.Hill || tB == TerrainType.Hill) &&
            (tA == TerrainType.Forest || tB == TerrainType.Forest))
            return;

        // Ensure hexA holds Hill
        if (tA != TerrainType.Hill && tB != TerrainType.Hill)
        {
            // Bring a Hill tile to hexA
            var hillCoord = terrainDict.Keys
                .FirstOrDefault(c => !c.Equals(hexA) && !c.Equals(hexB) && terrainDict[c] == TerrainType.Hill);
            if (hillCoord is not null)
            {
                terrainDict[hillCoord] = tA;
                terrainDict[hexA] = TerrainType.Hill;
                tA = TerrainType.Hill;
            }
        }
        else if (tB == TerrainType.Hill)
        {
            // Put Hill in the hexA slot
            (hexA, hexB) = (hexB, hexA);
            (tA, tB) = (tB, tA);
        }

        // Ensure hexB holds Forest
        if (tB != TerrainType.Forest)
        {
            var forestCoord = terrainDict.Keys
                .FirstOrDefault(c => !c.Equals(hexA) && !c.Equals(hexB) && terrainDict[c] == TerrainType.Forest);
            if (forestCoord is not null)
            {
                terrainDict[forestCoord] = tB;
                terrainDict[hexB] = TerrainType.Forest;
            }
        }
    }

    /// <summary>
    /// Yields all pairs (a, b) of adjacent land hexes where both are in coordSet and
    /// at least one of their shared non-a/b neighbors is NOT in coordSet (water slot exists).
    /// </summary>
    private static IEnumerable<(HexCoord a, HexCoord b)> EnumerateEdgeLandPairs(HashSet<HexCoord> coordSet)
    {
        foreach (var a in coordSet)
        {
            foreach (var d in HexDirectionUtils.AllHexDirections)
            {
                var b = a.Neighbor(d);
                if (!coordSet.Contains(b)) continue;

                var c1 = a.Neighbor(d.Next());
                if (!coordSet.Contains(c1))
                {
                    yield return (a, b);
                    break; // one water slot found for this (a,b) pair is enough
                }

                var c2 = a.Neighbor(d.Previous());
                if (!coordSet.Contains(c2))
                {
                    yield return (a, b);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Places island features (bandits, treasure troves) into the island state.
    /// </summary>
    public void PlaceFeatures(WorldState WorldState, IEnumerable<IslandFeatureParameters> features, long currentTick)
    {
        var landHexes = WorldState.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles.Values
            .Where(t => t.TerrainType != TerrainType.Water)
            .Select(t => t.Coord)
            .ToList();

        if (landHexes.Count == 0) return;

        HexCoord[]? cityHexes = WorldState.PlayerCivilization.Cities.Count > 0
            ? WorldState.PlayerCivilization.Cities[0].Position.GetHexes()
            : null;

        // Never place a feature on one of the player's two starting land hexes.
        if (cityHexes != null)
            landHexes.RemoveAll(cityHexes.Contains);

        if (landHexes.Count == 0) return;

        HexCoord[] allCivHexes = WorldState.Civilizations
            .SelectMany(c => c.Cities)
            .SelectMany(city => city.Position.GetHexes())
            .ToArray();

        foreach (var feature in features)
        {
            var hex = PickHex(landHexes, cityHexes, allCivHexes, feature.Placement);
            switch (feature.Type)
            {
                case IslandFeatureType.Bandit:
                    WorldState.AddFeature(new Bandit(hex, currentTick));
                    break;
                case IslandFeatureType.TreasureTrove:
                    WorldState.AddFeature(new TreasureTrove(hex));
                    break;
                case IslandFeatureType.BanditHideout:
                    WorldState.AddFeature(new BanditHideout(hex));
                    break;
                case IslandFeatureType.Dragon:
                    WorldState.AddFeature(new Dragon(hex));
                    break;
                case IslandFeatureType.Rats:
                    WorldState.AddFeature(new Rats(hex));
                    break;
            }
        }
    }

    private HexCoord PickHex(List<HexCoord> landHexes, HexCoord[]? cityHexes, HexCoord[] allCivHexes, IslandFeaturePlacement placement)
    {
        if (placement == IslandFeaturePlacement.FarFromAllCivilization)
        {
            if (allCivHexes.Length == 0)
                return landHexes[_prng.Next(landHexes.Count)];

            var candidates = new List<HexCoord>(10);
            for (int i = 0; i < 10; i++)
                candidates.Add(landHexes[_prng.Next(landHexes.Count)]);

            int MinDistToAnyCiv(HexCoord hex) => allCivHexes.Min(ch => hex.DistanceTo(ch));
            return candidates.OrderByDescending(MinDistToAnyCiv).First();
        }

        if (placement == IslandFeaturePlacement.Random || cityHexes == null)
            return landHexes[_prng.Next(landHexes.Count)];

        var playerCandidates = new List<HexCoord>(5);
        for (int i = 0; i < 5; i++)
            playerCandidates.Add(landHexes[_prng.Next(landHexes.Count)]);

        int DistanceToCity(HexCoord hex) => cityHexes.Min(ch => hex.DistanceTo(ch));

        return placement == IslandFeaturePlacement.FarFromPlayer
            ? playerCandidates.OrderByDescending(DistanceToCity).First()
            : playerCandidates.OrderBy(DistanceToCity).First();
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
            if (kvp.Value.TerrainType != TerrainType.Hill) continue;

            foreach (var d in HexDirectionUtils.AllHexDirections)
            {
                var b = a.Neighbor(d);
                var terrainB = coordToTerrain.TryGetValue(b, out var tb) ? tb : TerrainType.Desert;
                if (terrainB != TerrainType.Forest) continue;

                var c = a.Neighbor(d.Next());
                var terrainC = coordToTerrain.TryGetValue(c, out var tc) ? tc : TerrainType.Desert;
                if (terrainC == TerrainType.Water)
                    return Vertex.Create(a, b, c);

                c = a.Neighbor(d.Previous());
                terrainC = coordToTerrain.TryGetValue(c, out tc) ? tc : TerrainType.Desert;
                if (terrainC == TerrainType.Water)
                    return Vertex.Create(a, b, c);
            }
        }
        return null;
    }

    private List<T> Shuffle<T>(List<T> list)
    {
        var shuffled = new List<T>(list);
        _prng.Shuffle(shuffled);
        return shuffled;
    }
}
