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
        var underworld = UnderworldState.CreateDefault(playerCivIndex: 0);

        Assert.Equal(UnderworldState.Layer, underworld.Map.Z);
        Assert.All(underworld.Map.Tiles.Keys, coord => Assert.Equal(UnderworldState.Layer, coord.Z));
        Assert.Single(underworld.Cities);
        Assert.Equal(UnderworldState.Layer, underworld.Cities[0].Position.Z);
    }

    [Fact]
    public void IslandMap_RejectsMixedLayers()
    {
        var surface = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var underworld = new HexCoord(1, 0, UnderworldState.Layer);

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

        Assert.Throws<ArgumentException>(() => map.GetTile(new HexCoord(0, 0, UnderworldState.Layer)));
    }

    [Fact]
    public void RecalculateVisibleIslandMaps_BuildsVisibleMapForEachLayer()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var civ = new Civilization { Index = 0 };
        civ.Cities.Add(new City(Vertex.Create(a, b, c)) { CivilizationIndex = 0 });

        var state = new IslandState(
            new IslandMap([
                new HexTile(a, TerrainType.Plain),
                new HexTile(b, TerrainType.Plain),
                new HexTile(c, TerrainType.Plain),
            ]),
            new List<Civilization> { civ },
            AtlasController.InvalidIslandId);

        state.Underworld = UnderworldState.CreateDefault(civ.Index);
        state.RecalculateVisibleIslandMaps();

        var underworldHex = new HexCoord(0, 0, UnderworldState.Layer);
        Assert.Contains(civ.Cities, city => city.Position.Z == UnderworldState.Layer);
        Assert.True(state.GetVisibleIslandMapsForZ(0).GetValueOrDefault(0)?.HasTile(a) ?? false);
        Assert.DoesNotContain(state.GetVisibleIslandMapsForZ(0).GetValueOrDefault(0)?.Tiles.Keys ?? Enumerable.Empty<HexCoord>(), coord => coord.Z == UnderworldState.Layer);

        var underworldVisibleMap = state.GetVisibleIslandMapsForZ(UnderworldState.Layer)[0];
        Assert.True(underworldVisibleMap.HasTile(underworldHex));
        Assert.DoesNotContain(underworldVisibleMap.Tiles.Keys, coord => coord.Z == IslandMap.SurfaceLayer);
    }
}
