using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Apparition périodique de monstres en bordure de carte (toutes les 3 000 / niveau de
    /// corruption global ticks, 5 % de chance) sur les couches gérées par AutoExtendController.
    /// Sur l'Inframonde, le type tiré dépend du niveau de corruption global de l'île : (niveau - 1)%
    /// de chance d'un démon mineur, sinon 65 % troll / 35 % ogre. Sur l'Abysse, uniquement des
    /// démons mineurs/majeurs : le démon majeur a 5% de chance à partir du niveau de corruption 5 de
    /// l'hex tiré, +2%/niveau au-delà ; le reste est toujours démon mineur.
    ///
    /// Carte de test : triangle d'arrivée (0,0)/(1,0)/(0,1) + un unique hexagone supplémentaire à
    /// distance 2 de l'arrivée, qui est donc le seul hexagone "en bordure" éligible (les hexagones
    /// du triangle d'arrivée sont exclus par la distance minimale).
    /// </summary>
    public class AutoExtendBorderMonsterTests
    {
        private static HexCoord Arrival1 => new(0, 0, LayerState.UnderworldZ);
        private static HexCoord Arrival2 => new(1, 0, LayerState.UnderworldZ);
        private static HexCoord Arrival3 => new(0, 1, LayerState.UnderworldZ);
        private static HexCoord BorderHex => new(3, 0, LayerState.UnderworldZ);

        private static HexCoord AbyssArrival1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord AbyssArrival2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord AbyssArrival3 => new(0, 1, LayerState.AbyssZ);
        private static HexCoord AbyssBorderHex => new(3, 0, LayerState.AbyssZ);

        private static (WorldState state, GameClock clock, LayerState layer) CreateSetup(GamePRNG prng, PrestigeState? prestigeState = null)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var arrivalVertex = Vertex.Create(Arrival1, Arrival2, Arrival3);
            var underworldTiles = new List<HexTile>
            {
                new(Arrival1, TerrainType.Mountain),
                new(Arrival2, TerrainType.Mountain),
                new(Arrival3, TerrainType.Mountain),
                new(BorderHex, TerrainType.Mountain),
            };
            var layer = new LayerState(new IslandMap(underworldTiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex };
            state.AddLayer(LayerState.UnderworldZ, layer);

            var controller = new AutoExtendController();
            var clock = new GameClock();
            clock.Start();
            controller.Initialize(state, prng, clock, prestigeState);

            return (state, clock, layer);
        }

        /// <summary>Même dispositif que <see cref="CreateSetup"/>, mais sur la couche Abysse, avec une
        /// feature Corruption déjà posée sur le hex de bordure (comme le fait réellement
        /// PlaceAbyssCorruption sur chaque hex de terre de l'Abysse).</summary>
        private static (WorldState state, GameClock clock, LayerState layer) CreateAbyssSetup(
            GamePRNG prng, int corruptionLevelOnBorderHex, PrestigeState? prestigeState = null)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var arrivalVertex = Vertex.Create(AbyssArrival1, AbyssArrival2, AbyssArrival3);
            var abyssTiles = new List<HexTile>
            {
                new(AbyssArrival1, TerrainType.Mountain),
                new(AbyssArrival2, TerrainType.Mountain),
                new(AbyssArrival3, TerrainType.Mountain),
                new(AbyssBorderHex, TerrainType.Mountain),
            };
            var layer = new LayerState(new IslandMap(abyssTiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex };
            state.AddLayer(LayerState.AbyssZ, layer);
            state.AddFeature(new SettlersOfIdlestan.Model.IslandFeatures.Corruption(AbyssBorderHex, corruptionLevelOnBorderHex));

            var controller = new AutoExtendController();
            var clock = new GameClock();
            clock.Start();
            controller.Initialize(state, prng, clock, prestigeState);

            return (state, clock, layer);
        }

        /// <summary>
        /// PrestigeState d'une île située au-delà de l'anneau sûr des premières îles
        /// (AutoExtendController.UnderworldSafeRadiusBonusByIsland : +8 / +6 / +4 / +2 hexagones de
        /// distance minimale au point d'arrivée sur les quatre premières). Sans cela, ces tests
        /// mesureraient cet anneau et non le tirage d'apparition : l'unique hexagone de bordure de
        /// cette carte est à 3 hexagones de l'arrivée, donc dans la zone sûre des quatre premières
        /// îles. Le numéro d'île se lit sur le nombre de prestiges déjà enregistrés.
        /// </summary>
        private static PrestigeState LateIslandPrestigeState(int corruptionLevel = 1)
        {
            var prestigeState = new PrestigeState { CurrentCorruptionLevel = corruptionLevel };
            for (int i = 0; i < 4; i++)
                prestigeState.RunHistory.Add(new PrestigeRunStats());
            return prestigeState;
        }

        /// <summary>Cherche la plus petite graine pour laquelle la séquence de tirages d'un GamePRNG frais satisfait le prédicat.</summary>
        private static int FindSeed(Func<GamePRNG, bool> predicate)
        {
            for (int seed = 0; seed < 20_000; seed++)
                if (predicate(new GamePRNG(seed)))
                    return seed;
            throw new InvalidOperationException("Aucune graine trouvée pour ce prédicat.");
        }

        [Fact]
        public void NoMonster_BeforeCheckIntervalElapses()
        {
            // Corruption par défaut = 1 → intervalle = 3 000 ticks. Graine garantissant un tirage
            // d'apparition réussi (<5%) au prochain check, pour vérifier qu'il ne se produit pas
            // avant que l'intervalle ne soit écoulé.
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, _) = CreateSetup(new GamePRNG(seed), LateIslandPrestigeState());

            clock.SimulateAdvance(2_900);

            Assert.Empty(state.Features.OfType<MonsterFeature>());
        }

        [Fact]
        public void SpawnsTrollOrOgreOnBorderHex_AtLowCorruption_WhenRollSucceeds()
        {
            // Niveau de corruption 1 → chance de démon = 0% (toujours faux) → troll/ogre uniquement.
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, _) = CreateSetup(new GamePRNG(seed), LateIslandPrestigeState(corruptionLevel: 1));

            clock.SimulateAdvance(6_000);

            var monster = Assert.Single(state.Features.OfType<MonsterFeature>());
            Assert.True(monster is Troll or Ogre, $"Attendu Troll ou Ogre, obtenu {monster.GetType().Name}");
            Assert.Equal(BorderHex, monster.Position);
        }

        [Fact]
        public void SpawnsMinorDemon_AtHighCorruption_WhenRollsSucceed()
        {
            // Niveau de corruption 100 → chance de démon = 99% : pratiquement garanti avec la
            // graine choisie (on vérifie explicitement les deux tirages dans le prédicat).
            int seed = FindSeed(rng => rng.Next(100) < 5 && rng.Next(100) < 99);
            var (state, clock, _) = CreateSetup(new GamePRNG(seed), LateIslandPrestigeState(corruptionLevel: 100));

            clock.SimulateAdvance(6_000);

            var monster = Assert.Single(state.Features.OfType<MonsterFeature>());
            Assert.IsType<MinorDemon>(monster);
            Assert.Equal(BorderHex, monster.Position);
        }

        /// <summary>
        /// Anneau sûr : sur la première île, l'unique hexagone de bordure de cette carte est à
        /// 3 hexagones de l'arrivée, donc sous la distance minimale de 2 + 8. Aucun monstre n'y
        /// apparaît, même quand le tirage d'apparition réussit. Voir
        /// AutoExtendController.UnderworldSafeRadiusBonusByIsland — un monstre stérilise son
        /// hexagone, et une civilisation qui démarre sous terre n'a de quoi en déloger aucun.
        /// </summary>
        [Fact]
        public void NoMonster_OnUnderworld_OnTheFirstIsland()
        {
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, _) = CreateSetup(new GamePRNG(seed), new PrestigeState { CurrentCorruptionLevel = 1 });

            clock.SimulateAdvance(6_000);

            Assert.Empty(state.Features.OfType<MonsterFeature>());
        }

        /// <summary>
        /// L'anneau sûr est propre à l'Inframonde : l'Abysse, qu'on ne visite jamais avant d'avoir
        /// une civilisation debout, garde sa distance minimale ordinaire dès la première île.
        /// </summary>
        [Fact]
        public void SpawnsDemon_OnAbyss_EvenOnTheFirstIsland()
        {
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, _) = CreateAbyssSetup(new GamePRNG(seed), corruptionLevelOnBorderHex: 1, new PrestigeState());

            clock.SimulateAdvance(6_000);

            Assert.Single(state.Features.OfType<MonsterFeature>());
        }

        [Fact]
        public void NoMonster_WhenLayerIsNotAutoExtended()
        {
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, layer) = CreateSetup(new GamePRNG(seed), LateIslandPrestigeState());
            layer.AutoExtend = false;

            clock.SimulateAdvance(6_000);

            Assert.Empty(state.Features.OfType<MonsterFeature>());
        }

        [Fact]
        public void SpawnsMinorDemon_OnAbyss_BelowMajorDemonCorruptionThreshold()
        {
            // Corruption 4 (< seuil de 5) → chance de démon majeur = 0% → toujours démon mineur.
            int seed = FindSeed(rng => rng.Next(100) < 5 && rng.Next(100) >= 0);
            var (state, clock, _) = CreateAbyssSetup(new GamePRNG(seed), corruptionLevelOnBorderHex: 4);

            clock.SimulateAdvance(6_000);

            var monster = Assert.Single(state.Features.OfType<MonsterFeature>());
            Assert.IsType<MinorDemon>(monster);
            Assert.Equal(AbyssBorderHex, monster.Position);
        }

        [Fact]
        public void SpawnsMajorDemon_OnAbyss_AtHighCorruption_WhenRollsSucceed()
        {
            // Corruption 50 → chance de démon majeur = 5 + 2*(50-5) = 95% : pratiquement garanti
            // avec la graine choisie (on vérifie explicitement les deux tirages dans le prédicat).
            int seed = FindSeed(rng => rng.Next(100) < 5 && rng.Next(100) < 95);
            var (state, clock, _) = CreateAbyssSetup(new GamePRNG(seed), corruptionLevelOnBorderHex: 50);

            clock.SimulateAdvance(6_000);

            var monster = Assert.Single(state.Features.OfType<MonsterFeature>());
            Assert.IsType<MajorDemon>(monster);
            Assert.Equal(AbyssBorderHex, monster.Position);
        }

        /// <summary>
        /// La chance de tirage de bordure (5%) est doublée dans l'Abysse : une graine dont le premier
        /// tirage tombe entre 5% (inclus) et 10% (exclu) — donc raté sur l'Inframonde mais réussi sur
        /// l'Abysse — doit produire un monstre sur l'Abysse et aucun sur l'Inframonde.
        /// </summary>
        [Fact]
        public void BorderMonsterSpawnChance_IsDoubled_OnAbyss()
        {
            int seed = FindSeed(rng => rng.Next(100) is >= 5 and < 10);

            var (underworldState, underworldClock, _) = CreateSetup(new GamePRNG(seed), LateIslandPrestigeState());
            underworldClock.SimulateAdvance(6_000);
            Assert.Empty(underworldState.Features.OfType<MonsterFeature>());

            var (abyssState, abyssClock, _) = CreateAbyssSetup(new GamePRNG(seed), corruptionLevelOnBorderHex: 1);
            abyssClock.SimulateAdvance(6_000);
            Assert.Single(abyssState.Features.OfType<MonsterFeature>());
        }

        [Fact]
        public void NeverSpawnsTrollOrOgre_OnAbyss()
        {
            // Quel que soit le niveau de corruption, l'Abysse ne fait apparaître que des démons.
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, _) = CreateAbyssSetup(new GamePRNG(seed), corruptionLevelOnBorderHex: 1);

            clock.SimulateAdvance(6_000);

            var monster = Assert.Single(state.Features.OfType<MonsterFeature>());
            Assert.True(monster is MinorDemon or MajorDemon, $"Attendu MinorDemon ou MajorDemon, obtenu {monster.GetType().Name}");
        }
    }
}
