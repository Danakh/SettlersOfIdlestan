using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.MilitaryTests;

/// <summary>
/// Tests de l'attaque à distance des monstres (techno Surveillance + Tour de guet).
///
/// Géométrie : ville au vertex (Center, NE, NW). Center=(0,0), NE=(0,1), NW=(-1,1) — les 3 hexes
/// sont mutuellement adjacents (distance 1 entre eux).
///
/// Le monstre est placé en (0, D) sur l'axe R. La distance retenue par <see cref="MonsterCombatEngine"/>
/// est le MAXIMUM sur les 3 hexes de la ville (le coin le plus éloigné), pas le minimum — sinon la
/// portée affichée au joueur serait dépassable d'1 hex en choisissant le coin le plus proche. Pour ce
/// placement le calcul donne Max(dist(Center), dist(NE), dist(NW)) = D pour D ≥ 2 :
///   D=2 → Center=2, NE=1, NW=2 → Max=2
///   D=3 → Center=3, NE=2, NW=3 → Max=3
/// </summary>
public class RangedMonsterAttackTests
{
    private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
    private static HexCoord NE => new(0, 1, IslandMap.SurfaceLayer);
    private static HexCoord NW => new(-1, 1, IslandMap.SurfaceLayer);

    /// <summary>monsterDistance = 0 place le monstre sur le hex de ville Center (corps-à-corps) ; sinon (0, monsterDistance).</summary>
    private static (WorldState state, GameClock clock, MilitaryController ctrl, Civilization civ, City city, Bandit monster)
        CreateSetup(int monsterDistance, bool hasWatchtower, bool hasSurveillance, int soldiers = 5)
    {
        var monsterPos = monsterDistance == 0
            ? Center
            : new HexCoord(0, monsterDistance, IslandMap.SurfaceLayer);

        var tiles = new List<HexTile>
        {
            new(Center, TerrainType.Plain),
            new(NE, TerrainType.Plain),
            new(NW, TerrainType.Plain),
        };
        if (monsterDistance != 0)
            tiles.Add(new HexTile(monsterPos, TerrainType.Plain));
        var map = new IslandMap(tiles);

        var civ = new Civilization { Index = 0 };
        var city = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0, Soldiers = soldiers };
        city.Buildings.Add(new Barracks { Level = 1 });
        if (hasWatchtower)
            city.Buildings.Add(new Watchtower { Level = 1 });
        civ.AddCity(city);

        if (hasSurveillance)
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_RANGED_MONSTER_ATTACK, EType.ADDITIVE, 1),
            }));

        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        var monster = new Bandit(monsterPos, 0) { Found = true, LastMovedTick = 999_999_999L };
        state.AddFeature(monster);

        var clock = new GameClock();
        clock.Start();

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, prng: new GamePRNG());

        return (state, clock, ctrl, civ, city, monster);
    }

    // ── GetMonsterAttackAvailability ──────────────────────────────────────

    [Fact]
    public void Colocated_AlwaysAvailable_RegardlessOfTechOrBuilding()
    {
        var (_, _, ctrl, _, city, monster) = CreateSetup(monsterDistance: 0, hasWatchtower: false, hasSurveillance: false);

        Assert.Equal(MonsterAttackAvailability.Available, ctrl.GetMonsterAttackAvailability(city, monster));
    }

    [Fact]
    public void Distance2_WithoutSurveillance_IsTooFar()
    {
        var (_, _, ctrl, _, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: true, hasSurveillance: false);

        Assert.Equal(MonsterAttackAvailability.TooFar, ctrl.GetMonsterAttackAvailability(city, monster));
    }

    [Fact]
    public void Distance2_WithSurveillanceButNoWatchtower_RequiresWatchtower()
    {
        var (_, _, ctrl, _, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: false, hasSurveillance: true);

        Assert.Equal(MonsterAttackAvailability.RequiresWatchtower, ctrl.GetMonsterAttackAvailability(city, monster));
    }

    [Fact]
    public void Distance2_WithSurveillanceAndWatchtower_IsAvailable()
    {
        var (_, _, ctrl, _, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: true, hasSurveillance: true);

        Assert.Equal(MonsterAttackAvailability.Available, ctrl.GetMonsterAttackAvailability(city, monster));
    }

    [Fact]
    public void Distance3_IsTooFar_EvenWithSurveillanceAndWatchtower()
    {
        var (_, _, ctrl, _, city, monster) = CreateSetup(monsterDistance: 3, hasWatchtower: true, hasSurveillance: true);

        Assert.Equal(MonsterAttackAvailability.TooFar, ctrl.GetMonsterAttackAvailability(city, monster));
    }

    // ── Résolution de l'attaque (flux joueur) ──────────────────────────────

    [Fact]
    public void RangedFlow_WhenAvailable_DamagesMonsterAndConsumesSoldier()
    {
        var (state, clock, ctrl, _, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: true, hasSurveillance: true, soldiers: 5);
        int initialHp = monster.Hp;

        ctrl.SetMonsterFlow(city, monster.Position);
        clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

        Assert.True(monster.Hp < initialHp);
        Assert.Equal(4, city.Soldiers);
        Assert.Null(city.FlowTarget);
    }

    [Fact]
    public void RangedFlow_WhenRequiresWatchtower_DoesNothing()
    {
        var (state, clock, ctrl, _, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: false, hasSurveillance: true, soldiers: 5);
        int initialHp = monster.Hp;

        ctrl.SetMonsterFlow(city, monster.Position);
        clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

        Assert.Equal(initialHp, monster.Hp);
        Assert.Equal(5, city.Soldiers);
    }

    [Fact]
    public void RangedFlow_WhenTooFar_DoesNothing()
    {
        var (state, clock, ctrl, _, city, monster) = CreateSetup(monsterDistance: 3, hasWatchtower: true, hasSurveillance: true, soldiers: 5);
        int initialHp = monster.Hp;

        ctrl.SetMonsterFlow(city, monster.Position);
        clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

        Assert.Equal(initialHp, monster.Hp);
        Assert.Equal(5, city.Soldiers);
    }

    [Fact]
    public void SetMonsterFlow_ClearsExistingCityFlowTarget()
    {
        var (state, clock, ctrl, civ, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: true, hasSurveillance: true);
        var otherCity = new City(Vertex.Create(new(5, 0, IslandMap.SurfaceLayer), new(5, 1, IslandMap.SurfaceLayer), new(6, 0, IslandMap.SurfaceLayer))) { CivilizationIndex = 1 };
        ctrl.SetCityFlow(city, otherCity.Position);
        Assert.NotNull(city.FlowTarget);

        ctrl.SetMonsterFlow(city, monster.Position);

        Assert.Null(city.FlowTarget);
        Assert.Equal(monster.Position, city.MonsterAttackTarget);
    }

    [Fact]
    public void SetCityFlow_ClearsExistingMonsterAttackTarget()
    {
        var (state, clock, ctrl, civ, city, monster) = CreateSetup(monsterDistance: 2, hasWatchtower: true, hasSurveillance: true);
        ctrl.SetMonsterFlow(city, monster.Position);
        Assert.NotNull(city.MonsterAttackTarget);

        var otherCity = new City(Vertex.Create(new(5, 0, IslandMap.SurfaceLayer), new(5, 1, IslandMap.SurfaceLayer), new(6, 0, IslandMap.SurfaceLayer))) { CivilizationIndex = 1 };
        ctrl.SetCityFlow(city, otherCity.Position);

        Assert.Null(city.MonsterAttackTarget);
        Assert.Equal(otherCity.Position, city.FlowTarget);
    }
}
