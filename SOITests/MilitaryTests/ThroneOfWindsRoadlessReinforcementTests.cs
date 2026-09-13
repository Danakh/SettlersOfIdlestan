using System.Collections.Generic;
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
/// Trône des Vents : les renforts de la civilisation n'ont plus besoin de route
/// (UNLOCK_ROADLESS_REINFORCEMENT). Faute de chemin routier, les soldats volent en ligne droite
/// jusqu'à la cible ; la portée de renfort (REINFORCEMENT_RANGE) continue de s'appliquer.
///
/// Géométrie (civ 0) — chaîne de 7 vertex adjacents, aucune route posée :
///   Source — Vertex(0,0 / 0,1 / 1,0)
///   VNear  — 4 arêtes plus loin, dans la portée par défaut (5)
///   VFar   — 6 arêtes plus loin, hors de portée
/// </summary>
public class ThroneOfWindsRoadlessReinforcementTests
{
    private static readonly Vertex VSource = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex VNear   = Vertex.Create(new(2, 0, IslandMap.SurfaceLayer), new(2, 1, IslandMap.SurfaceLayer), new(3, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex VFar    = Vertex.Create(new(3, 0, IslandMap.SurfaceLayer), new(3, 1, IslandMap.SurfaceLayer), new(4, 0, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(2, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(2, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(3, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(3, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(4, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    private static (GameClock clock, MilitaryController ctrl, City source, City target) Setup(
        bool withThrone, Vertex targetPosition)
    {
        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Ore] = 999;
        civ.Resources[Resource.Food] = 999;

        var source = new City(VSource) { CivilizationIndex = 0, Soldiers = 5 };
        source.AddBuilding(new Barracks { Level = 2 });
        if (withThrone)
            source.AddBuilding(new ThroneOfWinds { Level = 1 });

        var target = new City(targetPosition) { CivilizationIndex = 0, Soldiers = 0 };
        target.AddBuilding(new Barracks { Level = 1 });

        civ.AddCity(source);
        civ.AddCity(target);

        // Aucune route : c'est tout l'objet du test.
        source.FlowTarget = targetPosition;

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (clock, ctrl, source, target);
    }

    [Fact]
    public void Reinforcement_BlockedWithoutRoad_WithoutThroneOfWinds()
    {
        var (clock, _, source, target) = Setup(withThrone: false, VNear);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }

    [Fact]
    public void Reinforcement_AllowedWithoutRoad_WithThroneOfWinds()
    {
        // Le soldat quitte la ville et vole vers la cible : il transite (IncomingSoldiers), il
        // n'arrive donc pas instantanément comme sur le lien forestier de l'Arbre-Cœur.
        var (clock, _, source, target) = Setup(withThrone: true, VNear);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(4, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Single(target.IncomingSoldiers);
    }

    [Fact]
    public void Reinforcement_FliesInStraightLine_WithThroneOfWinds()
    {
        // Sans route à suivre, le chemin transmis à l'animation est le vol direct source → cible.
        var (clock, ctrl, _, _) = Setup(withThrone: true, VNear);

        List<Vertex>? path = null;
        ctrl.ReinforcementSent += (_, args) => path = args.Path;

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.NotNull(path);
        Assert.Equal(new List<Vertex> { VSource, VNear }, path);
    }

    [Fact]
    public void Reinforcement_StillBlockedBeyondRange_WithThroneOfWinds()
    {
        // Le Trône des Vents affranchit de la route, pas de la portée de renfort.
        var (clock, _, source, target) = Setup(withThrone: true, VFar);

        clock.SimulateAdvance(MilitaryController.ReinforcementIntervalTicks);

        Assert.Equal(5, source.Soldiers);
        Assert.Equal(0, target.Soldiers);
        Assert.Empty(target.IncomingSoldiers);
    }
}
