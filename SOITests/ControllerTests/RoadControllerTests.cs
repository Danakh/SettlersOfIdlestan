using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests;

public class RoadControllerTests
{
    [Fact]
    public void GetBuildableRoads_CityEnablesAdjacentEdges()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
            new HexTile(b, TerrainType.Plain),
            new HexTile(c, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(a, b, c);
        IslandMapGenerator generator = new IslandMapGenerator();
        generator.PopulatePlayerCivilization(map, civ, vertex);

        var controller = new RoadController(state);
        var buildable = controller.GetBuildableRoads(0);

        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(a, b)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(a, c)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(b, c)));
    }

    [Fact]
    public void GetBuildableRoads_OccupiedEdgeNotReturned()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
            new HexTile(b, TerrainType.Plain),
            new HexTile(c, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(a, b, c);
        IslandMapGenerator generator = new IslandMapGenerator();
        generator.PopulatePlayerCivilization(map, civ, vertex);

        // Occupy edge a-b
        civ.Roads.Add(new Road(Edge.Create(a, b)) { CivilizationIndex = 0 });

        var controller = new RoadController(state);
        var buildable = controller.GetBuildableRoads(0);

        Assert.DoesNotContain(buildable, r => r.Position.Equals(Edge.Create(a, b)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(a, c)));
        Assert.Contains(buildable, r => r.Position.Equals(Edge.Create(b, c)));
    }

    [Fact]
    public void BuildRoad_AdjacentToCity_ConsumesResourcesAndSetsDistance()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
            new HexTile(b, TerrainType.Plain),
            new HexTile(c, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };

        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(a, b, c);
        IslandMapGenerator generator = new IslandMapGenerator();
        generator.PopulatePlayerCivilization(map, civ, vertex);

        // give enough resources: 2 wood and 2 brick
        civ.AddResource(Resource.Wood, 2);
        civ.AddResource(Resource.Brick, 2);

        var controller = new RoadController(state);
        var edge = Edge.Create(a, b);
        var road = controller.BuildRoad(0, edge)!;

        Assert.Contains(civ.Roads, r => r.Position.Equals(edge));
        Assert.Equal(1, road.DistanceToNearestCity);
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
    }

    [Fact]
    public void BuildRoad_ExtendRoad_CalculatesDistanceAndConsumesCost()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var d = new HexCoord(1, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
            new HexTile(b, TerrainType.Plain),
            new HexTile(c, TerrainType.Plain),
            new HexTile(d, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };

        var civs = new List<Civilization> { civ };
        var state = new IslandState(map, civs, AtlasController.InvalidIslandId);

        var vertex = Vertex.Create(a, b, c);
        IslandMapGenerator generator = new IslandMapGenerator();
        generator.PopulatePlayerCivilization(map, civ, vertex);

        var controller = new RoadController(state);
        var e1 = Edge.Create(b, c);
        var e2 = Edge.Create(b, d);

        // give enough resources for two roads: first costs 2 each, second costs 5 each => total 7 each
        civ.AddResource(Resource.Wood, 7);
        civ.AddResource(Resource.Brick, 7);

        var r1 = controller.BuildRoad(0, e1)!;
        Assert.Contains(civ.Roads, r => r.Position.Equals(e1));
        Assert.Equal(1, r1.DistanceToNearestCity);

        var r2 = controller.BuildRoad(0, e2)!;
        Assert.Contains(civ.Roads, r => r.Position.Equals(e2));
        Assert.Equal(2, r2.DistanceToNearestCity);

        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
    }

    // ─── Maritime Routes ─────────────────────────────────────────────────────
    //
    // Island layout used by maritime tests:
    //   (0,0)=Plain  (1,0)=Water  (0,1)=Water  [(1,1)=Plain if coastal]
    //   City at Vertex((0,0),(1,0),(0,1))
    //   Maritime edge = Edge.Create((1,0),(0,1))
    //     Vertex 1: (0,0),(1,0),(0,1) → (0,0)=Plain → touches land ✓
    //     Vertex 2: (1,0),(0,1),(1,1) → needs (1,1)=Plain to touch land
    //   DeepWaterIsland omits (1,1) → vertex 2 touches only water/absent → invalid maritime

    private static (IslandState state, Civilization civ) CoastalIsland()
    {
        var land  = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var w1    = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var w2    = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var land2 = new HexCoord(1, 1, IslandMap.SurfaceLayer);
        var map   = new IslandMap(new HexTile[]
        {
            new(land,  TerrainType.Plain),
            new(w1,    TerrainType.Water),
            new(w2,    TerrainType.Water),
            new(land2, TerrainType.Plain),
        });
        var civ  = new Civilization { Index = 0 };
        civ.Cities.Add(new City(Vertex.Create(land, w1, w2)) { CivilizationIndex = 0 });
        var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        return (state, civ);
    }

    private static (IslandState state, Civilization civ) DeepWaterIsland()
    {
        var land = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var w1   = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var w2   = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var map  = new IslandMap(new HexTile[]
        {
            new(land, TerrainType.Plain),
            new(w1,   TerrainType.Water),
            new(w2,   TerrainType.Water),
        });
        var civ  = new Civilization { Index = 0 };
        civ.Cities.Add(new City(Vertex.Create(land, w1, w2)) { CivilizationIndex = 0 });
        var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        return (state, civ);
    }

    private static Edge MaritimeEdge() => Edge.Create(new HexCoord(1, 0, IslandMap.SurfaceLayer), new HexCoord(0, 1, IslandMap.SurfaceLayer));

    private static void EnableMaritimeRoutes(Civilization civ)
    {
        var prestige = new PrestigeState();
        prestige.PurchasedVertices.Add(PrestigeMap.MaritimeRoutesVertex);
        civ.SetupModifierAggregator(
            civ.TechnologyTree,
            new PrestigeModifierProvider(prestige, PrestigeMapController.DefaultMap));
    }

    [Fact]
    public void MaritimeRoutes_CoastalWaterEdge_ExcludedWithoutModifier()
    {
        var (state, _) = CoastalIsland();
        var roads = new RoadController(state).GetBuildableRoads(0);
        Assert.DoesNotContain(roads, r => r.Position.Equals(MaritimeEdge()));
    }

    [Fact]
    public void MaritimeRoutes_CoastalWaterEdge_IncludedWithModifier()
    {
        var (state, civ) = CoastalIsland();
        EnableMaritimeRoutes(civ);
        var roads = new RoadController(state).GetBuildableRoads(0);
        Assert.Contains(roads, r => r.Position.Equals(MaritimeEdge()));
    }

    [Fact]
    public void MaritimeRoutes_DeepWaterEdge_ExcludedEvenWithModifier()
    {
        var (state, civ) = DeepWaterIsland();
        EnableMaritimeRoutes(civ);
        var roads = new RoadController(state).GetBuildableRoads(0);
        Assert.DoesNotContain(roads, r => r.Position.Equals(MaritimeEdge()));
    }

    [Fact]
    public void MaritimeRoutes_BuildRoad_ConsumesFixedCostAndCreatesRoad()
    {
        var (state, civ) = CoastalIsland();
        civ.Cities[0].Buildings.Add(new Warehouse());
        EnableMaritimeRoutes(civ);
        civ.AddResource(Resource.Wood,  10);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Gold,   5);

        var road = new RoadController(state).BuildRoad(0, MaritimeEdge())!;

        Assert.NotNull(road);
        Assert.Contains(civ.Roads, r => r.Position.Equals(MaritimeEdge()));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
        Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
    }

    [Fact]
    public void MaritimeRoutes_BuildRoad_InsufficientResources_ReturnsNull()
    {
        var (state, civ) = CoastalIsland();
        EnableMaritimeRoutes(civ);
        civ.AddResource(Resource.Wood,  9); // 1 wood short
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Gold,   5);

        var road = new RoadController(state).BuildRoad(0, MaritimeEdge());
        Assert.Null(road);
        Assert.Empty(civ.Roads);
    }

    [Fact]
    public void MaritimeRoutes_BuildRoad_WaterEdgeWithoutModifier_Throws()
    {
        var (state, _) = CoastalIsland();
        Assert.Throws<InvalidOperationException>(() =>
            new RoadController(state).BuildRoad(0, MaritimeEdge()));
    }

    [Fact]
    public void MaritimeRoutes_BuildRoad_DeepWaterEdge_Throws()
    {
        var (state, civ) = DeepWaterIsland();
        EnableMaritimeRoutes(civ);
        civ.AddResource(Resource.Wood,  10);
        civ.AddResource(Resource.Brick, 10);
        civ.AddResource(Resource.Gold,   5);
        Assert.Throws<InvalidOperationException>(() =>
            new RoadController(state).BuildRoad(0, MaritimeEdge()));
    }

    [Fact]
    public void MaritimeRoutes_GetMaritimeRoadCost_ReturnsFixedValues()
    {
        var cost = RoadController.GetMaritimeRoadCost();
        Assert.Equal(10, cost[Resource.Wood]);
        Assert.Equal(10, cost[Resource.Brick]);
        Assert.Equal(5,  cost[Resource.Gold]);
    }

    [Fact]
    public void MaritimeRoutes_GetPlayerRoadCost_ForWaterEdge_ReturnsMaritimeCost()
    {
        var (state, _) = CoastalIsland();
        var cost = new RoadController(state).GetPlayerRoadCost(MaritimeEdge());
        Assert.Equal(10, cost[Resource.Wood]);
        Assert.Equal(10, cost[Resource.Brick]);
        Assert.Equal(5,  cost[Resource.Gold]);
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildRoad_OverEnemyRoad_DestroysEnemyRoad()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);
        var d = new HexCoord(1, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
            new HexTile(b, TerrainType.Plain),
            new HexTile(c, TerrainType.Plain),
            new HexTile(d, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var playerCiv = new Civilization { Index = 0 };
        var enemyCiv = new Civilization { Index = 1 };
        var civs = new List<Civilization> { playerCiv, enemyCiv };
        var state = new IslandState(map, civs, AtlasController.InvalidIslandId);

        // Joueur : ville au vertex a-b-c
        var playerVertex = Vertex.Create(a, b, c);
        var generator = new IslandMapGenerator();
        generator.PopulatePlayerCivilization(map, playerCiv, playerVertex);

        // Ennemi : route déjà construite sur l'arête b-c (adjacente à la ville du joueur)
        var contestedEdge = Edge.Create(b, c);
        enemyCiv.Roads.Add(new Road(contestedEdge) { CivilizationIndex = 1, DistanceToNearestCity = 1 });

        // Distance 1 => coût 2 bois + 2 briques
        playerCiv.AddResource(Resource.Wood, 2);
        playerCiv.AddResource(Resource.Brick, 2);

        var controller = new RoadController(state);

        // L'arête ennemie doit apparaître comme constructible pour le joueur
        var buildable = controller.GetBuildableRoads(0);
        Assert.Contains(buildable, r => r.Position.Equals(contestedEdge));

        // Construction sur la route ennemie
        var road = controller.BuildRoad(0, contestedEdge)!;

        // La route ennemie est détruite
        Assert.DoesNotContain(enemyCiv.Roads, r => r.Position.Equals(contestedEdge));
        // La route du joueur est créée
        Assert.Contains(playerCiv.Roads, r => r.Position.Equals(contestedEdge));
        Assert.Equal(0, road.CivilizationIndex);
        Assert.Equal(1, road.DistanceToNearestCity);
    }
}
