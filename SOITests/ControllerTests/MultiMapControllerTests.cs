using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class MultiMapControllerTests
{
    [Fact]
    public void RoadController_BuildRoad_CanBuildOnUnderworldMap()
    {
        var (state, civ) = CreateStateWithUnderworldOutpost();
        var a = new HexCoord(0, 0, LayerState.UnderworldZ);
        var b = new HexCoord(1, 0, LayerState.UnderworldZ);
        var edge = Edge.Create(a, b);

        civ.AddResource(Resource.Wood, 2);
        civ.AddResource(Resource.Brick, 2);

        var controller = new RoadController(state);
        var road = controller.BuildRoad(civ.Index, edge);

        Assert.NotNull(road);
        Assert.Equal(LayerState.UnderworldZ, road.Position.Z);
        Assert.Contains(civ.Roads, r => r.Position.Equals(edge));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
    }

    [Fact]
    public void HarvestController_AutomaticHarvest_OnUnderworldCityAddsToSharedCivilizationStock()
    {
        var (state, civ) = CreateStateWithUnderworldOutpost();
        var underworldCity = civ.Cities.Single(city => city.Position.Z == LayerState.UnderworldZ);
        underworldCity.Buildings.Add(new Quarry());

        var clock = new GameClock();
        clock.Start();
        _ = new HarvestController(state, clock);

        clock.SimulateAdvance(10);

        // 3 stones because there are 3 mountains around the city
        Assert.Equal(3, civ.GetResourceQuantity(Resource.Stone));
    }

    [Fact]
    public void MilitaryController_FindNearbyEnemyCity_IgnoresCitiesOnDifferentLayers()
    {
        var (state, playerCiv) = CreateStateWithUnderworldOutpost();
        var enemyCiv = new Civilization { Index = 1 };
        var enemyVertex = Vertex.Create(
            new HexCoord(0, 0, IslandMap.SurfaceLayer),
            new HexCoord(1, 0, IslandMap.SurfaceLayer),
            new HexCoord(0, 1, IslandMap.SurfaceLayer));
        enemyCiv.AddCity(new City(enemyVertex) { CivilizationIndex = enemyCiv.Index });
        state.Civilizations.Add(enemyCiv);
        state.RecalculateVisibleIslandMaps();

        var underworldCity = playerCiv.Cities.Single(city => city.Position.Z == LayerState.UnderworldZ);
        underworldCity.Soldiers = 1;

        var controller = new MilitaryController();
        controller.Initialize(state, new GameClock());

        Assert.Null(controller.FindNearbyEnemyCity(underworldCity, playerCiv));
    }

    private static (WorldState State, Civilization Civilization) CreateStateWithUnderworldOutpost()
    {
        var surfaceA = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var surfaceB = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var surfaceC = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var civ = new Civilization { Index = 0 };
        civ.AddCity(new City(Vertex.Create(surfaceA, surfaceB, surfaceC)) { CivilizationIndex = 0 });

        var state = new WorldState(
            new IslandMap([
                new HexTile(surfaceA, TerrainType.Plain),
                new HexTile(surfaceB, TerrainType.Plain),
                new HexTile(surfaceC, TerrainType.Plain),
            ]),
            new List<Civilization> { civ },
            AtlasController.InvalidIslandId);

        state.Layers[LayerState.UnderworldZ] = LayerState.CreateUnderworld(civ.Index);
        state.RecalculateVisibleIslandMaps();
        return (state, civ);
    }
}
