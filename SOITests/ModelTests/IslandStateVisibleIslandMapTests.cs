using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services;
using Xunit;

namespace SOITests.ModelTests;

public class IslandStateVisibleIslandMapTests
{
    [Fact]
    public void Constructor_BuildsVisibleIslandMapForEachCivilization()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var map = CreateMap(a, b, c);
        var civilization = new Civilization { Index = 0 };
        civilization.AddCity(new City(Vertex.Create(a, b, c)));

        var state = new WorldState(map, new List<Civilization> { civilization }, AtlasController.InvalidIslandId);

        Assert.True(state.Visibility.GetForZ(0).TryGetValue(0, out var visibleMap));
        Assert.True(visibleMap.HasTile(a));
        Assert.True(visibleMap.HasTile(b));
        Assert.True(visibleMap.HasTile(c));
    }

    [Fact]
    public void Serialization_DoesNotWriteVisibleIslandMaps_ButDeserializationRebuildsThem()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var map = CreateMap(a, b, c);
        var civilization = new Civilization { Index = 0 };
        civilization.AddCity(new City(Vertex.Create(a, b, c)));
        var state = new WorldState(map, new List<Civilization> { civilization }, AtlasController.InvalidIslandId);

        var json = JsonSerializer.Serialize(state, SerializationService.SerializationOptions());
        var reloaded = JsonSerializer.Deserialize<WorldState>(json, SerializationService.SerializationOptions());

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.Visibility.GetForZ(0).TryGetValue(0, out var visibleMap));
        Assert.Equal(3, visibleMap.Tiles.Count);
        Assert.True(visibleMap.HasTile(a));
        Assert.True(visibleMap.HasTile(b));
        Assert.True(visibleMap.HasTile(c));
    }

    [Fact]
    public void BuildRoad_RecalculatesVisibleIslandMapForCivilization()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var roadEndpointOnly = new HexCoord(1, -1, IslandMap.SurfaceLayer);
        var map = CreateMap(a, b, c, roadEndpointOnly);
        var civilization = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civilization }, AtlasController.InvalidIslandId);

        var cityVertex = Vertex.Create(a, b, c);
        new IslandMapGenerator(new GamePRNG(42)).PopulatePlayerCivilization(map, civilization, cityVertex);
        state.Visibility.RecalculateFor(0);
        Assert.False(state.Visibility.GetForZ(0).GetValueOrDefault(0)?.HasTile(roadEndpointOnly) ?? false);

        civilization.AddResource(Resource.Wood, 2);
        civilization.AddResource(Resource.Brick, 2);

        var controller = new RoadController(state);
        controller.BuildRoad(0, Edge.Create(a, b));

        Assert.True(state.Visibility.GetForZ(0).GetValueOrDefault(0)?.HasTile(roadEndpointOnly) ?? false);
    }

    private static IslandMap CreateMap(params HexCoord[] coords)
    {
        return new IslandMap(coords.Select(coord => new HexTile(coord, TerrainType.Plain)));
    }
}
