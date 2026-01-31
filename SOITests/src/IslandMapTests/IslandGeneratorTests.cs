using System.Collections.Generic;
using System.Linq;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Civilization;
using System.Text.Json;

namespace SOITests.IslandMapTests;

public class IslandGeneratorTests
{
    [Fact]
    public void GenerateIsland_WithLandTiles_PlacesAllTiles()
    {
        // Arrange
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
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
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 1),
            (TerrainType.Hill, 1),
            (TerrainType.Pasture, 1),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        IslandMap? map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.Resource.HasValue).ToList();
        foreach (var tile in landTilesInMap)
        {
            var landNeighbors = map.GetNeighbors(tile.Coord).Count(n => n.Resource.HasValue);
            Assert.True(landNeighbors >= 2, $"Tile at {tile.Coord} has only {landNeighbors} land neighbors");
        }
    }

    [Fact]
    public void GenerateIsland_LandTilesHaveAtLeastTwoNeighbors()
    {
        // Arrange
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
            (TerrainType.Field, 9),
            (TerrainType.Mountain, 10),
            (TerrainType.Forest, 11),
            (TerrainType.Hill, 12),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        IslandMap? map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.Resource.HasValue).ToList();
        foreach (var tile in landTilesInMap)
        {
            var landNeighbors = map.GetNeighbors(tile.Coord).Count(n => n.Resource.HasValue);
            Assert.True(landNeighbors >= 2, $"Tile at {tile.Coord} has only {landNeighbors} land neighbors");
        }
    }

    [Fact]
    public void GenerateIsland_LandTilesAreSurroundedByWater()
    {
        // Arrange
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 1),
            (TerrainType.Hill, 1),
            (TerrainType.Pasture, 1),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.Resource.HasValue).ToList();
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
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
            (TerrainType.Field, 9),
            (TerrainType.Mountain, 10),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };

        // Act
        var map = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(map);

        // Assert
        var resources = map.Tiles.Values.Where(t => t.Resource.HasValue).Select(t => t.Resource!.Value).ToList();
        Assert.Contains(Resource.Wood, resources);
        Assert.Contains(Resource.Brick, resources);
        Assert.Contains(map.Tiles.Values, t => t.TerrainType == TerrainType.Water);
    }

    [Fact]
    public void GenerateIsland_EmptyList_ReturnsEmptyMap()
    {
        // Arrange
        var generator = new IslandGenerator();
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
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Hill, 1),
            (TerrainType.Forest, 1),
            (TerrainType.Pasture, 1),
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

    [Fact]
    public void FindVertexAdjacentToHillForestWater_ReturnsVertex_WhenHillForestWaterAreAdjacent()
    {
        // Arrange
        var tiles = new List<HexTile>
        {
            new HexTile(new HexCoord(0, 0), TerrainType.Hill, null),
            new HexTile(new HexCoord(0, 1), TerrainType.Forest, null),
            new HexTile(new HexCoord(1, 0), TerrainType.Water),
        };
        var map = new IslandMap(tiles);

        // Act
        var vertex = IslandGenerator.FindVertexAdjacentToHillForestWater(map);

        // Assert
        Assert.NotNull(vertex);
        var expectedCoords = new HashSet<HexCoord> { new HexCoord(0, 0), new HexCoord(0, 1), new HexCoord(1, 0) };
        var actualCoords = new HashSet<HexCoord> { vertex.Hex1, vertex.Hex2, vertex.Hex3 };
        Assert.Equal(expectedCoords, actualCoords);
    }

    [Fact]
    public void FindVertexAdjacentToHillForestWater_ReturnsNull_WhenNoSuchVertexExists()
    {
        // Arrange
        var tiles = new List<HexTile>
        {
            new HexTile(new HexCoord(0, 0), TerrainType.Hill, null),
            new HexTile(new HexCoord(0, 1), TerrainType.Forest, null),
            new HexTile(new HexCoord(2, 0), TerrainType.Water), // Not adjacent
        };
        var map = new IslandMap(tiles);

        // Act
        var vertex = IslandGenerator.FindVertexAdjacentToHillForestWater(map);

        // Assert
        Assert.Null(vertex);
    }

    [Fact]
    public void FindVertexAdjacentToHillForestWater_ReturnsNull_WhenMissingHill()
    {
        // Arrange
        var tiles = new List<HexTile>
        {
            new HexTile(new HexCoord(0, 0), TerrainType.Forest, null),
            new HexTile(new HexCoord(0, 1), TerrainType.Pasture, null),
            new HexTile(new HexCoord(1, 0), TerrainType.Water),
        };
        var map = new IslandMap(tiles);

        // Act
        var vertex = IslandGenerator.FindVertexAdjacentToHillForestWater(map);

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
        var generator = new IslandGenerator();
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
        };
        var civilizations = new List<Civilization> { new() { Index = 0 } };
        var originalMap = generator.GenerateIsland(tileData, civilizations);
        Assert.NotNull(originalMap);
        var original = new IslandState(originalMap, civilizations);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<IslandState>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(original.Map.Tiles.Count, deserialized.Map.Tiles.Count);
        Assert.Equal(original.Civilizations.Count, deserialized.Civilizations.Count);
        Assert.Equal(original.PlayerCivilization.Index, deserialized.PlayerCivilization.Index);
    }
}