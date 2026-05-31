using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System.Linq;
using Xunit;

namespace SOITests.MilitaryTests;

/// <summary>
/// Tests sur les interactions entre combat militaire et territoires contestés.
///
/// Géométrie :
///   Civ 0 — City A : Vertex(0,0 / 0,1 / 1,0)
///   Civ 1 — City B : Vertex(0,1 / 1,0 / 1,1)
///
/// Hexes partagés (frontière) : (0,1) et (1,0) → 2 territoires contestés attendus.
/// </summary>
public class MilitaryControllerWarTests
{
    private static readonly Vertex VertexA = Vertex.Create(new(0, 0), new(0, 1), new(1, 0));
    private static readonly Vertex VertexB = Vertex.Create(new(0, 1), new(1, 0), new(1, 1));

    private static IslandMap BuildMap() => new([
        new HexTile(new HexCoord(0, 0), TerrainType.Plain),
        new HexTile(new HexCoord(0, 1), TerrainType.Plain),
        new HexTile(new HexCoord(1, 0), TerrainType.Plain),
        new HexTile(new HexCoord(1, 1), TerrainType.Plain),
    ]);

    /// <summary>
    /// Crée un état de jeu avec 2 civilisations aux villes adjacentes.
    /// Le handler CityDestroyed → RefreshContestedTerritories est branché
    /// pour reproduire le comportement de MainGameController (après le fix).
    /// </summary>
    private static (IslandState state, GameClock clock, MilitaryController ctrl, FeatureController featureCtrl, Barracks barracksA)
        Setup(int soldiersA = 5)
    {
        var civA = new Civilization { Index = 0 };
        var cityA = new City(VertexA) { CivilizationIndex = 0, Soldiers = soldiersA };
        var barracksA = new Barracks { Level = 2 };
        cityA.Buildings.Add(barracksA);
        civA.Cities.Add(cityA);

        var civB = new Civilization { Index = 1 };
        var cityB = new City(VertexB) { CivilizationIndex = 1 };
        civB.Cities.Add(cityB);

        var state = new IslandState(BuildMap(), [civA, civB], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var featureCtrl = new FeatureController();
        featureCtrl.Initialize(state, clock);

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        // Branchement identique à MainGameController après le fix du bug
        ctrl.CityDestroyed += (_, _) => featureCtrl.RefreshContestedTerritories();

        cityA.FlowTarget = VertexB; // cible de renfort pour déclencher la logique

        return (state, clock, ctrl, featureCtrl, barracksA);
    }

    // ── Création des territoires contestés ────────────────────────────────

    [Fact]
    public void ContestedTerritories_AreCreated_WhenTwoCivsHaveAdjacentCities()
    {
        var (state, _, _, _, _) = Setup();

        Assert.NotEmpty(state.Features.OfType<ContestedTerritory>());
    }

    [Fact]
    public void ContestedTerritories_CoverOnlySharedHexes()
    {
        // VertexA ∩ VertexB = hexes (0,1) et (1,0)
        var (state, _, _, _, _) = Setup();

        var contested = state.Features.OfType<ContestedTerritory>().ToList();

        Assert.Equal(2, contested.Count);
        Assert.Contains(contested, c => c.Position.Equals(new HexCoord(0, 1)));
        Assert.Contains(contested, c => c.Position.Equals(new HexCoord(1, 0)));
    }

    [Fact]
    public void ContestedTerritories_AreAbsent_WhenOnlyOneCivilization()
    {
        var civA = new Civilization { Index = 0 };
        var cityA = new City(VertexA) { CivilizationIndex = 0 };
        civA.Cities.Add(cityA);

        var state = new IslandState(BuildMap(), [civA], AtlasController.InvalidIslandId);
        var clock = new GameClock();

        var featureCtrl = new FeatureController();
        featureCtrl.Initialize(state, clock);

        Assert.Empty(state.Features.OfType<ContestedTerritory>());
    }

    [Fact]
    public void ContestedTerritories_AreAbsent_WhenCitiesShareNoHex()
    {
        var farVertexB = Vertex.Create(new(5, 0), new(5, 1), new(6, 0));

        var civA = new Civilization { Index = 0 };
        var cityA = new City(VertexA) { CivilizationIndex = 0 };
        civA.Cities.Add(cityA);

        var civB = new Civilization { Index = 1 };
        var cityB = new City(farVertexB) { CivilizationIndex = 1 };
        civB.Cities.Add(cityB);

        var map = new IslandMap([
            new HexTile(new HexCoord(0, 0), TerrainType.Plain),
            new HexTile(new HexCoord(0, 1), TerrainType.Plain),
            new HexTile(new HexCoord(1, 0), TerrainType.Plain),
            new HexTile(new HexCoord(5, 0), TerrainType.Plain),
            new HexTile(new HexCoord(5, 1), TerrainType.Plain),
            new HexTile(new HexCoord(6, 0), TerrainType.Plain),
        ]);

        var state = new IslandState(map, [civA, civB], AtlasController.InvalidIslandId);
        var clock = new GameClock();

        var featureCtrl = new FeatureController();
        featureCtrl.Initialize(state, clock);

        Assert.Empty(state.Features.OfType<ContestedTerritory>());
    }

    // ── Suppression des territoires contestés à la destruction d'une ville ─

    [Fact]
    public void ContestedTerritories_AreRemoved_WhenEnemyCityIsDestroyed()
    {
        // cityB sans bâtiments ni défense : détruite au premier coup
        var (state, clock, _, _, _) = Setup(soldiersA: 5);

        Assert.NotEmpty(state.Features.OfType<ContestedTerritory>());

        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);

        Assert.Empty(state.Civilizations[1].Cities);
        Assert.Empty(state.Features.OfType<ContestedTerritory>());
    }

    [Fact]
    public void ContestedTerritories_PersistUntilCityActuallyDestroyed()
    {
        // TownHall niveau 1 : il faut deux coups pour détruire la ville
        var civA = new Civilization { Index = 0 };
        var cityA = new City(VertexA) { CivilizationIndex = 0, Soldiers = 10 };
        var barracksA = new Barracks { Level = 2 };
        cityA.Buildings.Add(barracksA);
        civA.Cities.Add(cityA);

        var civB = new Civilization { Index = 1 };
        var cityB = new City(VertexB) { CivilizationIndex = 1 };
        cityB.Buildings.Add(new TownHall { Level = 1 });
        civB.Cities.Add(cityB);

        cityA.FlowTarget = VertexB; // cible de renfort pour déclencher la logique

        var state = new IslandState(BuildMap(), [civA, civB], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();

        var featureCtrl = new FeatureController();
        featureCtrl.Initialize(state, clock);

        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);
        ctrl.CityDestroyed += (_, _) => featureCtrl.RefreshContestedTerritories();

        // Après le premier coup : TownHall retiré, ville toujours en vie
        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);
        Assert.Single(state.Civilizations[1].Cities);
        Assert.NotEmpty(state.Features.OfType<ContestedTerritory>());

        // Après le deuxième coup : ville détruite, territoires contestés supprimés
        clock.SimulateAdvance(MilitaryController.CityAttackIntervalTicks);
        Assert.Empty(state.Civilizations[1].Cities);
        Assert.Empty(state.Features.OfType<ContestedTerritory>());
    }

    [Fact]
    public void ContestedTerritories_BlockHarvest()
    {
        var (state, _, _, _, _) = Setup();

        var contested = state.Features.OfType<ContestedTerritory>().First();

        Assert.True(contested.BlocksHarvest);
    }
}
