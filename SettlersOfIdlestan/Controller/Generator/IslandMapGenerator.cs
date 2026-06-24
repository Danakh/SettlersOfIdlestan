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
    /// The shape generator is chosen from parameters.ShapeType. <paramref name="surfaceCorruptionLevel"/>
    /// gives each land hex (outside the player's starting city) a 10%-per-level chance of being
    /// corrupted, with the corruption level itself rolled the same way as in auto-expand layers.
    /// </summary>
    public WorldState? GenerateWorldState(IslandParameters parameters, long currentTick, long startTick = 0, int surfaceCorruptionLevel = 0)
    {
        IslandShapeGenerator shapeGenerator = parameters.ShapeType switch
        {
            IslandShapeType.Crescent    => new IslandShapeGeneratorCrescent(),
            IslandShapeType.Archipelago => new IslandShapeGeneratorArchipelago(_prng),
            IslandShapeType.Elongated   => new IslandShapeGeneratorElongated(_prng),
            IslandShapeType.Lake        => new IslandShapeGeneratorLake(_prng),
            IslandShapeType.InlandSea   => new IslandShapeGeneratorInlandSea(_prng),
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

        if (parameters.HasBonusIsland)
            TryAddBonusIsland(WorldState);

        if (surfaceCorruptionLevel > 0)
            PlaceSurfaceCorruption(WorldState, surfaceCorruptionLevel);

        return WorldState;
    }

    private const int SurfaceCorruptionChancePerLevelPercent = 10;

    /// <summary>
    /// Donne à chaque hex de terre de la carte de surface (hors hexagones de la ville de départ
    /// et hors hexagones déjà occupés par une feature) une chance de <see cref="SurfaceCorruptionChancePerLevelPercent"/>
    /// par niveau d'être corrompu, le niveau de la corruption étant tiré comme dans les couches auto-étendues.
    /// </summary>
    private void PlaceSurfaceCorruption(WorldState worldState, int surfaceCorruptionLevel)
    {
        var map = worldState.GetMapForZ(IslandMap.SurfaceLayer);
        if (map == null) return;

        int chancePercent = SurfaceCorruptionChancePerLevelPercent * surfaceCorruptionLevel;

        var cityHexes = worldState.PlayerCivilization.Cities.Count > 0
            ? new HashSet<HexCoord>(worldState.PlayerCivilization.Cities[0].Position.GetHexes())
            : new HashSet<HexCoord>();

        foreach (var tile in map.Tiles.Values)
        {
            if (tile.TerrainType == TerrainType.Water) continue;
            if (cityHexes.Contains(tile.Coord)) continue;
            if (worldState.HasFeaturesAt(tile.Coord)) continue;

            if (_prng.Next(100) < chancePercent)
                worldState.AddFeature(new Corruption(tile.Coord, Corruption.RollLevel(_prng, surfaceCorruptionLevel)));
        }
    }

    // Petite île bonus rattachée à l'île principale par un isthme étroit (un edge entre deux
    // hex terrestres dont les deux hex de flanc sont de l'eau) : 1-2 hex, avec un trésor (80%),
    // un cercle de fées (10%) ou un dragon (10%) dessus.
    private const int BonusIslandSecondHexChancePercent = 50;
    private const int BonusIslandTreasureChancePercent = 80;
    private const int BonusIslandFairyCircleChancePercent = 10;

    private static readonly TerrainType[] BonusIslandTerrainPool =
    {
        TerrainType.Forest, TerrainType.Hill, TerrainType.Plain, TerrainType.Mountain, TerrainType.Desert,
    };

    /// <summary>
    /// Ajoute une petite île bonus (1-2 hex) accessible depuis l'île principale via un isthme.
    /// On cherche un hex terrestre côtier <c>a</c> et une direction où l'hex voisin <c>b</c> est
    /// encore de l'eau mais où les deux hex de flanc de la future arête a-b sont aussi de l'eau :
    /// poser un nouvel hex terrestre sur <c>b</c> crée alors un isthme complet (arête a-b flanquée
    /// d'eau des deux côtés), sans nécessiter qu'un tel isthme existe déjà sur la carte.
    /// </summary>
    private void TryAddBonusIsland(WorldState worldState)
    {
        var map = worldState.GetMapForZ(IslandMap.SurfaceLayer);
        if (map == null) return;

        var landCoords = new HashSet<HexCoord>(
            map.Tiles.Values.Where(t => t.TerrainType != TerrainType.Water).Select(t => t.Coord));

        var startCandidates = new List<(HexCoord start, HexCoord flank1, HexCoord flank2)>();
        foreach (var a in landCoords)
        {
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var b = a.Neighbor(dir);
                if (landCoords.Contains(b)) continue;

                var c1 = a.Neighbor(dir.Next());
                var c2 = a.Neighbor(dir.Previous());
                if (!landCoords.Contains(c1) && !landCoords.Contains(c2))
                    startCandidates.Add((b, c1, c2));
            }
        }

        if (startCandidates.Count == 0) return;

        var chosen = startCandidates[_prng.Next(startCandidates.Count)];
        var startHex = chosen.start;

        var islandHexes = new List<HexCoord> { startHex };
        if (_prng.Next(100) < BonusIslandSecondHexChancePercent)
        {
            // Exclut les deux hex de flanc qui garantissent l'isthme : les convertir en terre
            // briserait la configuration "route maritime" qu'on vient de créer.
            var extraCandidates = startHex.Neighbors()
                .Where(n => !landCoords.Contains(n) && !n.Equals(chosen.flank1) && !n.Equals(chosen.flank2))
                .ToList();
            if (extraCandidates.Count > 0)
                islandHexes.Add(extraCandidates[_prng.Next(extraCandidates.Count)]);
        }

        var terrain = BonusIslandTerrainPool[_prng.Next(BonusIslandTerrainPool.Length)];
        foreach (var hex in islandHexes)
            map.AddTile(new HexTile(hex, terrain));

        // Garantit que la nouvelle île est bien encerclée d'eau (étend la carte si nécessaire).
        foreach (var hex in islandHexes)
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var neighbor = hex.Neighbor(dir);
                if (!map.HasTile(neighbor))
                    map.AddTile(new HexTile(neighbor, TerrainType.Water));
            }

        var featureHex = islandHexes[_prng.Next(islandHexes.Count)];
        int roll = _prng.Next(100);
        IslandFeature feature = roll < BonusIslandTreasureChancePercent
            ? new TreasureTrove(featureHex)
            : roll < BonusIslandTreasureChancePercent + BonusIslandFairyCircleChancePercent
                ? new FairyCircle(featureHex)
                : new Dragon(featureHex);

        worldState.AddFeature(feature);
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

        bool alreadySatisfied = (tA == TerrainType.Hill || tB == TerrainType.Hill) &&
                                 (tA == TerrainType.Forest || tB == TerrainType.Forest);

        if (!alreadySatisfied)
        {
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
                    tB = TerrainType.Forest;
                }
            }
        }
        else if (tB == TerrainType.Hill)
        {
            // Normalize so hexA always ends up holding Hill, hexB holding Forest
            (hexA, hexB) = (hexB, hexA);
            (tA, tB) = (tB, tA);
        }

        if (tA == TerrainType.Hill && tB == TerrainType.Forest)
            EnsureMountainPlainNearStart(terrainDict, coordSet, hexA, hexB);
    }

    /// <summary>
    /// Swaps terrain so the land hexes adjacent to the starting Hill/Forest hexes contain both a
    /// Mountain and a Plain tile (one each), pulling tiles in from elsewhere in the terrain pool
    /// if needed. No-ops if Mountain/Plain tiles aren't available anywhere, or there's no adjacent
    /// land slot to put them on.
    /// </summary>
    private static void EnsureMountainPlainNearStart(
        Dictionary<HexCoord, TerrainType> terrainDict,
        HashSet<HexCoord> coordSet,
        HexCoord hillHex,
        HexCoord forestHex)
    {
        var candidates = HexDirectionUtils.AllHexDirections
            .SelectMany(d => new[] { hillHex.Neighbor(d), forestHex.Neighbor(d) })
            .Where(c => coordSet.Contains(c) && !c.Equals(hillHex) && !c.Equals(forestHex))
            .Distinct()
            .ToList();

        if (candidates.Count == 0) return;

        var excludedFromSource = new HashSet<HexCoord>(candidates) { hillHex, forestHex };

        EnsureTerrainAtOneOf(terrainDict, candidates, excludedFromSource, TerrainType.Mountain);
        EnsureTerrainAtOneOf(terrainDict, candidates, excludedFromSource, TerrainType.Plain);
    }

    /// <summary>
    /// Ensures at least one of the candidate hexes holds the given terrain, swapping a tile in
    /// from elsewhere on the map (excluding excludedFromSource) if none of them already do.
    /// </summary>
    private static void EnsureTerrainAtOneOf(
        Dictionary<HexCoord, TerrainType> terrainDict,
        List<HexCoord> candidates,
        HashSet<HexCoord> excludedFromSource,
        TerrainType terrain)
    {
        if (candidates.Any(c => terrainDict[c] == terrain)) return;

        var sourceCoord = terrainDict.Keys
            .FirstOrDefault(c => !excludedFromSource.Contains(c) && terrainDict[c] == terrain);
        if (sourceCoord is null) return;

        // Avoid overwriting a candidate that already holds the other guaranteed terrain (Mountain/Plain)
        var targetCoord = candidates.FirstOrDefault(c =>
            terrainDict[c] != TerrainType.Mountain && terrainDict[c] != TerrainType.Plain) ?? candidates[0];

        terrainDict[sourceCoord] = terrainDict[targetCoord];
        terrainDict[targetCoord] = terrain;
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
                case IslandFeatureType.Volcano:
                    WorldState.AddFeature(new VolcanoFeature(hex));
                    // 10 % de chance qu'un Dragon soit aussi présent sur le même hex
                    if (_prng.Next(100) < 10)
                        WorldState.AddFeature(new Dragon(hex));
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
