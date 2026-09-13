using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestan.Controller;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace SOITests.IslandMapTests;

public class IslandGeneratorTests
{
    [Fact]
    public void GenerateIsland_WithLandTiles_PlacesAllTiles()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Plain, 8),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);
        Assert.Equal(5 + 6 + 8, map.Tiles.Count(t => t.Value.TerrainType != TerrainType.Water));
        Assert.Equal(5, map.Tiles.Count(t => t.Value.TerrainType == TerrainType.Forest));
        Assert.DoesNotContain(map.Tiles, t => t.Value.TerrainType == TerrainType.Desert);
    }

    [Fact]
    public void GenerateIsland_LandTilesHaveAtLeastTwoNeighborsSmall()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 1),
            (TerrainType.Hill, 1),
            (TerrainType.Plain, 1),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        IslandMap? map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.TerrainType != TerrainType.Water).ToList();
        foreach (var tile in landTilesInMap)
        {
            var landNeighbors = map.GetNeighbors(tile.Coord).Count(n => n.TerrainType != TerrainType.Water);
            Assert.True(landNeighbors >= 2, $"Tile at {tile.Coord} has only {landNeighbors} land neighbors");
        }
    }

    [Fact]
    public void GenerateIsland_LandTilesHaveAtLeastTwoNeighbors()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Plain, 8),
            (TerrainType.Mountain, 10),
            (TerrainType.Forest, 11),
            (TerrainType.Hill, 12),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        IslandMap? map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.TerrainType != TerrainType.Water).ToList();
        foreach (var tile in landTilesInMap)
        {
            var landNeighbors = map.GetNeighbors(tile.Coord).Count(n => n.TerrainType != TerrainType.Water);
            Assert.True(landNeighbors >= 2, $"Tile at {tile.Coord} has only {landNeighbors} land neighbors");
        }
    }

    [Fact]
    public void GenerateIsland_LandTilesAreSurroundedByWater()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 1),
            (TerrainType.Hill, 1),
            (TerrainType.Plain, 1),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.TerrainType != TerrainType.Water).ToList();
        foreach (var tile in landTilesInMap)
        {
            var waterNeighbors = map.GetNeighbors(tile.Coord).Count(n => n.TerrainType == TerrainType.Water);
            Assert.True(waterNeighbors > 0, $"Tile at {tile.Coord} has no water neighbors");
        }
    }

    [Fact]
    public void GenerateIsland_ContainsRequiredResourcesAndWater()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Plain, 8),
            (TerrainType.Mountain, 10),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var terrainTypes = map.Tiles.Values.Select(t => t.TerrainType).ToList();
        Assert.Contains(TerrainType.Forest, terrainTypes);
        Assert.Contains(TerrainType.Hill, terrainTypes);
        Assert.Contains(TerrainType.Plain, terrainTypes);
        Assert.Contains(TerrainType.Mountain, terrainTypes);
        Assert.Contains(TerrainType.Water, terrainTypes);
    }

    [Fact]
    public void GenerateIsland_EmptyList_ReturnsEmptyMap()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var landData = new List<(TerrainType terrainType, int tileCount)>();
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(landData, civilizations);

        // Assert
        Assert.NotNull(map);
        Assert.Empty(map.Tiles);
    }

    [Fact]
    public void GenerateIsland_HasVertexAdjacentToHillForestWater_WhenPossible()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Hill, 1),
            (TerrainType.Forest, 1),
            (TerrainType.Plain, 1),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);

        // Assert
        Assert.NotNull(map);
        Assert.Single(civilizations[0].Cities);
        Assert.True(HasVertexAdjacentToHillForestWater(map));
        Vertex cityPos = civilizations[0].Cities[0].Position!;
        // Check that the city is indeed adjacent to Hill, Forest, and Water
        var terrainsAtCity = new HashSet<TerrainType>();
        foreach (HexCoord coord in new[] { cityPos.Hex1, cityPos.Hex2, cityPos.Hex3} )
        {
            bool gotHex = map.Tiles.TryGetValue(coord, out var tile);
            Assert.True(gotHex, $"City vertex at {cityPos} has missing adjacent hex at {coord}");
            Assert.NotNull(tile);
            terrainsAtCity.Add(tile.TerrainType);
        }
        Assert.Contains(TerrainType.Hill, terrainsAtCity);
        Assert.Contains(TerrainType.Forest, terrainsAtCity);
        Assert.Contains(TerrainType.Water, terrainsAtCity);
    }

    /// <summary>
    /// Départ nain : triangle entièrement terrestre Montagne/Forêt/Colline, sans un seul hex d'Eau
    /// (voir RaceDefinition.StartVertexThirdTerrain). Balayé sur toutes les formes d'île et
    /// plusieurs seeds : c'est la garantie qui permet à la capitale de respecter la restriction de
    /// placement naine, et son absence se paierait par une partie sans ville de départ du tout.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(999)]
    [InlineData(12345)]
    public void GenerateWorldState_Dwarf_StartsOnInlandMountainForestHillTriangle(int seed)
    {
        foreach (var shape in System.Enum.GetValues<IslandShapeType>())
        {
            var generator = new IslandMapGenerator(new GamePRNG(seed));
            var parameters = new IslandParameters(
                worldId: 1,
                tileData: new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 13),
                    (TerrainType.Hill, 13),
                    (TerrainType.Plain, 13),
                    (TerrainType.Mountain, 13),
                    (TerrainType.Desert, 4),
                },
                shapeType: shape);

            var state = generator.GenerateWorldState(parameters, currentTick: 0,
                race: RaceDefinitions.Get(RaceId.Dwarf));

            Assert.NotNull(state);
            var city = Assert.Single(state.PlayerCivilization.Cities);
            var map = state.GetMapFor(city.Position)!;
            var terrains = city.Position.GetHexes().Select(h => map.Tiles[h].TerrainType).ToHashSet();

            Assert.Equal(
                new HashSet<TerrainType> { TerrainType.Mountain, TerrainType.Forest, TerrainType.Hill },
                terrains);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(42)]
    [InlineData(12345)]
    public void GenerateIsland_GuaranteesMountainAndPlainAdjacentToStartingHillAndForest(int seed)
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(seed));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Hill, 1),
            (TerrainType.Forest, 1),
            (TerrainType.Mountain, 1),
            (TerrainType.Plain, 1),
            (TerrainType.Desert, 6),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);
        Assert.Single(civilizations[0].Cities);

        // Assert: the city's Hill and Forest hexes both have a land neighbor, and together
        // those neighbors contain a Mountain and a Plain.
        Vertex cityPos = civilizations[0].Cities[0].Position!;
        var coordToTerrain = map.Tiles.ToDictionary(t => t.Key, t => t.Value.TerrainType);
        var hillHex = new[] { cityPos.Hex1, cityPos.Hex2, cityPos.Hex3 }
            .First(c => coordToTerrain[c] == TerrainType.Hill);
        var forestHex = new[] { cityPos.Hex1, cityPos.Hex2, cityPos.Hex3 }
            .First(c => coordToTerrain[c] == TerrainType.Forest);

        var neighborTerrains = hillHex.Neighbors().Concat(forestHex.Neighbors())
            .Where(coordToTerrain.ContainsKey)
            .Select(c => coordToTerrain[c])
            .ToHashSet();

        Assert.Contains(TerrainType.Mountain, neighborTerrains);
        Assert.Contains(TerrainType.Plain, neighborTerrains);
    }

    [Fact]
    public void FindStartVertex_ReturnsVertex_WhenHillForestWaterAreAdjacent()
    {
        // Arrange
        var tiles = new List<HexTile>
        {
            new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Hill),
            new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Forest),
            new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Water),
        };
        var map = new IslandMap(tiles);

        // Act
        var vertex = IslandMapGenerator.FindStartVertex(map);

        // Assert
        Assert.NotNull(vertex);
        var expectedCoords = new HashSet<HexCoord> { new HexCoord(0, 0, IslandMap.SurfaceLayer), new HexCoord(0, 1, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer) };
        var actualCoords = new HashSet<HexCoord> { vertex.Hex1, vertex.Hex2, vertex.Hex3 };
        Assert.Equal(expectedCoords, actualCoords);
    }

    [Fact]
    public void FindStartVertex_ReturnsNull_WhenNoSuchVertexExists()
    {
        // Arrange
        var tiles = new List<HexTile>
        {
            new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Hill),
            new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Forest),
            new HexTile(new HexCoord(2, 0, IslandMap.SurfaceLayer), TerrainType.Water),
        };
        var map = new IslandMap(tiles);

        // Act
        var vertex = IslandMapGenerator.FindStartVertex(map);

        // Assert
        Assert.Null(vertex);
    }

    [Fact]
    public void FindStartVertex_ReturnsNull_WhenMissingHill()
    {
        // Arrange
        var tiles = new List<HexTile>
        {
            new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Forest),
            new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Water),
        };
        var map = new IslandMap(tiles);

        // Act
        var vertex = IslandMapGenerator.FindStartVertex(map);

        // Assert
        Assert.Null(vertex);
    }

    private bool HasVertexAdjacentToHillForestWater(IslandMap map)
    {
        var coordToTerrain = map.Tiles.ToDictionary(t => t.Key, t => t.Value.TerrainType);
        foreach (var kvp in map.Tiles)
        {
            var a = kvp.Key;
            var terrainA = kvp.Value.TerrainType;
            foreach (var d in HexDirectionUtils.AllHexDirections)
            {
                var b = a.Neighbor(d);
                var terrainB = coordToTerrain.TryGetValue(b, out var tb) ? tb : TerrainType.Water;
                var c = a.Neighbor(d.Next());
                var terrainC = coordToTerrain.TryGetValue(c, out var tc) ? tc : TerrainType.Water;
                var terrains = new HashSet<TerrainType> { terrainA, terrainB, terrainC };
                if (terrains.SetEquals(new HashSet<TerrainType> { TerrainType.Hill, TerrainType.Forest, TerrainType.Water }))
                {
                    return true;
                }
            }
        }
        return false;
    }

    [Fact]
    public void IslandState_Serialization_RoundTrip()
    {
        // Arrange
        var generator = new IslandMapGenerator(new GamePRNG(42));
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Plain, 8),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };
        var originalMap = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(originalMap);
        var original = new WorldState(originalMap, civilizations, AtlasController.InvalidIslandId);

        // Act
        var json = JsonSerializer.Serialize(original, SaveController.SerializationOptions());
        var deserialized = JsonSerializer.Deserialize<WorldState>(json, SaveController.SerializationOptions());

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles.Count, deserialized.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles.Count);
        Assert.Equal(original.Civilizations.Count, deserialized.Civilizations.Count);
        Assert.Equal(original.PlayerCivilization.Index, deserialized.PlayerCivilization.Index);
    }
}