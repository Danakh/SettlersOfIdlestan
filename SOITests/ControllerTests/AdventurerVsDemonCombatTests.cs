using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Vérifie l'équilibrage du combat Aventurier ↔ Démon (voir
    /// MonsterFeatureController.AttackNearbyMonster) à travers quelques repères de niveau :
    /// un Aventurier niveau 1 est censé perdre face à un Démon mineur au niveau de corruption 4,
    /// un niveau 4 doit pouvoir le vaincre, et face à un Démon majeur (deux fois plus résistant,
    /// même formule de niveau) un seul niveau 4 ne suffit plus mais deux si. Le niveau de démon
    /// utilisé est celui que produirait réellement AutoExtendController pour ce tier et ce
    /// niveau de corruption (voir RollBorderMonster/RollAbyssDemon), via
    /// MonsterLeveling.UndergroundLevel(tier: 4, corruptionLevel: 4).
    /// </summary>
    public class AdventurerVsDemonCombatTests
    {
        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord NW => new(-1, 1, IslandMap.SurfaceLayer);

        /// <summary>Niveau de démon (mineur ou majeur) au tier 4, niveau de corruption 4 — voir MonsterLeveling.UndergroundLevel.</summary>
        private static readonly int DemonLevelAtCorruption4 = MonsterLeveling.UndergroundLevel(tier: 4, corruptionLevel: 4);

        private const long CombatChunkTicks = 1_000L;

        /// <summary>
        /// Petite carte de 3 hexes mutuellement adjacents (un seul sommet de ville), pour que
        /// tout ce qui s'y trouve reste visible du joueur (nécessaire à l'Aventurier pour
        /// engager le combat) sans jamais sortir de portée d'attaque, quel que soit le
        /// déplacement erratique du démon sur ces 3 cases. La ville n'a pas d'Hôtel de Ville :
        /// une éventuelle attaque du démon contre elle se résout donc instantanément (ville déjà
        /// "détruite" au sens du code, sans contrôleur pour l'acter) sans consommer le PRNG
        /// partagé ni fausser les tirages du combat Aventurier/Démon qu'on observe ici.
        /// </summary>
        private static (WorldState state, GameClock clock) CreateArena()
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE, TerrainType.Plain),
                new(NW, TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var city = new City(Vertex.Create(Center, NE, NW)) { CivilizationIndex = 0 };
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            return (state, clock);
        }

        /// <summary>Fait avancer le combat jusqu'à ce qu'un des deux camps soit anéanti, ou que maxTicks soit atteint.</summary>
        private static void RunUntilOneSideDies(WorldState state, GameClock clock, long maxTicks)
        {
            for (long elapsed = 0; elapsed < maxTicks; elapsed += CombatChunkTicks)
            {
                clock.SimulateAdvance(CombatChunkTicks);

                bool anyDemonAlive = state.Features.OfType<MinorDemon>().Any() || state.Features.OfType<MajorDemon>().Any();
                bool anyHeroAlive = state.Features.OfType<Adventurer>().Any();
                if (!anyDemonAlive || !anyHeroAlive) return;
            }
        }

        [Fact]
        public void Level1Hero_LosesToMinorDemon()
        {
            var (state, clock) = CreateArena();
            var hero = new Adventurer(Center, level: 1) { Found = true };
            var demon = new MinorDemon(Center, DemonLevelAtCorruption4) { Found = true };
            state.AddFeature(hero);
            state.AddFeature(demon);

            RunUntilOneSideDies(state, clock, maxTicks: 15_000L);

            Assert.Empty(state.Features.OfType<Adventurer>());
            Assert.Single(state.Features.OfType<MinorDemon>());
        }

        [Fact]
        public void Level4Hero_EventuallyKillsMinorDemon()
        {
            var (state, clock) = CreateArena();
            var hero = new Adventurer(Center, level: 4) { Found = true };
            var demon = new MinorDemon(Center, DemonLevelAtCorruption4) { Found = true };
            state.AddFeature(hero);
            state.AddFeature(demon);

            RunUntilOneSideDies(state, clock, maxTicks: 10_000L);

            Assert.Empty(state.Features.OfType<MinorDemon>());
            Assert.Single(state.Features.OfType<Adventurer>());
        }

        [Fact]
        public void Level4Hero_LosesAloneToMajorDemon()
        {
            var (state, clock) = CreateArena();
            var hero = new Adventurer(Center, level: 4) { Found = true };
            var demon = new MajorDemon(Center, DemonLevelAtCorruption4) { Found = true };
            state.AddFeature(hero);
            state.AddFeature(demon);

            RunUntilOneSideDies(state, clock, maxTicks: 15_000L);

            Assert.Empty(state.Features.OfType<Adventurer>());
            Assert.Single(state.Features.OfType<MajorDemon>());
        }

        [Fact]
        public void TwoLevel4Heroes_KillMajorDemonTogether()
        {
            var (state, clock) = CreateArena();
            var heroA = new Adventurer(Center, level: 4) { Found = true };
            var heroB = new Adventurer(Center, level: 4) { Found = true };
            var demon = new MajorDemon(Center, DemonLevelAtCorruption4) { Found = true };
            state.AddFeature(heroA);
            state.AddFeature(heroB);
            state.AddFeature(demon);

            RunUntilOneSideDies(state, clock, maxTicks: 10_000L);

            Assert.Empty(state.Features.OfType<MajorDemon>());
        }
    }
}
