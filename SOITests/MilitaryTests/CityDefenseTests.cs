using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.MilitaryTests;

/// <summary>
/// Tests de régénération de la défense dynamique des villes.
/// Setup minimaliste : une seule civilisation sans soldats pour éviter
/// toute interférence avec la logique d'attaque inter-cités.
/// </summary>
public class CityDefenseTests
{
    private static readonly Vertex CityVertex = Vertex.Create(new(0, 0, IslandMap.SurfaceLayer), new(0, 1, IslandMap.SurfaceLayer), new(1, 0, IslandMap.SurfaceLayer));

    private static (GameClock clock, MilitaryController ctrl, City city)
        Setup(params Building[] buildings)
    {
        var map = new IslandMap([
            new(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        ]);

        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Wood] = 9999;
        civ.Resources[Resource.Stone] = 9999;
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        foreach (var b in buildings) city.Buildings.Add(b);
        civ.AddCity(city);

        var state = new WorldState(map, [civ], AtlasController.InvalidIslandId);
        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, prng: new GamePRNG());

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
    public void Defense_RegenAcceleratedByProtectiveFaith_PerDominionPointAroundCity()
    {
        var map = new IslandMap([
            new(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        ]);

        var civ = new Civilization { Index = 0 };
        civ.Resources[Resource.Wood] = 9999;
        civ.Resources[Resource.Stone] = 9999;
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        city.Buildings.Add(new Palisade { Level = 1 });
        civ.AddCity(city);

        var state = new WorldState(map, [civ], AtlasController.InvalidIslandId);

        // Dominion 2 + 3 + 4 = 9 points autour de la ville, avec 2 vertex Foi Protectrice
        // achetés (2 × 0.02 = 0.04) ⇒ +36% de vitesse de régénération.
        state.AddFeature(new Dominion(new HexCoord(0, 0, IslandMap.SurfaceLayer), level: 2));
        state.AddFeature(new Dominion(new HexCoord(0, 1, IslandMap.SurfaceLayer), level: 3));
        state.AddFeature(new Dominion(new HexCoord(1, 0, IslandMap.SurfaceLayer), level: 4));
        civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(ECategory.DOMINION_DEFENSE_REGEN_PER_LEVEL, EType.ADDITIVE, 0.04),
        }));

        var clock = new GameClock();
        clock.Start();
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock, prng: new GamePRNG());

        // Vitesse 1.36 ⇒ intervalle effectif = 500 / 1.36 = 367 ticks (au lieu de 500).
        Assert.Equal(1.36 * 100.0 / MilitaryController.DefenseRegenIntervalTicks, ctrl.GetDefenseRegenRate(city), 5);

        clock.SimulateAdvance(360);
        Assert.Equal(0, city.CurrentDefense);
        clock.SimulateAdvance(10);
        Assert.Equal(1, city.CurrentDefense);
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

    // ── Bastion Consacré (TEMPLE_DEFENSE_BONUS) ───────────────────────────

    private static (MilitaryController ctrl, Civilization civ, City city) SetupWithCiv(params Building[] buildings)
    {
        var map = new IslandMap([
            new(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(0, 1, IslandMap.SurfaceLayer), TerrainType.Plain),
            new(new HexCoord(1, 0, IslandMap.SurfaceLayer), TerrainType.Plain),
        ]);

        var civ = new Civilization { Index = 0 };
        var city = new City(CityVertex) { CivilizationIndex = 0 };
        foreach (var b in buildings) city.Buildings.Add(b);
        civ.AddCity(city);

        var state = new WorldState(map, [civ], AtlasController.InvalidIslandId);
        var ctrl = new MilitaryController();
        ctrl.Initialize(state, clock: null, prng: new GamePRNG());

        return (ctrl, civ, city);
    }

    [Fact]
    public void DefenseScore_TempleWithoutBastionConsacre_GivesNoDefense()
    {
        var (ctrl, _, city) = SetupWithCiv(new Temple { Level = 4 });
        Assert.Equal(0, ctrl.GetDefenseScore(city));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(3, 6)]
    [InlineData(4, 10)]
    public void DefenseScore_TempleWithBastionConsacre_GivesFixedBonusPerLevel(int templeLevel, int expectedBonus)
    {
        var (ctrl, civ, city) = SetupWithCiv(new Palisade { Level = 1 }, new Temple { Level = templeLevel });
        civ.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(ECategory.TEMPLE_DEFENSE_BONUS, EType.ADDITIVE, 1),
        }));

        Assert.Equal(10 + expectedBonus, ctrl.GetDefenseScore(city)); // Palissade = 10
    }
}
