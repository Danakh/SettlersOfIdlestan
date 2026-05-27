using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using Xunit;

namespace SOITests.MilitaryTests;

/// <summary>
/// Tests de régénération de la défense dynamique des villes.
/// Setup minimaliste : une seule civilisation sans soldats pour éviter
/// toute interférence avec la logique d'attaque inter-cités.
/// </summary>
public class CityDefenseTests
{
    private static readonly Vertex CityVertex = Vertex.Create(new(0, 0), new(0, 1), new(1, 0));

    private static (GameClock clock, MilitaryController ctrl, City city)
        Setup(params Building[] buildings)
    {
        var map = new IslandMap([
            new(new HexCoord(0, 0), TerrainType.Plain),
            new(new HexCoord(0, 1), TerrainType.Plain),
            new(new HexCoord(1, 0), TerrainType.Plain),
        ]);

        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        foreach (var b in buildings) city.Buildings.Add(b);
        civ.Cities.Add(city);

        var state = new IslandState(map, [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock);

        return (clock, ctrl, city);
    }

    // ── Régénération ──────────────────────────────────────────────────────

    [Fact]
    public void Defense_StartsAtZero()
    {
        var (_, _, city) = Setup(new Palisade { Level = 1 });
        Assert.Equal(0, city.CurrentDefense);
    }

    [Fact]
    public void Defense_RegeneratesOnePointPerInterval()
    {
        var (clock, _, city) = Setup(new Palisade { Level = 1 });

        clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);

        Assert.Equal(1, city.CurrentDefense);
    }

    [Fact]
    public void Defense_RegeneratesMultiplePoints()
    {
        var (clock, _, city) = Setup(new Palisade { Level = 1 });

        clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);
        clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);
        clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);

        Assert.Equal(3, city.CurrentDefense);
    }

    [Fact]
    public void Defense_StopsAtBuildingScore()
    {
        var (clock, ctrl, city) = Setup(new Palisade { Level = 1 });
        int max = ctrl.GetDefenseScore(city); // Palissade = 10

        for (int i = 0; i < max + 5; i++)
            clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);

        Assert.Equal(max, city.CurrentDefense);
    }

    [Fact]
    public void Defense_StaysAtZero_WithoutDefenseBuildings()
    {
        var (clock, _, city) = Setup(new Market());

        for (int i = 0; i < 10; i++)
            clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);

        Assert.Equal(0, city.CurrentDefense);
    }

    [Fact]
    public void Defense_MaxIsBarracksScore_WhenOnlyBarracks()
    {
        var barracks = new Barracks { Level = 2 };
        var (clock, ctrl, city) = Setup(barracks);
        int max = ctrl.GetDefenseScore(city); // Caserne = 5

        for (int i = 0; i < max + 5; i++)
            clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);

        Assert.Equal(max, city.CurrentDefense);
    }

    [Fact]
    public void Defense_MaxIsCumulative_WithPalisadeAndBarracks()
    {
        var (clock, ctrl, city) = Setup(new Palisade { Level = 1 }, new Barracks { Level = 2 });
        int max = ctrl.GetDefenseScore(city); // 10 + 5 = 15

        Assert.Equal(15, max);

        for (int i = 0; i < max + 5; i++)
            clock.SimulateAdvance(MilitaryController.DefenseRegenIntervalTicks);

        Assert.Equal(15, city.CurrentDefense);
    }
}
