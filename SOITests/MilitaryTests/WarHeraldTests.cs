using System.Linq;
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
/// War Herald : raid gratuit et instantané sur un emplacement militaire allié (ville, Flotte de
/// Guerre ou Camp Mobile), qui redirige le flux de chaque emplacement militaire de la civilisation
/// vers la cible si elle est à portée de renfort, sinon vers l'allié le plus proche de la cible qui
/// est lui-même à portée de renfort (même logique de relais que le Raid classique) — sauf ceux ayant
/// un flux d'attaque actif (ville ennemie ou monstre).
///
/// Géométrie (civ 0) :
///   Target          — Vertex(0,0 / 0,1 / 1,0)
///   AllyNoFlow      — Vertex(0,1 / 1,0 / 1,1)
///   AllyAttacking   — Vertex(1,0 / 1,1 / 2,0), FlowTarget = EnemyCity (civ 1)
///   AllyPatrolling  — Vertex(1,1 / 2,0 / 2,1), MonsterAttackTarget défini
/// Civ 1 :
///   EnemyCity       — Vertex(2,0 / 2,1 / 3,0)
/// </summary>
public class WarHeraldTests
{
    private static readonly Vertex Target         = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex AllyNoFlow     = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));
    private static readonly Vertex AllyAttacking  = Vertex.Create(new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer), new(2, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex AllyPatrolling = Vertex.Create(new(1, 1, IslandMap.SurfaceLayer), new(2, 0, IslandMap.SurfaceLayer), new(2, 1, IslandMap.SurfaceLayer));
    private static readonly Vertex EnemyCity      = Vertex.Create(new(2, 0, IslandMap.SurfaceLayer), new(2, 1, IslandMap.SurfaceLayer), new(3, 0, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(2, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(2, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(3, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    private static (MilitaryController ctrl, Civilization civ, City target, City allyNoFlow, City allyAttacking, City allyPatrolling) Setup()
    {
        var civ = new Civilization { Index = 0 };
        var enemyCiv = new Civilization { Index = 1 };

        var target = new City(Target) { CivilizationIndex = 0 };
        target.Buildings.Add(new Barracks { Level = 1 });
        var allyNoFlow = new City(AllyNoFlow) { CivilizationIndex = 0 };
        var allyAttacking = new City(AllyAttacking) { CivilizationIndex = 0, FlowTarget = EnemyCity };
        var allyPatrolling = new City(AllyPatrolling) { CivilizationIndex = 0, MonsterAttackTarget = new HexCoord(9, 9, IslandMap.SurfaceLayer) };
        var enemyCity = new City(EnemyCity) { CivilizationIndex = 1 };

        civ.AddCity(target);
        civ.AddCity(allyNoFlow);
        civ.AddCity(allyAttacking);
        civ.AddCity(allyPatrolling);
        enemyCiv.AddCity(enemyCity);

        var state = new WorldState(BuildMap(), [civ, enemyCiv], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (ctrl, civ, target, allyNoFlow, allyAttacking, allyPatrolling);
    }

    [Fact]
    public void StartWarHeraldRaid_RedirectsFlow_OfLocationsWithoutActiveAttack()
    {
        var (ctrl, civ, target, allyNoFlow, _, _) = Setup();

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Equal(target.Position, allyNoFlow.FlowTarget);
    }

    [Fact]
    public void StartWarHeraldRaid_DoesNotRedirect_LocationAttackingEnemyCity()
    {
        var (ctrl, civ, target, _, allyAttacking, _) = Setup();

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Equal(EnemyCity, allyAttacking.FlowTarget);
    }

    [Fact]
    public void StartWarHeraldRaid_DoesNotRedirect_LocationWithActiveMonsterAttack()
    {
        var (ctrl, civ, target, _, _, allyPatrolling) = Setup();
        var originalMonsterTarget = allyPatrolling.MonsterAttackTarget;

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Null(allyPatrolling.FlowTarget);
        Assert.Equal(originalMonsterTarget, allyPatrolling.MonsterAttackTarget);
    }

    [Fact]
    public void StartWarHeraldRaid_DoesNotSetTargetsOwnFlow()
    {
        var (ctrl, civ, target, _, _, _) = Setup();

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Null(target.FlowTarget);
    }

    [Fact]
    public void StartWarHeraldRaid_CancelsTargetsOwnReinforcementFlow()
    {
        var (ctrl, civ, target, allyNoFlow, _, _) = Setup();
        target.FlowTarget = allyNoFlow.Position;

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Null(target.FlowTarget);
    }

    [Fact]
    public void StartWarHeraldRaid_KeepsTargetsOwnActiveAttackFlow()
    {
        var (ctrl, civ, target, _, _, _) = Setup();
        target.FlowTarget = EnemyCity;

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Equal(EnemyCity, target.FlowTarget);
    }

    [Fact]
    public void StartWarHeraldRaid_RelaysThroughNearestAllyWhenTargetOutOfReinforcementRange()
    {
        // Chaîne de vertex adjacents (même bande hexagonale que Setup()) : Relay est à distance 3 du
        // Target (dans la portée de renfort par défaut de 5), Far est à distance 7 du Target (hors
        // portée) mais à distance 4 de Relay (dans la portée) — Far doit donc relayer via Relay plutôt
        // que de cibler Target directement.
        var relayVertex = Vertex.Create(new(1, 1, IslandMap.SurfaceLayer), new(2, 0, IslandMap.SurfaceLayer), new(2, 1, IslandMap.SurfaceLayer));
        var farVertex = Vertex.Create(new(3, 1, IslandMap.SurfaceLayer), new(4, 0, IslandMap.SurfaceLayer), new(4, 1, IslandMap.SurfaceLayer));

        var civ = new Civilization { Index = 0 };
        var target = new City(Target) { CivilizationIndex = 0 };
        target.Buildings.Add(new Barracks { Level = 1 });
        var relay = new City(relayVertex) { CivilizationIndex = 0 };
        relay.Buildings.Add(new Barracks { Level = 1 });
        var far = new City(farVertex) { CivilizationIndex = 0 };
        civ.AddCity(target);
        civ.AddCity(relay);
        civ.AddCity(far);

        var map = new IslandMap([
            new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(2, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(2, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(3, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(4, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new HexTile(new HexCoord(4, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        ]);
        var state = new WorldState(map, [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        ctrl.StartWarHeraldRaid(civ, target.Position);

        Assert.Equal(target.Position, relay.FlowTarget);
        Assert.Equal(relay.Position, far.FlowTarget);
    }

    [Fact]
    public void GetWarHeraldTargets_ReturnsOnlyOwnCities()
    {
        var (ctrl, civ, target, allyNoFlow, allyAttacking, allyPatrolling) = Setup();

        var targets = ctrl.GetWarHeraldTargets(civ);

        Assert.Equal(4, targets.Count);
        Assert.Contains(target.Position, targets);
        Assert.Contains(allyNoFlow.Position, targets);
        Assert.Contains(allyAttacking.Position, targets);
        Assert.Contains(allyPatrolling.Position, targets);
        Assert.DoesNotContain(EnemyCity, targets);
    }

    [Fact]
    public void GetWarHeraldTargets_IncludesFleetsAndMobileCamps()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(Target) { CivilizationIndex = 0 };
        var fleet = new WarFleet(AllyNoFlow) { CivilizationIndex = 0 };
        var camp = new MobileCamp(AllyAttacking) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddFleet(fleet);
        civ.AddMobileCamp(camp);

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        var targets = ctrl.GetWarHeraldTargets(civ);

        Assert.Equal(3, targets.Count);
        Assert.Contains(city.Position, targets);
        Assert.Contains(fleet.Position, targets);
        Assert.Contains(camp.Position, targets);
    }

    [Fact]
    public void StartWarHeraldRaid_CanTargetAFleetOrMobileCamp()
    {
        var civ = new Civilization { Index = 0 };
        var city = new City(Target) { CivilizationIndex = 0 };
        var fleet = new WarFleet(AllyNoFlow) { CivilizationIndex = 0 };
        var camp = new MobileCamp(AllyAttacking) { CivilizationIndex = 0 };
        civ.AddCity(city);
        civ.AddFleet(fleet);
        civ.AddMobileCamp(camp);

        var state = new WorldState(BuildMap(), [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        ctrl.StartWarHeraldRaid(civ, fleet.Position);

        Assert.Equal(fleet.Position, city.FlowTarget);
        Assert.Equal(fleet.Position, camp.FlowTarget);
    }

    [Fact]
    public void IsWarHeraldUnlocked_FalseByDefault_WithoutModifier()
    {
        var (ctrl, civ, _, _, _, _) = Setup();

        Assert.False(ctrl.IsWarHeraldUnlocked(civ));
    }
}
