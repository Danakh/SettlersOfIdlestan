using System.Collections.Generic;
using System.Linq;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;

namespace SOITests.IslandMapTests;

public class IslandGeneratorTests
{
    [Fact]
    public void GenerateIsland_WithLandTiles_PlacesAllTiles()
    {
        // Arrange
        var generator = new IslandGenerator();
        var landData = new List<(Resource resource, int? productionNumber)>
        {
            (Resource.Wood, 5),
            (Resource.Brick, 6),
            (Resource.Sheep, 8),
        };

        // Act
        var map = generator.GenerateIsland(landData);

        // Assert
        Assert.Equal(3, map.Tiles.Count(t => t.Value.TerrainType == TerrainType.Land));
        Assert.True(map.Tiles.Any(t => t.Value.TerrainType == TerrainType.Water));
    }

    [Fact]
    public void GenerateIsland_LandTilesHaveAtLeastTwoNeighbors()
    {
        // Arrange
        var generator = new IslandGenerator();
        var landData = new List<(Resource resource, int? productionNumber)>
        {
            (Resource.Wood, 5),
            (Resource.Brick, 6),
            (Resource.Sheep, 8),
            (Resource.Wheat, 9),
            (Resource.Ore, 10),
            (Resource.Wood, 11),
            (Resource.Brick, 12),
        };

        // Act
        var map = generator.GenerateIsland(landData);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.TerrainType == TerrainType.Land).ToList();
        foreach (var tile in landTilesInMap)
        {
            var landNeighbors = map.GetNeighbors(tile.Coord).Count(n => n.TerrainType == TerrainType.Land);
            Assert.True(landNeighbors >= 2, $"Tile at {tile.Coord} has only {landNeighbors} land neighbors");
        }
    }

    [Fact]
    public void GenerateIsland_LandTilesAreSurroundedByWater()
    {
        // Arrange
        var generator = new IslandGenerator();
        var landData = new List<(Resource resource, int? productionNumber)>
        {
            (Resource.Wood, 5),
            (Resource.Brick, 6),
            (Resource.Sheep, 8),
        };

        // Act
        var map = generator.GenerateIsland(landData);

        // Assert
        var landTilesInMap = map.Tiles.Values.Where(t => t.TerrainType == TerrainType.Land).ToList();
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
        var landData = new List<(Resource resource, int? productionNumber)>
        {
            (Resource.Wood, 5),
            (Resource.Brick, 6),
            (Resource.Sheep, 8),
            (Resource.Wheat, 9),
            (Resource.Ore, 10),
        };

        // Act
        var map = generator.GenerateIsland(landData);

        // Assert
        var resources = map.Tiles.Values.Where(t => t.TerrainType == TerrainType.Land).Select(t => t.Resource).ToList();
        Assert.Contains(Resource.Wood, resources);
        Assert.Contains(Resource.Brick, resources);
        Assert.True(map.Tiles.Values.Any(t => t.TerrainType == TerrainType.Water));
    }

    [Fact]
    public void GenerateIsland_EmptyList_ReturnsEmptyMap()
    {
        // Arrange
        var generator = new IslandGenerator();
        var landData = new List<(Resource resource, int? productionNumber)>();

        // Act
        var map = generator.GenerateIsland(landData);

        // Assert
        Assert.Empty(map.Tiles);
    }
}