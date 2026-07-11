using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class MaritimeBeaconControllerTests
{
    private static (WorldState state, Civilization civ, Vertex waterVertex) WaterTriangleIsland(int observatoryLevel)
    {
        var h1 = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var h2 = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var h3 = new HexCoord(0, 1, IslandMap.SurfaceLayer);

        var map = new IslandMap(new HexTile[]
        {
            new(h1, TerrainType.Water),
            new(h2, TerrainType.Water),
            new(h3, TerrainType.Water),
        });

        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        if (observatoryLevel > 0)
            state.Features.Add(new Observatory(h1) { Level = observatoryLevel });

        var vertex = Vertex.Create(h1, h2, h3);
        return (state, civ, vertex);
    }

    private static MaritimeBeaconController Controller(WorldState state)
    {
        var controller = new MaritimeBeaconController();
        controller.Initialize(state);
        return controller;
    }

    [Fact]
    public void AreMaritimeBeaconsUnlocked_FalseWithoutObservatory()
    {
        var (state, _, _) = WaterTriangleIsland(observatoryLevel: 0);
        Assert.False(Controller(state).AreMaritimeBeaconsUnlocked());
    }

    [Fact]
    public void AreMaritimeBeaconsUnlocked_FalseAtObservatoryLevel1()
    {
        var (state, _, _) = WaterTriangleIsland(observatoryLevel: 1);
        Assert.False(Controller(state).AreMaritimeBeaconsUnlocked());
    }

    [Fact]
    public void AreMaritimeBeaconsUnlocked_TrueAtObservatoryLevel2()
    {
        var (state, _, _) = WaterTriangleIsland(observatoryLevel: 2);
        Assert.True(Controller(state).AreMaritimeBeaconsUnlocked());
    }

    [Fact]
    public void GetBuildableVertices_EmptyWithoutObservatoryLevel2()
    {
        var (state, _, _) = WaterTriangleIsland(observatoryLevel: 1);
        Assert.Empty(Controller(state).GetBuildableVertices(0));
    }

    [Fact]
    public void GetBuildableVertices_IncludesAllWaterVertex()
    {
        var (state, _, vertex) = WaterTriangleIsland(observatoryLevel: 2);
        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.Contains(vertices, v => v.Equals(vertex));
    }

    [Fact]
    public void GetBuildableVertices_ExcludesVertexAlreadyOccupiedByCity()
    {
        var (state, civ, vertex) = WaterTriangleIsland(observatoryLevel: 2);
        civ.AddCity(new City(vertex) { CivilizationIndex = 0 });
        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.DoesNotContain(vertices, v => v.Equals(vertex));
    }

    [Fact]
    public void GetBuildableVertices_ExcludesVertexAlreadyOccupiedByAnotherCivilizationsBeacon()
    {
        var (state, civ, vertex) = WaterTriangleIsland(observatoryLevel: 2);
        var otherCiv = new Civilization { Index = 1 };
        state.Civilizations.Add(otherCiv);
        otherCiv.AddMaritimeBeacon(new MaritimeBeacon(vertex) { CivilizationIndex = 1 });

        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.DoesNotContain(vertices, v => v.Equals(vertex));
    }

    [Fact]
    public void BuildMaritimeBeacon_PaysCostAndAddsBeacon()
    {
        var (state, civ, vertex) = WaterTriangleIsland(observatoryLevel: 2);

        // Storage capacity is 0 with no city — give the civ a Warehouse so it can actually hold
        // the Glass (an advanced-storage resource) needed to pay the beacon's cost.
        var storageCity = new City(Vertex.Create(
            new HexCoord(10, 10, IslandMap.SurfaceLayer),
            new HexCoord(11, 10, IslandMap.SurfaceLayer),
            new HexCoord(10, 11, IslandMap.SurfaceLayer))) { CivilizationIndex = 0 };
        storageCity.Buildings.Add(new Warehouse { Level = 1 });
        civ.AddCity(storageCity);

        civ.AddResource(Resource.Glass, 10);
        civ.AddResource(Resource.Wood, 10);

        var beacon = Controller(state).BuildMaritimeBeacon(0, vertex);

        Assert.NotNull(beacon);
        Assert.Contains(civ.MaritimeBeacons, b => b.Position.Equals(vertex));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Glass));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
    }

    [Fact]
    public void BuildMaritimeBeacon_InsufficientResources_ReturnsNull()
    {
        var (state, civ, vertex) = WaterTriangleIsland(observatoryLevel: 2);

        var beacon = Controller(state).BuildMaritimeBeacon(0, vertex);

        Assert.Null(beacon);
        Assert.Empty(civ.MaritimeBeacons);
    }

    [Fact]
    public void BuildMaritimeBeacon_VertexNotBuildable_Throws()
    {
        var (state, civ, _) = WaterTriangleIsland(observatoryLevel: 0);
        civ.AddResource(Resource.Glass, 10);
        civ.AddResource(Resource.Wood, 10);

        var h1 = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var h2 = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var h3 = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var vertex = Vertex.Create(h1, h2, h3);

        Assert.Throws<System.InvalidOperationException>(() => Controller(state).BuildMaritimeBeacon(0, vertex));
    }

    [Fact]
    public void GetBuildCost_ReturnsFixedValues()
    {
        var cost = MaritimeBeaconController.GetBuildCost();
        Assert.Equal(10, cost[Resource.Glass]);
        Assert.Equal(10, cost[Resource.Wood]);
    }
}
