using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
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
/// raison. Le PRNG (Lehmer/Park-Miller) donne un tout premier tirage quasi nul pour toute petite
/// graine (1, 2, 3, …) — sans effet sur les scénarios "100% de déclenchement" (0 déclenche toujours),
/// mais rend une graine minuscule impropre à démontrer un NON-déclenchement sur le premier tirage :
/// ces scénarios précis utilisent une graine plus grande (voir commentaire au cas par cas).
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

    /// <summary>
    /// Même chose, mais la ville est dans l'Inframonde (un seul hex existant sur cette couche, la
    /// surface se réduisant à un hex vide) — isole le malus de profondeur du Dominion.
    /// </summary>
    private static (WorldState state, City city, HexCoord underworldHex) CreateSingleHexUnderworldCitySetup()
    {
        var surface = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var map = new IslandMap(new[] { new HexTile(surface, TerrainType.Plain) });
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var a = new HexCoord(0, 0, LayerState.UnderworldZ);
        var b = new HexCoord(1, 0, LayerState.UnderworldZ);
        var c = new HexCoord(0, 1, LayerState.UnderworldZ);
        var underworldTiles = new[] { new HexTile(a, TerrainType.Mountain) };
        state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(underworldTiles, LayerState.UnderworldZ)));

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

    /// <summary>
    /// Centre + anneau de 6 voisins immédiats + second anneau de 12 hexes (aucun autre hex sur la
    /// carte) — permet de vérifier qu'un débordement ne dépasse jamais le premier anneau.
    /// </summary>
    private static (WorldState state, HexCoord center, List<HexCoord> ring1, List<HexCoord> ring2) CreateTwoRingHexGridSetup()
    {
        var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var ring1 = center.Neighbors().ToList();
        var ring2 = ring1
            .SelectMany(h => h.Neighbors())
            .Where(h => !h.Equals(center) && !ring1.Contains(h))
            .Distinct()
            .ToList();

        var allHexes = new List<HexCoord> { center };
        allHexes.AddRange(ring1);
        allHexes.AddRange(ring2);

        var tiles = allHexes.Select(h => new HexTile(h, TerrainType.Plain)).ToArray();
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        return (state, center, ring1, ring2);
    }

    private static CorruptionController CreateController(WorldState state, GameClock clock, int seed = 1, PrestigeState? prestigeState = null)
    {
        var controller = new CorruptionController();
        controller.Initialize(state, clock, new GamePRNG(seed), prestigeState);
        return controller;
    }

    /// <summary>Complète une recherche sur la civilisation du joueur (mêmes modificateurs qu'en jeu).</summary>
    private static void CompleteResearch(WorldState state, TechnologyId id)
    {
        var tree = new TechnologyTree();
        tree.CompleteResearch(id);
        state.PlayerCivilization.AddCustomAggregator(tree);
    }

    // ── Production des Temples ──────────────────────────────────────────────

    [Fact]
    public void TempleLevel2_NoCorruptionOnTarget_CreatesDominionLevel1()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().SingleOrDefault();
        Assert.NotNull(dominion);
        Assert.Equal(1, dominion!.Level);
    }

    [Fact]
    public void TempleLevel2_ExistingDominionAtCap_DoesNotExceedTwiceTempleLevel()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Dominion(landHex, level: 4)); // cap = 2 * 2 = 4

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().Single();
        Assert.Equal(4, dominion.Level);
    }

    [Fact]
    public void TempleLevel4_ExistingDominionBelowCap_IncrementsUpToEight()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 4 });
        state.AddFeature(new Dominion(landHex, level: 7)); // cap = 2 * 4 = 8

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var dominion = state.GetFeaturesAt(landHex).OfType<Dominion>().Single();
        Assert.Equal(8, dominion.Level);
    }

    [Fact]
    public void TempleLevel2_CorruptionOnTarget_ReducesCorruptionInsteadOfCreatingDominion()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 3));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void TempleLevel2_CorruptionAtLevel1_RemovesFeatureOnceReducedToZero()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new Corruption(landHex, level: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
        Assert.False(state.HasFeaturesAt(landHex));
    }

    [Fact]
    public void TempleLevel1_BelowThreshold_DoesNotProduce()
    {
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 1 });

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
        city.AddBuilding(new Temple { Level = 5 });

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
        // Graine 3 : après l'annulation (dominion 4→3), le dominion (niveau 3, 30% de déclenchement)
        // tire lui-même une deuxième annulation à son propre tour — la graine 3 est vérifiée pour NE
        // PAS re-déclencher ce second tour (tirage 39 ≥ 30), ce qui isole une seule annulation.
        CreateController(state, clock, seed: 3);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, corruption.Level);
        Assert.Equal(3, dominion.Level);
    }

    [Fact]
    public void Spread_SameStatusLargeLevelGap_NeighborGainsSourceUnchanged()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var strong = new Dominion(a, level: 10); // 100% de déclenchement
        var weak = new Dominion(b, level: 1); // écart de 9 > 2
        state.AddFeature(strong);
        state.AddFeature(weak);

        var clock = new GameClock();
        clock.Start();
        // Graine 2 : après le gain (weak 1→2), weak (niveau 2, 20% de déclenchement) tire lui-même un
        // second débordement vers strong à son propre tour — la graine 2 est vérifiée pour NE PAS
        // re-déclencher ce second tour (tirage 26 ≥ 20), ce qui isole un seul débordement.
        CreateController(state, clock, seed: 2);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(10, strong.Level);
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

        Assert.Equal(10, strong.Level);
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

        Assert.Equal(10, strong.Level);
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
    public void Spread_LevelFourSourceSurroundedByRing_StabilizesAtGapOfTwo_SourceNeverIncreases()
    {
        // Régression du bug corrigé dans ProcessSpread : la comparaison utilisait Math.Abs(niveau
        // source - niveau voisin), si bien qu'un voisin FAIBLE, à son propre tour de débordement,
        // pouvait quand même faire grandir un voisin déjà PLUS FORT que lui (le centre) dès que
        // l'écart dépassait SpreadSameStatusLevelGap — permettant au centre de grimper sans plafond.
        // La comparaison doit être directionnelle : seule la source la plus forte fait grandir
        // l'autre. Ici le centre (niveau 4) doit s'entourer d'un anneau au niveau 2 (4 - 2, le
        // plafond) et s'arrêter là : le centre ne bouge jamais, et aucun hex du second anneau n'est
        // atteint (écart de 2 avec l'anneau au niveau 2, jamais > 2).
        var (state, center, ring1, ring2) = CreateTwoRingHexGridSetup();
        var source = new Dominion(center, level: 4);
        state.AddFeature(source);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, seed: 1);

        for (int i = 0; i < 3000; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(4, source.Level);

        foreach (var hex in ring1)
        {
            var dominion = state.GetFeaturesAt(hex).OfType<Dominion>().SingleOrDefault();
            Assert.NotNull(dominion);
            Assert.Equal(2, dominion!.Level);
        }

        foreach (var hex in ring2)
            Assert.False(state.HasFeaturesAt(hex));

        Assert.All(state.Features.OfType<Dominion>(), d => Assert.True(d.Level <= 4));
    }

    // ── Recherches de la Théocratie (Dogme de l'Emprise, Évangélisation, Terre Consacrée) ──

    [Fact]
    public void DominionLayerDivisor_WithoutDogmeDeLEmprise_DoublesPerLayer()
    {
        var civ = new Civilization { Index = 0 };

        Assert.Equal(1000, CorruptionController.GetDominionLayerDivisorMilli(civ, IslandMap.SurfaceLayer));
        Assert.Equal(2000, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.UnderworldZ));
        Assert.Equal(4000, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.AbyssZ));
        Assert.Equal(8000, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.PandemoniumZ));
    }

    [Fact]
    public void DominionLayerDivisor_WithDogmeDeLEmprise_UsesOnePointFivePerLayer()
    {
        var (state, _, _) = CreateSingleHexUnderworldCitySetup();
        CompleteResearch(state, TechnologyId.DogmeDeLEmprise);
        var civ = state.PlayerCivilization;

        Assert.Equal(1000, CorruptionController.GetDominionLayerDivisorMilli(civ, IslandMap.SurfaceLayer));
        Assert.Equal(1500, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.UnderworldZ));
        Assert.Equal(2250, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.AbyssZ));
        Assert.Equal(3375, CorruptionController.GetDominionLayerDivisorMilli(civ, LayerState.PandemoniumZ));
    }

    [Fact]
    public void TempleInUnderworld_WithDogmeDeLEmprise_ClearsMoreCorruption()
    {
        int withoutDogme = RunUnderworldTempleCorruptionClearing(withDogme: false);
        int withDogme = RunUnderworldTempleCorruptionClearing(withDogme: true);

        Assert.True(withDogme > withoutDogme, $"Dogme : {withDogme} points dissipés, sans : {withoutDogme}");
    }

    /// <summary>
    /// Points de Corruption dissipés par un Temple de l'Inframonde en 300 cycles, à graine identique :
    /// un tir sur deux aboutit sans le Dogme de l'Emprise (÷2), deux sur trois avec (÷1,5). Le niveau
    /// de départ est assez haut pour que la zone ne soit jamais entièrement nettoyée dans l'intervalle.
    /// </summary>
    private static int RunUnderworldTempleCorruptionClearing(bool withDogme)
    {
        const int cycles = 300;
        const int startLevel = 300;

        var (state, city, underworldHex) = CreateSingleHexUnderworldCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        var corruption = new Corruption(underworldHex, level: startLevel);
        state.AddFeature(corruption);
        if (withDogme)
            CompleteResearch(state, TechnologyId.DogmeDeLEmprise);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, seed: 25555);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks * cycles);

        return startLevel - corruption.Level;
    }

    [Fact]
    public void Spread_DominionLevel2_WithoutEvangelisation_DoesNotTrigger()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var dominion = new Dominion(a, level: 2); // 20% de déclenchement, tirage 20 (graine 25555) → pas de débordement
        var corruption = new Corruption(b, level: 1); // 10%, tirage 44 → pas de débordement
        state.AddFeature(dominion);
        state.AddFeature(corruption);

        var clock = new GameClock();
        clock.Start();
        // Une petite graine (1, 2, 3, …) donne toujours un premier tirage quasi nul avec ce PRNG
        // (Lehmer/Park-Miller) — impropre à démontrer un non-déclenchement. 25555 est la plus petite
        // graine vérifiée à donner tirage ≥ 20 puis ≥ 10 (les deux seuils de ce scénario).
        CreateController(state, clock, seed: 25555);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(2, dominion.Level);
        Assert.Equal(1, corruption.Level);
    }

    [Fact]
    public void Spread_DominionLevel3_WithEvangelisation_TriggersAtFifteenPercentPerLevel()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var dominion = new Dominion(a, level: 3); // 3 × (10+5) = 45% de déclenchement, tirage 31 (graine 1) → débordement
        var corruption = new Corruption(b, level: 1);
        state.AddFeature(dominion);
        state.AddFeature(corruption);
        CompleteResearch(state, TechnologyId.Evangelisation);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(2, dominion.Level);
        Assert.Empty(state.GetFeaturesAt(b).OfType<Corruption>());
    }

    [Fact]
    public void Spread_MutualAnnulation_TempleProtection_DominionSpared()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: 10); // 100% de déclenchement
        var dominion = new Dominion(b, level: 4);
        state.AddFeature(corruption);
        state.AddFeature(dominion);

        // Ville du joueur touchant b (mais pas a) avec un Temple niveau 1 (aucune production, donc
        // aucune consommation du PRNG par ProcessTempleProduction) ; chance de protection forcée à
        // 100% pour rendre le tirage de Terre Consacrée déterministe.
        var city = new City(Vertex.Create(b, new HexCoord(0, 1, IslandMap.SurfaceLayer), new HexCoord(1, 1, IslandMap.SurfaceLayer)))
        { CivilizationIndex = 0 };
        city.AddBuilding(new Temple { Level = 1 });
        state.PlayerCivilization.AddCity(city);
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, Modifier.EType.ADDITIVE, 1.0),
        }));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        // Le Dominion protégé ne perd jamais de niveau ; la Corruption, elle, perd le sien à chaque
        // annulation (1 ou 2 fois selon le tirage de débordement du Dominion lui-même).
        Assert.Equal(4, dominion.Level);
        Assert.True(corruption.Level < 10);
    }

    [Fact]
    public void Spread_MutualAnnulation_ProtectionWithoutTemple_DominionStillReduced()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: 10); // 100% de déclenchement
        var dominion = new Dominion(b, level: 4);
        state.AddFeature(corruption);
        state.AddFeature(dominion);

        // Chance de protection maximale mais aucune ville avec Temple : la protection ne s'applique pas.
        state.PlayerCivilization.AddCustomAggregator(new StaticModifierProvider(new[]
        {
            new Modifier(Modifier.ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, Modifier.EType.ADDITIVE, 1.0),
        }));

        var clock = new GameClock();
        clock.Start();
        // Graine 3 : même raisonnement que Spread_OppositeStatusNeighbor_BothReduceByOne — isole une
        // seule annulation (le second tour du dominion, niveau 3, ne re-déclenche pas : tirage 39 ≥ 30).
        CreateController(state, clock, seed: 3);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, corruption.Level);
        Assert.Equal(3, dominion.Level);
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

    // ── Spire de Corruption / Faille des Abysses : aucune protection, décroissance garantie ──

    [Fact]
    public void MonumentDecay_ReducesCorruptionOnSpireHex_Guaranteed()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
    }

    [Fact]
    public void MonumentDecay_DoesNotReduceCorruptionOnSpireHex_WhileUnderConstruction()
    {
        // Tant que la Spire n'est pas achevée (Built = false), elle ne réduit pas encore la corruption,
        // y compris sur son propre hex.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
    }

    [Fact]
    public void MonumentDecay_ReducesCorruptionUnderAbyssGate_Guaranteed()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new AbyssGate(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level);
    }

    [Fact]
    public void MonumentDecay_ClearingToZero_RecordsPeakLevelNotFinalLevel()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        // Simule une zone qui a grimpé jusqu'au niveau 5 avant d'être ramenée à 1 par un autre biais :
        // c'est le pic (5), pas le niveau final au moment du nettoyage (1), qui doit être enregistré.
        state.AddFeature(new Corruption(landHex, level: 1) { PeakLevel = 5 });
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });

        var prestigeState = new PrestigeState();
        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: prestigeState);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
        Assert.Equal(5, prestigeState.MaxCorruptionLevelCleared);
    }

    [Fact]
    public void SpireHex_TempleProduction_CanStillCreateDominion()
    {
        // La Spire de Corruption ne protège plus son hex : un Temple peut toujours y poser du Dominion.
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new CorruptionSpire(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Single(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void SpireHex_Spread_SourceNotProtected_CanSpreadOut()
    {
        // La Spire de Corruption ne protège plus son hex : le débordement peut s'y produire normalement.
        // Rayon mis à 0 pour isoler ce mécanisme de la décroissance garantie (qui, à rayon ≥ 1,
        // effacerait immédiatement la Corruption fraîchement débordée sur le voisin b).
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: 10); // 100% de déclenchement si non protégé
        state.AddFeature(corruption);
        state.AddFeature(new CorruptionSpire(a) { Radius = 0 });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.True(state.HasFeaturesAt(b)); // débordement possible depuis l'hex de la Spire
    }

    [Fact]
    public void SpireHex_Spread_TargetNotProtected_DominionCanBeSeeded()
    {
        // La Spire de Corruption ne protège plus son hex : elle peut recevoir du Dominion débordé.
        var (state, a, b) = CreateTwoLandHexesSetup();
        state.AddFeature(new Dominion(a, level: 10)); // 100% de déclenchement si non protégé
        state.AddFeature(new CorruptionSpire(b));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Single(state.GetFeaturesAt(b).OfType<Dominion>());
    }

    [Fact]
    public void AbyssGateHex_TempleProduction_CanStillCreateDominion()
    {
        // La Faille des Abysses ne protège plus son hex non plus : un Temple peut toujours y poser du Dominion.
        var (state, city, landHex) = CreateSingleLandHexCitySetup();
        city.AddBuilding(new Temple { Level = 2 });
        state.AddFeature(new AbyssGate(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // sentinel : initialise LastDominionProductionTick (coldStartOnZero)
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Single(state.GetFeaturesAt(landHex).OfType<Dominion>());
    }

    [Fact]
    public void AbyssGateHex_Spread_SourceNotProtected_CanSpreadOut()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        state.AddFeature(new Corruption(a, level: 10)); // 100% de déclenchement
        state.AddFeature(new AbyssGate(a));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.True(state.HasFeaturesAt(b)); // débordement possible depuis l'hex de la Faille
    }

    [Fact]
    public void AbyssGateHex_Spread_TargetNotProtected_DominionCanBeSeeded()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        state.AddFeature(new Dominion(a, level: 10)); // 100% de déclenchement
        state.AddFeature(new AbyssGate(b));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Single(state.GetFeaturesAt(b).OfType<Dominion>());
    }

    [Fact]
    public void MonumentDecay_ReducesCorruptionOnSpireNeighbor_WithinRadius()
    {
        var (state, a, b) = CreateTwoLandHexesSetup();
        // Niveau 2 : 20% de déclenchement du débordement, tirage 31 (graine 1) → pas de débordement
        // ce tick, ce qui isole la décroissance garantie de la Spire de tout autre effet.
        state.AddFeature(new Corruption(b, level: 2)); // sur le voisin de la Spire, pas sur son propre hex
        state.AddFeature(new CorruptionSpire(a) { Radius = 1, Built = true });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(b).OfType<Corruption>().Single();
        Assert.Equal(1, corruption.Level); // rayon 1 : le voisin immédiat est couvert
    }

    [Fact]
    public void MonumentDecay_DoesNotReachHexesBeyondRadius()
    {
        var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
        var farHex = new HexCoord(2, 0, IslandMap.SurfaceLayer); // à distance 2 de a
        var tiles = new[] { new HexTile(a, TerrainType.Plain), new HexTile(b, TerrainType.Plain), new HexTile(farHex, TerrainType.Plain) };
        var map = new IslandMap(tiles);
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        // Niveau 2 : 20% de déclenchement du débordement, tirage 31 (graine 1) → pas de débordement
        // ce tick, ce qui isole la décroissance garantie de la Spire de tout autre effet.
        state.AddFeature(new Corruption(farHex, level: 2));
        state.AddFeature(new CorruptionSpire(a) { Radius = 1, Built = true }); // rayon 1 : n'atteint pas farHex (distance 2)

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        var corruption = state.GetFeaturesAt(farHex).OfType<Corruption>().Single();
        Assert.Equal(2, corruption.Level); // hors rayon : aucune décroissance garantie
    }

    // ── Éligibilité de la Faille des Abysses : basée sur le nettoyage, n'importe où ────────

    [Fact]
    public void ReduceLevel_ClearingCorruptionViaDominionAnnulation_MakesAbyssGateEligible_OnUnrelatedHex()
    {
        // AbyssGateController.IsAbyssGateEligible se base sur RunRecord.MaxCorruptionLevelCleared
        // (le record global PrestigeState.MaxCorruptionLevelCleared, mis à jour en parallèle, ne sert
        // qu'au bonus de prestige), ici alimenté par annulation mutuelle avec le Dominion (pas par
        // Temple ni par la décroissance de la Spire) et sur un hex qui n'a AUCUN rapport avec celui de
        // la Spire — l'éligibilité doit être vraie quel que soit l'hex nettoyé et quel que soit le
        // mécanisme de nettoyage.
        var (state, a, b) = CreateTwoLandHexesSetup();
        var corruption = new Corruption(a, level: AbyssGate.RequiredCorruptionLevel);
        var dominion = new Dominion(b, level: 20); // 200% de déclenchement : annule toujours son tour
        state.AddFeature(corruption);
        state.AddFeature(dominion);

        // Spire déjà bâtie sur un tout autre hex, sans lien avec l'annulation ci-dessus.
        var spireHex = new HexCoord(50, 50, IslandMap.SurfaceLayer);
        state.AddFeature(new CorruptionSpire(spireHex) { Built = true });

        var prestigeState = new PrestigeState();
        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, seed: 1, prestigeState: prestigeState);

        // Le Dominion (niveau 20) annule à coup sûr chaque intervalle : largement assez pour ramener
        // la Corruption (niveau initial = seuil requis) à 0.
        for (int i = 0; i < 8; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(a).OfType<Corruption>());
        Assert.True(state.RunRecord.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel);
        Assert.True(prestigeState.MaxCorruptionLevelCleared >= AbyssGate.RequiredCorruptionLevel);
        Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateEligible && e.Toast);

        var gateController = new AbyssGateController();
        gateController.Initialize(state, clock);
        Assert.True(gateController.IsAbyssGateEligible());
    }

    // ── Os Divins : générateurs de Corruption tant qu'ils ne sont pas purifiés ─────────────
    // La carte à un seul hex isole ce mécanisme du débordement (aucun voisin candidat).

    [Fact]
    public void DivineBones_RaiseCorruptionOnTheirOwnHex_EachInterval()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3)); // plafond 6

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
        Assert.Equal(3, corruption.PeakLevel); // le pic engendré compte pour le record de nettoyage
    }

    [Fact]
    public void DivineBones_SeedCorruption_WhenTheirHexIsClean()
    {
        // Hex sain (une Spire voisine a pu le nettoyer) : les Os y resèment une poche de niveau 1.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 2));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_StopRaisingCorruption_AtTwiceTheIslandCorruptionLevel()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        var bones = new DivineBones(landHex, corruptionLevel: 2); // plafond 4
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(bones);

        Assert.Equal(4, bones.GetCorruptionCap());

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 10; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(4, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_DoNotReduceCorruptionAlreadyAboveTheirCap()
    {
        // Le plafond ne borne que la génération : une Corruption plus élevée (tirage initial de
        // l'Abysse, débordement d'un voisin) est laissée telle quelle, jamais rabaissée.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 9));
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 2)); // plafond 4

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_Purified_GenerateNoCorruption()
    {
        // En jeu, des Os purifiés sont retirés de la carte (DivineBonesController.ProcessInvestment) ;
        // l'état transitoire ne doit de toute façon plus rien engendrer.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3) { Purified = true });

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void DivineBones_UnderBuiltSpire_CancelOutItsDecay()
    {
        // Décroissance garantie de la Spire (-1) puis génération des Os (+1) dans le même intervalle :
        // la Corruption reste figée tant que les Os ne sont pas purifiés.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3)); // plafond 6

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    // ── Sources de Corruption : générateurs de Corruption, sans le doublement de plafond des Os Divins ──
    // La carte à un seul hex isole ce mécanisme du débordement (aucun voisin candidat).

    [Fact]
    public void CorruptionSource_RaisesCorruptionOnItsOwnHex_EachInterval()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 3)); // plafond 3, pas 6

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
        Assert.Equal(3, corruption.PeakLevel); // le pic engendré compte pour le record de nettoyage
    }

    [Fact]
    public void CorruptionSource_SeedsCorruption_WhenItsHexIsClean()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 2));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void CorruptionSource_StopsRaisingCorruption_AtTheIslandCorruptionLevel_NotTwice()
    {
        // Contrairement aux Os Divins (plafond ×2), la Source s'arrête exactement au niveau de
        // corruption de l'île figé à sa génération.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        var source = new CorruptionSource(landHex, corruptionLevel: 2); // plafond 2, pas 4
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(source);

        Assert.Equal(2, source.GetCorruptionCap());

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 10; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void CorruptionSource_DoesNotReduceCorruptionAlreadyAboveItsCap()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 9));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 2)); // plafond 2

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    // ── Invariant : Corruption et Dominion ne coexistent jamais sur le même hex ────────────
    // Reproduit le bug signalé : une Source de Corruption (ou un Os Divin/monstre) ne doit jamais
    // semer de la Corruption sur un hex déjà occupé par du Dominion — elle doit d'abord le combattre.

    [Fact]
    public void CorruptionSource_FightsExistingDominion_InsteadOfSeedingCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 4));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Dominion>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void CorruptionSource_KeepsFightingDominion_UntilItIsGone_ThenSeedsCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 2));
        state.AddFeature(new CorruptionSource(landHex, corruptionLevel: 1));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // Dominion 2 -> 1
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks); // Dominion 1 -> 0 (supprimé)

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());

        // Le Dominion enfin parti, la Source recommence à semer normalement.
        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void DivineBones_FightsExistingDominion_InsteadOfSeedingCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 1));
        state.AddFeature(new DivineBones(landHex, corruptionLevel: 3));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void Monster_FightsExistingDominion_InsteadOfSeedingCorruption()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Dominion(landHex, level: 1));
        state.AddFeature(new DemonGod(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 2 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Dominion>());
        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    // ── Tentacules et Dieu démon : générateurs de Corruption tant qu'ils sont vivants ──────
    // Même mécanique que les Os Divins, avec le plafond calculé sur le niveau de corruption courant.

    [Fact]
    public void Tentacle_RaisesCorruptionOnItsOwnHex_EachInterval()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new Tentacle(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 3 }); // plafond 6

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(2, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        var corruption = state.GetFeaturesAt(landHex).OfType<Corruption>().Single();
        Assert.Equal(3, corruption.Level);
        Assert.Equal(3, corruption.PeakLevel); // le pic engendré compte pour le record de nettoyage
    }

    [Fact]
    public void DemonGod_SeedsCorruption_WhenItsHexIsClean()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new DemonGod(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 2 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void Monsters_StopRaisingCorruption_AtTwiceTheIslandCorruptionLevel()
    {
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 1));
        state.AddFeature(new DemonGod(landHex));

        var clock = new GameClock();
        clock.Start();
        var controller = CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 4 });

        Assert.Equal(8, controller.GetMonsterCorruptionCap());

        for (int i = 0; i < 20; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(8, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void Monsters_DoNotReduceCorruptionAlreadyAboveTheirCap()
    {
        // Le plafond ne borne que la génération : le tirage initial d'une île de l'Abysse peut déjà
        // dépasser 2× le niveau de corruption, il est laissé tel quel.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 9));
        state.AddFeature(new Tentacle(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 2 }); // plafond 4

        for (int i = 0; i < 3; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(9, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void MonsterRemovedFromMap_GeneratesNoMoreCorruption()
    {
        // Abattre la Tentacule tarit la source ; la Corruption déjà semée reste à nettoyer.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        var tentacle = new Tentacle(landHex);
        state.AddFeature(tentacle);

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 5 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);
        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);

        state.RemoveFeature(tentacle);
        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(1, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void OtherMonsters_GenerateNoCorruption()
    {
        // L'opt-in ne concerne que les monstres enracinés dans la Corruption (Tentacule, Dieu démon).
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new MajorDemon(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 5 });

        clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Empty(state.GetFeaturesAt(landHex).OfType<Corruption>());
    }

    [Fact]
    public void Tentacle_UnderBuiltSpire_CancelsOutItsDecay()
    {
        // Décroissance garantie de la Spire (-1) puis génération de la Tentacule (+1) dans le même
        // intervalle : la Corruption reste figée tant que la Tentacule est vivante.
        var (state, _, landHex) = CreateSingleLandHexCitySetup();
        state.AddFeature(new Corruption(landHex, level: 3));
        state.AddFeature(new CorruptionSpire(landHex) { Built = true });
        state.AddFeature(new Tentacle(landHex));

        var clock = new GameClock();
        clock.Start();
        CreateController(state, clock, prestigeState: new PrestigeState { CurrentCorruptionLevel = 3 }); // plafond 6

        for (int i = 0; i < 5; i++)
            clock.SimulateAdvance(CorruptionController.ProductionIntervalTicks);

        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
    }

    // ── Corruption semée à l'apparition d'un monstre enraciné ─────────────────────────────

    /// <summary>Carte pleine de rayon 2 autour de l'origine — de quoi observer les 6 voisins d'un monstre placé au centre.</summary>
    private static (WorldState state, HexCoord center) CreateRadius2MapSetup(TerrainType centerTerrain = TerrainType.Plain)
    {
        var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var tiles = new List<HexTile>();
        for (int q = -2; q <= 2; q++)
            for (int r = System.Math.Max(-2, -q - 2); r <= System.Math.Min(2, -q + 2); r++)
                tiles.Add(new HexTile(new HexCoord(q, r, IslandMap.SurfaceLayer), centerTerrain));

        var map = new IslandMap(tiles.ToArray());
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
        return (state, center);
    }

    [Fact]
    public void NewMonster_CorruptsItsHexAndAllSixNeighbours_ToTheIslandCorruptionLevel()
    {
        var (state, center) = CreateRadius2MapSetup();
        var tentacle = new Tentacle(center);
        state.AddFeature(tentacle);

        CorruptionController.SeedCorruptionAroundNewMonster(state, tentacle, islandCorruptionLevel: 5);

        foreach (var hex in center.Neighbors().Append(center))
        {
            var corruption = state.GetFeaturesAt(hex).OfType<Corruption>().Single();
            Assert.Equal(5, corruption.Level);
            Assert.Equal(5, corruption.PeakLevel);
        }

        // Le deuxième anneau reste sain : seul le voisinage immédiat est semé.
        Assert.Empty(state.GetFeaturesAt(new HexCoord(2, 0, IslandMap.SurfaceLayer)).OfType<Corruption>());
    }

    [Fact]
    public void NewMonster_SeedsHalfOfItsGenerationCap()
    {
        // Le niveau semé est exactement la moitié du plafond que la génération continue atteindra.
        var (state, center) = CreateRadius2MapSetup();
        var god = new DemonGod(center);
        state.AddFeature(god);

        var clock = new GameClock();
        clock.Start();
        var prestigeState = new PrestigeState { CurrentCorruptionLevel = 4 };
        var controller = CreateController(state, clock, prestigeState: prestigeState);

        CorruptionController.SeedCorruptionAroundNewMonster(state, god, prestigeState.CurrentCorruptionLevel);

        Assert.Equal(8, controller.GetMonsterCorruptionCap());
        Assert.Equal(4, state.GetFeaturesAt(center).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void NewMonster_NeverLowersAnAlreadyDeeperCorruption()
    {
        var (state, center) = CreateRadius2MapSetup();
        var neighbour = center.Neighbors()[0];
        state.AddFeature(new Corruption(center, level: 9));
        state.AddFeature(new Corruption(neighbour, level: 2));

        var tentacle = new Tentacle(center);
        state.AddFeature(tentacle);

        CorruptionController.SeedCorruptionAroundNewMonster(state, tentacle, islandCorruptionLevel: 5);

        Assert.Equal(9, state.GetFeaturesAt(center).OfType<Corruption>().Single().Level);
        Assert.Equal(5, state.GetFeaturesAt(neighbour).OfType<Corruption>().Single().Level);
    }

    [Fact]
    public void NewMonster_SkipsVoidAndMissingHexes()
    {
        // Un hex de Void n'est jamais rendu ni interactif (cf. PlaceAbyssCorruption) ; un hex sans
        // tuile n'existe pas du tout.
        var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var voidHex = center.Neighbors()[0];
        var landHex = center.Neighbors()[1];
        var missingHex = center.Neighbors()[2];

        var map = new IslandMap(new[]
        {
            new HexTile(center, TerrainType.Plain),
            new HexTile(voidHex, TerrainType.Void),
            new HexTile(landHex, TerrainType.Plain),
        });
        var civ = new Civilization { Index = 0 };
        var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

        var tentacle = new Tentacle(center);
        state.AddFeature(tentacle);

        CorruptionController.SeedCorruptionAroundNewMonster(state, tentacle, islandCorruptionLevel: 3);

        Assert.Equal(3, state.GetFeaturesAt(center).OfType<Corruption>().Single().Level);
        Assert.Equal(3, state.GetFeaturesAt(landHex).OfType<Corruption>().Single().Level);
        Assert.Empty(state.GetFeaturesAt(voidHex).OfType<Corruption>());
        Assert.Empty(state.GetFeaturesAt(missingHex).OfType<Corruption>());
    }

    [Fact]
    public void NewMonster_WithoutCorruptionGeneration_SeedsNothing()
    {
        var (state, center) = CreateRadius2MapSetup();
        var demon = new MajorDemon(center);
        state.AddFeature(demon);

        CorruptionController.SeedCorruptionAroundNewMonster(state, demon, islandCorruptionLevel: 5);

        Assert.Empty(state.Features.OfType<Corruption>());
    }
}
