using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.MilitaryTests;

/// <summary>
/// Arbre-Cœur : relie par la Forêt deux villes du joueur toutes deux adjacentes à une case Forêt et
/// sur le même plan, ignorant la portée normale de renfort (REINFORCEMENT_RANGE) tant qu'une route
/// les relie (voir ReinforcementEngine.HasUnlimitedRangeReinforcementLink).
///
/// Géométrie (civ 0) — chaîne de 7 vertex, 6 segments de route (> REINFORCEMENT_RANGE par défaut = 5) :
///   Source — Vertex(0,0 / 0,1 / 1,0), hex (0,0) = Forêt
///   V1..V5 — relais intermédiaires
///   Target — Vertex(3,0 / 3,1 / 4,0), hex (4,0) = Forêt
/// </summary>
public class HeartTreeForestReinforcementTests
{
    private static readonly Vertex VSource = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex V1       = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));
    private static readonly Vertex V2       = Vertex.Create(new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer), new(2, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex V3       = Vertex.Create(new(1, 1, IslandMap.SurfaceLayer), new(2, 0, IslandMap.SurfaceLayer), new(2, 1, IslandMap.SurfaceLayer));
    private static readonly Vertex V4       = Vertex.Create(new(2, 0, IslandMap.SurfaceLayer), new(2, 1, IslandMap.SurfaceLayer), new(3, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex V5       = Vertex.Create(new(2, 1, IslandMap.SurfaceLayer), new(3, 0, IslandMap.SurfaceLayer), new(3, 1, IslandMap.SurfaceLayer));
    private static readonly Vertex VTarget  = Vertex.Create(new(3, 0, IslandMap.SurfaceLayer), new(3, 1, IslandMap.SurfaceLayer), new(4, 0, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap(bool sourceForest, bool targetForest) => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), sourceForest ? TerrainType.Forest : TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(2, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(2, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(3, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(3, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(4, 0, IslandMap.SurfaceLayer), targetForest ? TerrainType.Forest : TerrainType.Plain),
    ]);

    /// <summary>Chaîne de 6 routes reliant Source à Target en passant par V1..V5 (6 segments).</summary>
    private static void AddRoadChain(Civilization civ)
    {
        civ.AddRoad(new Road(Edge.Create(new HexCoord(0, 1, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer))) { CivilizationIndex = 0, DistanceToNearestCity = 1 });
        civ.AddRoad(new Road(Edge.Create(new HexCoord(1, 0, IslandMap.SurfaceLayer), new HexCoord(1, 1, IslandMap.SurfaceLayer))) { CivilizationIndex = 0, DistanceToNearestCity = 2 });
        civ.AddRoad(new Road(Edge.Create(new HexCoord(1, 1, IslandMap.SurfaceLayer), new HexCoord(2, 0, IslandMap.SurfaceLayer))) { CivilizationIndex = 0, DistanceToNearestCity = 3 });
        civ.AddRoad(new Road(Edge.Create(new HexCoord(2, 0, IslandMap.SurfaceLayer), new HexCoord(2, 1, IslandMap.SurfaceLayer))) { CivilizationIndex = 0, DistanceToNearestCity = 4 });
        civ.AddRoad(new Road(Edge.Create(new HexCoord(2, 1, IslandMap.SurfaceLayer), new HexCoord(3, 0, IslandMap.SurfaceLayer))) { CivilizationIndex = 0, DistanceToNearestCity = 5 });
        civ.AddRoad(new Road(Edge.Create(new HexCoord(3, 0, IslandMap.SurfaceLayer), new HexCoord(3, 1, IslandMap.SurfaceLayer))) { CivilizationIndex = 0, DistanceToNearestCity = 6 });
    }

    private static (GameClock clock, MilitaryController ctrl, City source, City target) Setup(
        bool withHeartTree, bool sourceForest, bool targetForest, bool withRoad = true)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999;

        var source = new City(VSource) { CivilizationIndex = 0, Soldiers = 5 };
        source.AddBuilding(new Barracks { Level = 2 });
        if (withHeartTree)
            source.AddBuilding(new HeartTree { Level = 1 });

        var target = new City(VTarget) { CivilizationIndex = 0, Soldiers = 0 };
        target.AddBuilding(new Barracks { Level = 1 });

        civ.AddCity(source);
        civ.AddCity(target);

        if (withRoad)
            AddRoadChain(civ);

        source.FlowTarget = VTarget;

        var state = new WorldState(BuildMap(sourceForest, targetForest), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (clock, ctrl, source, target);
    }

    [Fact]
    public void Reinforcement_BlockedBeyondRange_WithoutHeartTree()
    {
        // 6 segments > REINFORCEMENT_RANGE (5) par défaut, pas d'Arbre-Cœur : bloqué.
        var (clock, _, source, target) = Setup(withHeartTree: false, sourceForest: true, targetForest: true);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    [Fact]
    public void Reinforcement_AllowedBeyondRange_WithHeartTree_WhenBothCitiesForestAdjacent()
    {
        var (clock, _, source, target) = Setup(withHeartTree: true, sourceForest: true, targetForest: true);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        Assert.Equal(4, source.Soldiers);
        Assert.Single(target.IncomingSoldiers);

        // Le trajet (6 segments = 120 ticks) dépasse l'intervalle entre deux expéditions (100 ticks) :
        // un second soldat est expédié avant l'arrivée du premier. On ne vérifie donc que l'arrivée du
        // premier, preuve suffisante que le lien forestier a bien laissé passer le renfort malgré la
        // distance hors de portée normale.
        clock.SimulateAdvance(6 * MilitaryController.ReinforcementTicksPerRoadSegment);
        Assert.Equal(1, target.Soldiers);
    }

    [Fact]
    public void Reinforcement_StillBlockedBeyondRange_WithHeartTree_WhenOnlyOneCityForestAdjacent()
    {
        var (clock, _, source, target) = Setup(withHeartTree: true, sourceForest: true, targetForest: false);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    [Fact]
    public void Reinforcement_StillBlocked_WithHeartTree_WhenNoRoadConnectsForestCities()
    {
        // Le lien forestier ignore la portée, pas le réseau routier : sans route, toujours bloqué.
        var (clock, _, source, target) = Setup(withHeartTree: true, sourceForest: true, targetForest: true, withRoad: false);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }
}
