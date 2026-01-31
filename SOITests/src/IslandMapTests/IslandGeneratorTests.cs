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
        var tileData = new List<(TerrainType terrainType, int tileCount)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
        };

        // Act
        var map = generator.GenerateIsland(tileData);
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
        var tileData = new List<(TerrainType terrainType, int productionNumber)>
        {
            (TerrainType.Forest, 1),
            (TerrainType.Hill, 1),
            (TerrainType.Pasture, 1),
        };

        // Act
        var map = generator.GenerateIsland(tileData);

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
        var tileData = new List<(TerrainType terrainType, int productionNumber)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
            (TerrainType.Field, 9),
            (TerrainType.Mountain, 10),
            (TerrainType.Forest, 11),
            (TerrainType.Hill, 12),
        };

        // Act
        var map = generator.GenerateIsland(tileData);

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

        // Act
        var map = generator.GenerateIsland(tileData);

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
        var tileData = new List<(TerrainType terrainType, int productionNumber)>
        {
            (TerrainType.Forest, 5),
            (TerrainType.Hill, 6),
            (TerrainType.Pasture, 8),
            (TerrainType.Field, 9),
            (TerrainType.Mountain, 10),
        };

        // Act
        var map = generator.GenerateIsland(tileData);

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
        var landData = new List<(TerrainType resource, int count)>();

        // Act
        var map = generator.GenerateIsland(landData);

        // Assert
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

        // Act
        var map = generator.GenerateIsland(tileData);

        // Assert
        Assert.True(HasVertexAdjacentToHillForestWater(map), "No vertex adjacent to Hill, Forest, and Water found.");
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
}