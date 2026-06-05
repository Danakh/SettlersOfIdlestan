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
/// Vérifie que les renforts respectent la capacité maximale de soldats de la cité cible.
///
/// Géométrie :
///   Civ 0 (joueur) — Source : Vertex(0,0 / 0,1 / 1,0)
///   Civ 0 (joueur) — Cible  : Vertex(0,1 / 1,0 / 1,1)
///   Distance entre les deux vertices : 1 edge (dans ReinforcementRange par défaut = 5).
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
    /// Crée deux villes alliées (civ joueur index 0) avec un flux défini de source → cible.
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

        source.FlowTarget = VertexTarget;

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (clock, ctrl, source, target);
    }

    // ── Cas nominal ──────────────────────────────────────────────────────────

    [Fact]
    public void Reinforcement_TransfersSoldier_WhenTargetBelowMaxCapacity()
    {
        // Source : 5 soldats (max 10), Cible : 0 soldats (max 5)
        var (clock, _, source, target) = Setup(
            sourceSoldiers: 5, sourceBarracksLevel: 2,
            targetSoldiers: 0, targetBarracksLevel: 1);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(4, source.Soldiers);
        Assert.Equal(1, target.Soldiers);
    }

    [Fact]
    public void ReinforcementSent_EventFired_WhenTargetBelowMaxCapacity()
    {
        var (clock, ctrl, _, _) = Setup(
            sourceSoldiers: 5, sourceBarracksLevel: 2,
            targetSoldiers: 0, targetBarracksLevel: 1);

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
        var (clock, _, source, target) = Setup(
            sourceSoldiers: 5, sourceBarracksLevel: 2,
            targetSoldiers: 5, targetBarracksLevel: 1);

        Assert.Equal(5, target.MaxSoldiers); // précondition

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);   // source inchangée
        Assert.Equal(5, target.Soldiers);   // cible inchangée
    }

    [Fact]
    public void ReinforcementSent_EventNotFired_WhenTargetAtMaxCapacity()
    {
        var (clock, ctrl, _, _) = Setup(
            sourceSoldiers: 5, sourceBarracksLevel: 2,
            targetSoldiers: 5, targetBarracksLevel: 1);

        bool fired = false;
        ctrl.ReinforcementSent += (_, _) => fired = true;

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.False(fired);
    }

    // ── Arrêt au seuil ────────────────────────────────────────────────────────

    [Fact]
    public void Reinforcement_StopsExactlyAtMaxCapacity()
    {
        // Cible à 4/5 : un seul soldat peut être transféré, puis la cible est pleine.
        var (clock, _, source, target) = Setup(
            sourceSoldiers: 5, sourceBarracksLevel: 2,
            targetSoldiers: 4, targetBarracksLevel: 1);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        Assert.Equal(4, source.Soldiers);
        Assert.Equal(5, target.Soldiers);

        // Second tick : cible pleine, aucun transfert supplémentaire.
        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);
        Assert.Equal(4, source.Soldiers);   // source inchangée
        Assert.Equal(5, target.Soldiers);   // cible toujours à max
    }

    [Fact]
    public void Reinforcement_NeverExceedsMaxCapacity_AfterManyTicks()
    {
        // Source avec beaucoup de soldats, cible initialement vide (max = 5).
        var (clock, _, _, target) = Setup(
            sourceSoldiers: 20, sourceBarracksLevel: 4,
            targetSoldiers: 0, targetBarracksLevel: 1);

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.True(target.Soldiers <= target.MaxSoldiers,
            $"La cible a {target.Soldiers} soldats mais le max est {target.MaxSoldiers}.");
        Assert.Equal(5, target.Soldiers);
    }

    // ── Cas source vide ───────────────────────────────────────────────────────

    [Fact]
    public void Reinforcement_DoesNotTransfer_WhenSourceHasNoSoldiers()
    {
        var (clock, _, source, target) = Setup(
            sourceSoldiers: 0, sourceBarracksLevel: 2,
            targetSoldiers: 0, targetBarracksLevel: 1);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(0, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
    }
}
