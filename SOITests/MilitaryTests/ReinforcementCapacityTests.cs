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
/// Vérifie que les renforts respectent la capacité maximale de soldats de la cité cible,
/// suivent les routes de la civilisation et arrivent avec le délai de transit attendu.
///
/// Géométrie :
///   Civ 0 (joueur) — Source : Vertex(0,0 / 0,1 / 1,0)
///   Civ 0 (joueur) — Cible  : Vertex(0,1 / 1,0 / 1,1)
///   Route : Edge(0,1 — 1,0) — 1 segment, délai = ReinforcementTicksPerRoadSegment.
/// </summary>
public class ReinforcementCapacityTests
{
    private static readonly Vertex VertexSource = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex VertexTarget = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    /// <summary>
    /// Crée deux villes alliées avec une route entre elles et un flux défini de source → cible.
    /// </summary>
    private static (GameClock clock, MilitaryController ctrl, City source, City target) Setup(
        int sourceSoldiers, int sourceBarracksLevel,
        int targetSoldiers, int targetBarracksLevel)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999;

        var source = new City(VertexSource) { CivilizationIndex = 0, Soldiers = sourceSoldiers };
        source.Buildings.Add(new Barracks { Level = sourceBarracksLevel });

        var target = new City(VertexTarget) { CivilizationIndex = 0, Soldiers = targetSoldiers };
        target.Buildings.Add(new Barracks { Level = targetBarracksLevel });

        civ.AddCity(source);
        civ.AddCity(target);

        // Route reliant les deux villes (1 segment)
        var roadEdge = Edge.Create(new HexCoord(0, 1, IslandMap.SurfaceLayer), new HexCoord(1, 0, IslandMap.SurfaceLayer));
        civ.AddRoad(new Road(roadEdge) { CivilizationIndex = 0, DistanceToNearestCity = 1 });

        source.FlowTarget = VertexTarget;

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (clock, ctrl, source, target);
    }

    // ── Transit intermédiaire ────────────────────────────────────────────────

    [Fact]
    public void Reinforcement_SoldierInTransit_AfterDispatch()
    {
        // Après expédition, le soldat est en transit (slot réservé) mais pas encore en garnison.
        var (clock, _, source, target) = Setup(5, 2, 0, 1);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(4, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Single(target.IncomingSoldiers);
    }

    [Fact]
    public void Reinforcement_SoldierArrives_AfterTransitDelay()
    {
        // Deux avancements séparés : d'abord le dispatch, puis le délai de transit.
        var (clock, _, source, target) = Setup(5, 2, 0, 1);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        clock.SimulateAdvance(MilitaryController.ReinforcementTicksPerRoadSegment);

        Assert.Equal(4, source.Soldiers);
        Assert.Equal(1, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    // ── Event ────────────────────────────────────────────────────────────────

    [Fact]
    public void ReinforcementSent_EventFired_WhenTargetBelowMaxCapacity()
    {
        var (clock, ctrl, _, _) = Setup(5, 2, 0, 1);

        bool fired = false;
        ctrl.ReinforcementSent += (_, _) => fired = true;

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.True(fired);
    }

    // ── Capacité max atteinte ─────────────────────────────────────────────────

    [Fact]
    public void Reinforcement_DoesNotTransfer_WhenTargetAtMaxCapacity()
    {
        // Cible déjà pleine : 5/5 (Barracks niveau 1 → max = 5)
        var (clock, _, source, target) = Setup(5, 2, 5, 1);

        Assert.Equal(5, target.MaxSoldiers);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(5, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    [Fact]
    public void ReinforcementSent_EventNotFired_WhenTargetAtMaxCapacity()
    {
        var (clock, ctrl, _, _) = Setup(5, 2, 5, 1);

        bool fired = false;
        ctrl.ReinforcementSent += (_, _) => fired = true;

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.False(fired);
    }

    // ── Slot réservé bloque les expéditions suivantes ─────────────────────────

    [Fact]
    public void Reinforcement_InTransitCountsAsOccupiedSlot()
    {
        // Cible à 4/5 : un soldat est expédié → réserve le dernier slot (effective = 5 = max).
        // Le soldat arrive après 20 ticks → target = 5/5 → aucune autre expédition.
        var (clock, _, source, target) = Setup(5, 2, 4, 1);

        // Expédition au tick 100 → slot réservé
        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        Assert.Equal(4, source.Soldiers);
        Assert.Equal(4, target.Soldiers);
        Assert.Single(target.IncomingSoldiers);

        // Arrivée au tick 120
        clock.SimulateAdvance(MilitaryController.ReinforcementTicksPerRoadSegment);
        Assert.Equal(4, source.Soldiers);
        Assert.Equal(5, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);

        // Intervalle suivant (tick 220) : cible pleine → aucune nouvelle expédition
        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        Assert.Equal(4, source.Soldiers);
        Assert.Equal(5, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    [Fact]
    public void Reinforcement_StopsExactlyAtMaxCapacity()
    {
        // Cible à 4/5 : un seul soldat peut être transféré, puis la cible est pleine.
        var (clock, _, source, target) = Setup(5, 2, 4, 1);

        // Expédition puis arrivée (deux avancements séparés)
        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        clock.SimulateAdvance(MilitaryController.ReinforcementTicksPerRoadSegment);
        Assert.Equal(4, source.Soldiers);
        Assert.Equal(5, target.Soldiers);

        // Intervalle suivant : cible pleine, aucun transfert
        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        Assert.Equal(4, source.Soldiers);
        Assert.Equal(5, target.Soldiers);
    }

    [Fact]
    public void Reinforcement_NeverExceedsMaxCapacity_AfterManyTicks()
    {
        // Source avec beaucoup de soldats, cible initialement vide (max = 5).
        var (clock, _, _, target) = Setup(20, 4, 0, 1);

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        // Attendre que tous les soldats en transit soient arrivés
        clock.SimulateAdvance(MilitaryController.ReinforcementTicksPerRoadSegment);

        Assert.True(target.Soldiers <= target.MaxSoldiers,
            $"La cible a {target.Soldiers} soldats mais le max est {target.MaxSoldiers}.");
        Assert.Equal(5, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    // ── Sans route : aucun renfort possible ──────────────────────────────────

    [Fact]
    public void Reinforcement_DoesNotTransfer_WhenNoRoadPath()
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999;

        var source = new City(VertexSource) { CivilizationIndex = 0, Soldiers = 5 };
        source.Buildings.Add(new Barracks { Level = 2 });

        var target = new City(VertexTarget) { CivilizationIndex = 0, Soldiers = 0 };
        target.Buildings.Add(new Barracks { Level = 1 });

        civ.AddCity(source);
        civ.AddCity(target);
        // Pas de route ajoutée
        source.FlowTarget = VertexTarget;

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    // ── Cas source vide ───────────────────────────────────────────────────────

    [Fact]
    public void Reinforcement_DoesNotTransfer_WhenSourceHasNoSoldiers()
    {
        var (clock, _, source, target) = Setup(0, 2, 0, 1);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(0, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
    }
}
