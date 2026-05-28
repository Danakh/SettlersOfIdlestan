using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

public class RoadControllerTests
{
    [Fact]
    public void GetBuildableRoads_CityEnablesAdjacentEdges()
    {
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

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
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

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
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);

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
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);
        var d = new HexCoord(1, 1);

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

    [Fact]
    public void BuildRoad_OverEnemyRoad_DestroysEnemyRoad()
    {
        var a = new HexCoord(0, 0);
        var b = new HexCoord(1, 0);
        var c = new HexCoord(0, 1);
        var d = new HexCoord(1, 1);

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
