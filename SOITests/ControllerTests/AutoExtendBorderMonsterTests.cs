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
    /// Apparition périodique de monstres en bordure de carte (toutes les 6 000 ticks, 5 % de
    /// chance) sur les couches gérées par AutoExtendController. Le type tiré dépend du niveau de
    /// corruption de l'île : (niveau - 1)% de chance d'un démon mineur, sinon 65 % troll / 35 % ogre.
    ///
    /// Carte de test : triangle d'arrivée (0,0)/(1,0)/(0,1) sur l'Inframonde + un unique hexagone
    /// supplémentaire à distance 2 de l'arrivée, qui est donc le seul hexagone "en bordure"
    /// éligible (les hexagones du triangle d'arrivée sont exclus par la distance minimale).
    /// </summary>
    public class AutoExtendBorderMonsterTests
    {
        private static HexCoord Arrival1 => new(0, 0, LayerState.UnderworldZ);
        private static HexCoord Arrival2 => new(1, 0, LayerState.UnderworldZ);
        private static HexCoord Arrival3 => new(0, 1, LayerState.UnderworldZ);
        private static HexCoord BorderHex => new(3, 0, LayerState.UnderworldZ);

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
            // Graine garantissant un tirage d'apparition réussi (<5%) au prochain check à 6 000 ticks.
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, _) = CreateSetup(new GamePRNG(seed));

            clock.SimulateAdvance(3_000);

            Assert.Empty(state.Features.OfType<MonsterFeature>());
        }

        [Fact]
        public void SpawnsTrollOrOgreOnBorderHex_AtLowCorruption_WhenRollSucceeds()
        {
            // Niveau de corruption 1 → chance de démon = 0% (toujours faux) → troll/ogre uniquement.
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var prestigeState = new PrestigeState { CurrentCorruptionLevel = 1 };
            var (state, clock, _) = CreateSetup(new GamePRNG(seed), prestigeState);

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
            var prestigeState = new PrestigeState { CurrentCorruptionLevel = 100 };
            var (state, clock, _) = CreateSetup(new GamePRNG(seed), prestigeState);

            clock.SimulateAdvance(6_000);

            var monster = Assert.Single(state.Features.OfType<MonsterFeature>());
            Assert.IsType<MinorDemon>(monster);
            Assert.Equal(BorderHex, monster.Position);
        }

        [Fact]
        public void NoMonster_WhenLayerIsNotAutoExtended()
        {
            int seed = FindSeed(rng => rng.Next(100) < 5);
            var (state, clock, layer) = CreateSetup(new GamePRNG(seed));
            layer.AutoExtend = false;

            clock.SimulateAdvance(6_000);

            Assert.Empty(state.Features.OfType<MonsterFeature>());
        }
    }
}
