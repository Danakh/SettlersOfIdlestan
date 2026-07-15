using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests;

/// <summary>
/// Tests de CorruptionController : production de Dominion / réduction de Corruption par les Temples
/// de niveau 2-4 (ProcessTempleProduction), et débordement Corruption/Dominion entre hexes voisins
/// (ProcessSpread). Les scénarios avec un seul hex existant autour de la ville évitent toute
/// dépendance au tirage aléatoire du hex ciblé (GamePRNG.Next(1) ne consomme pas le générateur) ; les
/// scénarios de débordement utilisent une mini-carte à 2 hexes (un seul voisin candidat) pour la même
/// raison, avec la graine 1 dont la séquence (29, 30, …) a été vérifiée déterministe pour ces cas.
/// </summary>
public class CorruptionControllerTests
{
    /// <summary>
    /// Ville sur un vertex avec un seul hex existant sur la carte (les deux autres n'ont pas de
    /// tuile — l'eau est désormais un hex valide pour la Corruption/le Dominion) — cible du Temple garantie.
    /// </summary>
    private static (WorldState state, City city, HexCoord landHex) CreateSingleLandHexCitySetup()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

        var tiles = new[]
        {
            new HexTile(a, TerrainType.Plain),
        };

        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var city = new City(Vertex.Create(a, b, c)) { CivilizationIndex = civ.Index };
        civ.AddCity(city);

        return (state, city, a);
    }

    /// <summary>Deux hexes de terre adjacents, aucun autre hex sur la carte — un seul voisin candidat de chaque côté pour le débordement.</summary>
    private static (WorldState state, HexCoord a, HexCoord b) CreateTwoLandHexesSetup()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer); // voisin Est de a

        var tiles = new[] { new HexTile(a, TerrainType.Plain), new HexTile(b, TerrainType.Plain) };
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        return (state, a, b);
    }

    private static CorruptionController CreateController(WorldState state, GameClock clock, int seed = 1)
    {
        var controller = new CorruptionController();
        controller.Initialize(state, clock, new GamePRNG(seed));
        return controller;
    }

    // ── Production des Temples ──────────────────────────────────────────────

    [Fact]
    public void TempleLevel2_NoCorruptionOnTarget_CreatesDominionLevel1()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 2 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().SingleOrDefault();
        Assert.NotNull(dominion);
        Assert.Equal(1, dominion!.Level);
    }

    [Fact]
    public void TempleLevel2_ExistingDominionAtCap_DoesNotExceedTwiceTempleLevel()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 2 });
        state.AddFeature(new Dominion(landHex, level: 4)); // cap = 2 * 2 = 4

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().Single();
        Assert.Equal(4, dominion.Level);
    }

    [Fact]
    public void TempleLevel4_ExistingDominionBelowCap_IncrementsUpToEight()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 4 });
        state.AddFeature(new Dominion(landHex, level: 7)); // cap = 2 * 4 = 8

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().Single();
        Assert.Equal(8, dominion.Level);
    }

    [Fact]
    public void TempleLevel2_CorruptionOnTarget_ReducesCorruptionInsteadOfCreatingDominion()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 3));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void TempleLevel2_CorruptionAtLevel1_RemovesFeatureOnceReducedToZero()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
        Assert.False(state.HasFeaturesAt(landHex));
    }

    [Fact]
    public void TempleLevel1_BelowThreshold_DoesNotProduce()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 1 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.False(state.HasFeaturesAt(landHex));
    }

    [Fact]
    public void TempleLevel5_AboveThreshold_DoesNotProduce()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.Buildings.Add(new Temple { Level = 5 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.False(state.HasFeaturesAt(landHex));
    }

    // ── Débordement Corruption/Dominion ─────────────────────────────────────

    [Fact]
    public void Spread_OppositeStatusNeighbor_BothReduceByOne()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: 10); // 100% de déclenchement
        var dominion = new Dominion(b, level: 4);
        state.AddFeature(corruption);
        state.AddFeature(dominion);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock); // seed 1 : séquence vérifiée déterministe pour ce scénario

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, corruption.Level);
        Assert.Equal(3, dominion.Level);
    }

    [Fact]
    public void Spread_SameStatusLargeLevelGap_SourceLosesNeighborGains()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var strong = new Dominion(a, level: 10); // 100% de déclenchement
        var weak = new Dominion(b, level: 1); // écart de 9 > 2
        state.AddFeature(strong);
        state.AddFeature(weak);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, strong.Level);
        Assert.Equal(2, weak.Level);
    }

    [Fact]
    public void Spread_SameStatusSmallLevelGap_NoChange()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var strong = new Dominion(a, level: 5); // 100% de déclenchement (5*10=50 > roll 29)
        var close = new Dominion(b, level: 3); // écart de 2, pas > 2
        state.AddFeature(strong);
        state.AddFeature(close);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(5, strong.Level);
        Assert.Equal(3, close.Level);
    }

    [Fact]
    public void Spread_EmptyNeighborStrongSource_SeedsNewFeatureAtLevelOne()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var strong = new Dominion(a, level: 10); // 100% de déclenchement, écart avec 0 = 10 > 2
        state.AddFeature(strong);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, strong.Level);
        var seeded = state.GetFeaturesAt(b).OfType<Dominion>().SingleOrDefault();
        Assert.NotNull(seeded);
        Assert.Equal(1, seeded!.Level);
    }

    [Fact]
    public void Spread_EmptyNeighborStrongCorruptionSource_SeedsNewCorruption()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var strong = new Corruption(a, level: 10); // 100% de déclenchement, écart avec 0 = 10 > 2
        state.AddFeature(strong);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, strong.Level);
        var seeded = state.GetFeaturesAt(b).OfType<Corruption>().SingleOrDefault();
        Assert.NotNull(seeded);
        Assert.Equal(1, seeded!.Level);
    }

    [Fact]
    public void Spread_EmptyNeighborSmallGap_NoSeed()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var close = new Dominion(a, level: 2); // écart avec 0 = 2, pas > 2 — même si le déclenchement a lieu, pas de semis
        state.AddFeature(close);

        var clock = new GameClock();
        clock.Start();
        var controller = new CorruptionController();
        // GamePRNG.Next(100) consomme le générateur même si le seuil de déclenchement (20%) n'est
        // pas atteint : on avance sur plusieurs ticks pour couvrir le cas où le tirage réussirait,
        // et on vérifie que même alors aucune poche n'est semée (l'écart de niveau reste <= 2).
        controller.Initialize(state, clock, new GamePRNG(1));

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(b).OfType<Dominion>());
    }

    [Fact]
    public void Spread_NoOtherFeatureOnMap_NeverThrows()
    {
        var (state, _, _) = CreateTwoLandHexesSetup();

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.Features);
    }
}
