using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ModelTests;

public class MultiMapStateTests
{
    [Fact]
    public void UnderworldState_CreateDefault_UsesUnderworldLayer()
    {
        var playerCiv = new Civilization { Index = 0 };
        var underworld = LayerState.EstablishOupostInNewAutoExpandLayer(playerCiv);

        Assert.Equal(LayerState.UnderworldZ, underworld.Map.Z);
        Assert.Single(playerCiv.Cities);
        Assert.All(underworld.Map.Tiles.Keys, coord => Assert.Equal(LayerState.UnderworldZ, coord.Z));
        Assert.Equal(LayerState.UnderworldZ, playerCiv.Cities[0].Position.Z);
    }

    [Fact]
    public void IslandMap_RejectsMixedLayers()
    {
        var surface = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var underworld = new HexCoord(1, 0, LayerState.UnderworldZ);

        Assert.Throws<ArgumentException>(() => new IslandMap([
            new HexTile(surface, TerrainType.Plain),
            new HexTile(underworld, TerrainType.Mountain),
        ]));
    }

    [Fact]
    public void IslandMap_GetTile_WithDifferentLayer_ThrowsArgumentException()
    {
        var map = new IslandMap([
            new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        ]);

        Assert.Throws<ArgumentException>(() => map.GetTile(new HexCoord(0, 0, LayerState.UnderworldZ)));
    }

    [Fact]
    public void RecalculateVisibleIslandMaps_BuildsVisibleMapForEachLayer()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var civ = new Civilization { Index = 0 };
        civ.AddCity(new City(Vertex.Create(a, b, c)) { CivilizationIndex = 0 });

        var state = new WorldState(
            new IslandMap([
                new HexTile(a, TerrainType.Plain),
                new HexTile(b, TerrainType.Plain),
                new HexTile(c, TerrainType.Plain),
            ]),
            new List<Civilization> { civ },
            AtlasController.InvalidIslandId);

        var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ);
        state.Layers[LayerState.UnderworldZ] = underworldLayer;
        state.RecalculateVisibleIslandMaps();

        var underworldHex = new HexCoord(0, 0, LayerState.UnderworldZ);
        Assert.Contains(civ.Cities, city => city.Position.Z == LayerState.UnderworldZ);
        Assert.True(state.GetVisibleIslandMapsForZ(0).GetValueOrDefault(0)?.HasTile(a) ?? false);
        Assert.DoesNotContain(state.GetVisibleIslandMapsForZ(0).GetValueOrDefault(0)?.Tiles.Keys ?? Enumerable.Empty<HexCoord>(), coord => coord.Z == LayerState.UnderworldZ);

        var underworldVisibleMap = state.GetVisibleIslandMapsForZ(LayerState.UnderworldZ)[0];
        Assert.True(underworldVisibleMap.HasTile(underworldHex));
        Assert.DoesNotContain(underworldVisibleMap.Tiles.Keys, coord => coord.Z == IslandMap.SurfaceLayer);
    }
}
