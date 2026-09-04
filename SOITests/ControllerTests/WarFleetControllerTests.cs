using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class WarFleetControllerTests
{
    private static (WorldState state, Civilization civ, Vertex beaconVertex) IslandWithOwnBeacon(int greatLighthouseLevel = 0)
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
        if (greatLighthouseLevel > 0)
            state.AddFeature(new GreatLighthouse(h1) { Level = greatLighthouseLevel });

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

    [Fact]
    public void IsWarFleetUnlocked_FalseWithoutGreatLighthouse()
    {
        var (state, _, _) = IslandWithOwnBeacon();
        Assert.False(Controller(state).IsWarFleetUnlocked());
    }

    [Fact]
    public void IsWarFleetUnlocked_FalseAtGreatLighthouseLevel2()
    {
        var (state, _, _) = IslandWithOwnBeacon(greatLighthouseLevel: 2);
        Assert.False(Controller(state).IsWarFleetUnlocked());
    }

    [Fact]
    public void IsWarFleetUnlocked_TrueAtGreatLighthouseLevel3()
    {
        var (state, _, _) = IslandWithOwnBeacon(greatLighthouseLevel: 3);
        Assert.True(Controller(state).IsWarFleetUnlocked());
    }

    [Fact]
    public void GetPotentialVertices_IncludesOwnBeaconVertex_RegardlessOfGreatLighthouse()
    {
        var (state, _, beaconVertex) = IslandWithOwnBeacon();
        var vertices = Controller(state).GetPotentialVertices(0);
        Assert.Contains(vertices, v => v.Equals(beaconVertex));
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
    public void GetBuildableVertices_EmptyWithoutGreatLighthouseLevel3()
    {
        var (state, _, _) = IslandWithOwnBeacon(greatLighthouseLevel: 2);
        Assert.Empty(Controller(state).GetBuildableVertices(0));
    }

    [Fact]
    public void GetBuildableVertices_IncludesBeaconVertex_AtGreatLighthouseLevel3()
    {
        var (state, _, beaconVertex) = IslandWithOwnBeacon(greatLighthouseLevel: 3);
        var vertices = Controller(state).GetBuildableVertices(0);
        Assert.Contains(vertices, v => v.Equals(beaconVertex));
    }

    [Fact]
    public void BuildWarFleet_WithoutGreatLighthouseLevel3_ReturnsNull()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();

        var fleet = Controller(state).BuildWarFleet(0, beaconVertex);
        Assert.Null(fleet);
        Assert.Empty(civ.Fleets);
    }

    [Fact]
    public void BuildWarFleet_InsufficientResources_ReturnsNull()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon(greatLighthouseLevel: 3);

        var fleet = Controller(state).BuildWarFleet(0, beaconVertex);
        Assert.Null(fleet);
        Assert.Empty(civ.Fleets);
    }

    [Fact]
    public void BuildWarFleet_VertexNotPotential_Throws()
    {
        var (state, _, _) = IslandWithOwnBeacon(greatLighthouseLevel: 3);

        var elsewhere = Vertex.Create(
            new HexCoord(10, 10, IslandMap.SurfaceLayer),
            new HexCoord(11, 10, IslandMap.SurfaceLayer),
            new HexCoord(10, 11, IslandMap.SurfaceLayer));

        Assert.Throws<System.InvalidOperationException>(() => Controller(state).BuildWarFleet(0, elsewhere));
    }

    [Fact]
    public void BuildWarFleet_PaysCostAndAddsFleetWithFixedStats()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon(greatLighthouseLevel: 3);
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
        var (state, civ, beaconVertex) = IslandWithOwnBeacon(greatLighthouseLevel: 3);
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
    public void DestroyFleetsInvalidatedByTerrain_RemovesFleet_WhenAHexIsNoLongerWater()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        civ.AddFleet(new WarFleet(beaconVertex) { CivilizationIndex = 0 });

        state.GetMapFor(beaconVertex)!.GetTile(beaconVertex.GetHexes()[0])!.TerrainType = TerrainType.Plain;
        var controller = Controller(state);

        var destroyed = controller.DestroyFleetsInvalidatedByTerrain();

        Assert.Single(destroyed);
        Assert.Empty(civ.Fleets);
    }

    [Fact]
    public void DestroyFleetsInvalidatedByTerrain_KeepsFleet_WhenStillFullyWater()
    {
        var (state, civ, beaconVertex) = IslandWithOwnBeacon();
        civ.AddFleet(new WarFleet(beaconVertex) { CivilizationIndex = 0 });
        var controller = Controller(state);

        var destroyed = controller.DestroyFleetsInvalidatedByTerrain();

        Assert.Empty(destroyed);
        Assert.Single(civ.Fleets);
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
