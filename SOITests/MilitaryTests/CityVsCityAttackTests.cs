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

namespace SOITests.MilitaryTests;

/// <summary>
/// Tests de combat entre deux civilisations ayant chacune une seule ville.
///
/// Géométrie :
///   Civ 0 — City A : Vertex(0,0 / 0,1 / 1,0)
///   Civ 1 — City B : Vertex(0,1 / 1,0 / 1,1)   ← EdgeDistance = 1 depuis A
///
/// Les deux vertex partagent les hexes (0,1) et (1,0), ce qui garantit
/// la visibilité mutuelle sans Watchtower.
/// </summary>
public class CityVsCityAttackTests
{
    private static readonly Vertex VertexA = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));
    private static readonly Vertex VertexB = Vertex.Create(new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer), new(1, 1, IslandMap.SurfaceLayer));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
    ]);

    private static (WorldState state, GameClock clock, MilitaryController ctrl, CityBuilderController cityBuilder, City cityA, City cityB, Barracks barracksA)
        Setup(int soldiersA = 5, IEnumerable<Building>? buildingsB = null)
    {
        var civA = new Civilization { Index = 0 };
        var cityA = new City(VertexA) { CivilizationIndex = 0, Soldiers = soldiersA };
        var barracksA = new Barracks { Level = 2 };
        cityA.Buildings.Add(barracksA);
        civA.AddCity(cityA);

        var civB = new Civilization { Index = 1 };
        var cityB = new City(VertexB) { CivilizationIndex = 1 };
        if (buildingsB != null)
            foreach (var b in buildingsB) cityB.Buildings.Add(b);
        civB.AddCity(cityB);

        var state = new WorldState(BuildMap(), [civA, civB], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var cityBuilder = new CityBuilderController();
        cityBuilder.Initialize(state, clock, new GamePRNG());
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, cityBuilder, prng: new GamePRNG());

        cityA.FlowTarget = VertexB; // cible d'attaque pour déclencher la logique

        return (state, clock, ctrl, cityBuilder, cityA, cityB, barracksA);
    }

    // ── Condition de déclenchement ────────────────────────────────────────

    [Fact]
    public void Attack_FiresAfterCityAttackIntervalTicks()
    {
        var (_, clock, ctrl, _, _, _, barracksA) = Setup(soldiersA: 5);

        CityAttackEventArgs? args = null;
        ctrl.SoldierAttackedCity += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.NotNull(args);
    }

    [Fact]
    public void Attack_DoesNotFire_BeforeIntervalElapsed()
    {
        var (_, clock, ctrl, _, _, _, barracksA) = Setup(soldiersA: 5);

        bool fired = false;
        ctrl.SoldierAttackedCity += (_, _) => fired = true;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks - 1);

        Assert.False(fired);
    }

    [Fact]
    public void Attack_DoesNotFire_WhenNoSoldiers()
    {
        // cityB n'a pas de bâtiments — serait détruite au premier coup.
        // Si aucune attaque n'a lieu, la ville B existe toujours.
        // Soldats à 0 : aucune attaque ne peut se déclencher.
        var (state, clock, _, _, _, _, _) = Setup(soldiersA: 0);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Single(state.Civilizations[1].Cities);
    }

    // ── Événements ────────────────────────────────────────────────────────

    [Fact]
    public void SoldierAttackedCity_Event_ContainsCorrectVertices()
    {
        var (_, clock, ctrl, _, _, _, _) = Setup();

        CityAttackEventArgs? args = null;
        ctrl.SoldierAttackedCity += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.NotNull(args);
        Assert.Equal(VertexA, args.SourceCity);
        Assert.Equal(VertexB, args.TargetCity);
    }

    [Fact]
    public void SoldierAttackedCity_Event_PathIsNotEmpty()
    {
        var (_, clock, ctrl, _, _, _, _) = Setup();

        CityAttackEventArgs? args = null;
        ctrl.SoldierAttackedCity += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.NotNull(args);
        Assert.NotEmpty(args.Path);
        Assert.Equal(VertexA, args.Path[0]);
        Assert.Equal(VertexB, args.Path[^1]);
    }

    // ── Consommation de soldats ───────────────────────────────────────────

    [Fact]
    public void Attack_ConsumesOneSoldier()
    {
        // Capacité pleine (niveau 2 = 10) pour empêcher la production de s'activer.
        var (_, clock, _, _, cityA, _, _) = Setup(soldiersA: Barracks.MaxSoldiersPerLevel * 2);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Equal(Barracks.MaxSoldiersPerLevel * 2 - 1, cityA.Soldiers);
    }

    [Fact]
    public void Attack_IsRateLimited_OneSoldierPerInterval()
    {
        var palisade = new Palisade { Level = 1 };
        var (_, clock, _, _, cityA, cityB, barracksA) = Setup(soldiersA: 10, buildingsB: [palisade]);
        barracksA.Level = 1; // capacité réduite à 5 pour isoler la logique de rate-limit
        cityB.CurrentDefense = 10; // défense pleine : pas de destruction de bâtiment

        // Premier tick exact → 1 attaque
        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);
        Assert.Equal(9, cityA.Soldiers);

        // Avant le prochain intervalle → pas d'attaque supplémentaire
        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks - 1);
        Assert.Equal(9, cityA.Soldiers);

        // Au prochain intervalle → 2e attaque
        clock.SimulateAdvance(1);
        Assert.Equal(8, cityA.Soldiers);
    }

    // ── Dégâts sur la ville défenseure ───────────────────────────────────

    [Fact]
    public void Attack_ReducesCurrentDefense_WhenDefenseAboveZero()
    {
        var palisade = new Palisade { Level = 1 };
        var (_, clock, ctrl, _, _, cityB, _) = Setup(buildingsB: [palisade]);
        cityB.CurrentDefense = ctrl.GetDefenseScore(cityB); // = 10, regen ne s'active pas

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Equal(9, cityB.CurrentDefense);
    }

    [Fact]
    public void Attack_DoesNotDestroyBuilding_WhenDefenseAboveZero()
    {
        var palisade = new Palisade { Level = 1 };
        var (_, clock, ctrl, _, _, cityB, _) = Setup(buildingsB: [palisade]);
        cityB.CurrentDefense = ctrl.GetDefenseScore(cityB);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Single(cityB.Buildings); // Palissade toujours là
    }

    [Fact]
    public void Attack_ReducesTownHallLevel_WhenDefenseIsZero()
    {
        var townHall = new TownHall { Level = 2 };
        var (_, clock, _, _, _, cityB, _) = Setup(buildingsB: [townHall]);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Equal(1, townHall.Level);
        Assert.Single(cityB.Buildings);
    }

    [Fact]
    public void CityBuildingDestroyed_EventFires_WhenTownHallReachesLevelZero()
    {
        var (_, clock, ctrl, _, _, _, _) = Setup(buildingsB: [new TownHall { Level = 1 }]);

        CityBuildingDestroyedEventArgs? args = null;
        ctrl.CityBuildingDestroyed += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.NotNull(args);
        Assert.Equal(VertexB, args.CityVertex);
    }

    // ── Destruction de la ville ───────────────────────────────────────────

    [Fact]
    public void Attack_DestroysCity_WhenNoBuildingsAndNoDefense()
    {
        // cityB sans bâtiments ni défense : détruite au premier coup
        var (state, clock, _, _, _, _, _) = Setup(buildingsB: null);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Empty(state.Civilizations[1].Cities);
    }

    [Fact]
    public void CityDestroyed_EventFires_WhenCityIsEliminated()
    {
        var (_, clock, _, cityBuilder, _, _, _) = Setup(buildingsB: null);

        CityDestroyedEventArgs? args = null;
        cityBuilder.OnCityDestroyed += (_, a) => args = a;

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.NotNull(args);
        Assert.Equal(VertexB, args.CityVertex);
    }

    [Fact]
    public void Attack_RequiresMultipleHits_ToDestroyTownHallThenCity()
    {
        // TownHall niveau 1 : premier coup le retire, deuxième détruit la ville
        var (state, clock, _, _, _, cityB, _) = Setup(soldiersA: 10, buildingsB: [new TownHall { Level = 1 }]);

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);
        Assert.Empty(cityB.Buildings);
        Assert.Single(state.Civilizations[1].Cities); // ville encore là

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);
        Assert.Empty(state.Civilizations[1].Cities); // ville détruite
    }
}
