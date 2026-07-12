using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class WarFleetControllerTests
{
    private static (WorldState state, Civilization civ, Vertex beaconVertex) IslandWithOwnBeacon()
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

        var beaconVertex = Vertex.Create(h1, h2, h3);
        civ.AddMaritimeBeacon(new MaritimeBeacon(beaconVertex) { CivilizationIndex = 0 });

        return (state, civ, beaconVertex);
    }

    private static WarFleetController Controller(WorldState state)
    {
        var controller = new WarFleetController();
        controller.Initialize(state);
        return controller;
    }

    private static void GrantImperialPort(Civilization civ) => civ.RegisterUniqueBuildingInCache(new ImperialPort());

    [Fact]
    public void IsWarFleetUnlocked_FalseWithoutImperialPort()
    {
        var (_, civ, _) = IslandWithOwnBeacon();
        Assert.False(new WarFleetController().IsWarFleetUnlocked(civ));
    }

    [Fact]
    public void IsWarFleetUnlocked_TrueWithImperialPort()
    {
        var (_, civ, _) = IslandWithOwnBeacon();
        GrantImperialPort(civ);
        Assert.True(new WarFleetController().IsWarFleetUnlocked(civ));
    }

    [Fact]
    public void GetPotentialVertices_IncludesOwnBeaconVertex_RegardlessOfImperialPort()
    {
        var (state, _, beaconVertex) = IslandWithOwnBeacon();
        var vertices = Controller(state).GetPotentialVertices(0);
        Assert.Contains(vertices, v => v.Equals(beaconVertex));
    }

    [Fact]
    public void GetPotentialVertices_ExcludesVertexAlreadyOccupiedByCity()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        civ.AddCity(new City(beaconVertex) { CivilizationIndex = 0 });
        var vertices = Controller(state).GetPotentialVertices(0);
        Assert.DoesNotContain(vertices, v => v.Equals(beaconVertex));
    }

    [Fact]
    public void GetPotentialVertices_ExcludesVertexAlreadyOccupiedByFleet()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        civ.AddFleet(new WarFleet(beaconVertex) { CivilizationIndex = 0 });
        var vertices = Controller(state).GetPotentialVertices(0);
        Assert.DoesNotContain(vertices, v => v.Equals(beaconVertex));
    }

    [Fact]
    public void GetBuildableVertices_EmptyWithoutImperialPort()
    {
        var (state, _, _) = IslandWithOwnBeacon();
        Assert.Empty(Controller(state).GetBuildableVertices(0));
    }

    [Fact]
    public void GetBuildableVertices_IncludesBeaconVertex_WithImperialPort()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        GrantImperialPort(civ);
        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.Contains(vertices, v => v.Equals(beaconVertex));
    }

    [Fact]
    public void BuildWarFleet_WithoutImperialPort_ReturnsNull()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();

        var fleet = Controller(state).BuildWarFleet(0, beaconVertex);
        Assert.Null(fleet);
        Assert.Empty(civ.Fleets);
    }

    [Fact]
    public void BuildWarFleet_InsufficientResources_ReturnsNull()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        GrantImperialPort(civ);

        var fleet = Controller(state).BuildWarFleet(0, beaconVertex);
        Assert.Null(fleet);
        Assert.Empty(civ.Fleets);
    }

    [Fact]
    public void BuildWarFleet_VertexNotPotential_Throws()
    {
        var (state, civ, _) = IslandWithOwnBeacon();
        GrantImperialPort(civ);

        var elsewhere = Vertex.Create(
            new HexCoord(10, 10, IslandMap.SurfaceLayer),
            new HexCoord(11, 10, IslandMap.SurfaceLayer),
            new HexCoord(10, 11, IslandMap.SurfaceLayer));

        Assert.Throws<System.InvalidOperationException>(() => Controller(state).BuildWarFleet(0, elsewhere));
    }

    [Fact]
    public void BuildWarFleet_PaysCostAndAddsFleetWithFixedStats()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        GrantImperialPort(civ);
        // Storage capacity is 0 with no city yet — bump it directly so AddResource isn't clamped
        // to 0 before BuildWarFleet gets a chance to pay for the fleet.
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Wood, 200);
        civ.AddResource(Resource.Ore, 100);
        civ.AddResource(Resource.Food, 200);
        civ.AddResource(Resource.Gold, 200);

        var fleet = Controller(state).BuildWarFleet(0, beaconVertex);

        Assert.NotNull(fleet);
        Assert.Contains(civ.Fleets, f => f == fleet);
        Assert.DoesNotContain(civ.Cities, c => c.Position.Equals(beaconVertex));
        Assert.Equal(20, fleet!.MaxSoldiers);
        Assert.Equal(20, fleet.MaxDefense);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Ore));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Food));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
    }

    [Fact]
    public void DestroyFleet_RemovesFleetFromCivilization()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        GrantImperialPort(civ);
        civ.SetStorageCapacityCache(1000, 1000);
        civ.AddResource(Resource.Wood, 200);
        civ.AddResource(Resource.Ore, 100);
        civ.AddResource(Resource.Food, 200);
        civ.AddResource(Resource.Gold, 200);
        var controller = Controller(state);
        var fleet = controller.BuildWarFleet(0, beaconVertex);

        controller.DestroyFleet(fleet!);

        Assert.DoesNotContain(civ.Fleets, f => f == fleet);
    }

    [Fact]
    public void GetBuildCost_ReturnsFixedValues()
    {
        var cost = WarFleetController.GetBuildCost();
        Assert.Equal(200, cost[Resource.Wood]);
        Assert.Equal(100, cost[Resource.Ore]);
        Assert.Equal(200, cost[Resource.Food]);
        Assert.Equal(200, cost[Resource.Gold]);
    }
}
