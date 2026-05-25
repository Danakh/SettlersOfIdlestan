using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using Xunit;

namespace SOITests.ControllerTests
{
    public class MilitaryControllerTests
    {
        // Shared hex coords
        private static HexCoord Center => new(0, 0);
        private static HexCoord NE     => new(0, 1);
        private static HexCoord East   => new(1, 0);
        private static HexCoord NE11   => new(1, 1);

        /// <summary>
        /// Creates a minimal setup: city at Vertex(NE, E, (1,1)) with a barracks.
        /// Center(0,0) is adjacent to two of those city hexes.
        /// Only MilitaryController is registered; BanditController is not, so
        /// bandit.LastMovedTick stays at 0 and the combat interval fires on every
        /// SimulateAdvance(CombatIntervalTicks) call.
        /// </summary>
        private static (IslandState state, GameClock clock, MilitaryController controller, Barracks barracks)
            CreateAdjacentSetup(int initialSoldiers, int barracksLevel = 2)
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(NE,     TerrainType.Plain),
                new(East,   TerrainType.Plain),
                new(NE11,   TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0 };
            var barracks = new Barracks { Level = barracksLevel, Soldiers = initialSoldiers };
            city.Buildings.Add(barracks);
            civ.Cities.Add(city);

            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var clock = new GameClock();
            clock.Start();
            var controller = new MilitaryController();
            controller.Initialize(state, clock);

            return (state, clock, controller, barracks);
        }

        // ── Soldier production ────────────────────────────────────────────────

        [Fact]
        public void Barracks_Level2_ProducesSoldiers()
        {
            var (_, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 0);

            Assert.Equal(0, barracks.Soldiers);
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);
            Assert.Equal(1, barracks.Soldiers);
            clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);
            Assert.Equal(2, barracks.Soldiers);
        }

        [Fact]
        public void Barracks_Level1_DoesNotProduceSoldiers()
        {
            var (_, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 0, barracksLevel: 1);

            for (int i = 0; i < 5; i++)
                clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

            Assert.Equal(0, barracks.Soldiers);
        }

        [Fact]
        public void Barracks_SoldierCapIsTen()
        {
            var (_, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 0);

            for (int i = 0; i < MilitaryController.MaxSoldiers + 5; i++)
                clock.SimulateAdvance(MilitaryController.SoldierProductionIntervalTicks);

            Assert.Equal(MilitaryController.MaxSoldiers, barracks.Soldiers);
        }

        // ── Bandit combat ─────────────────────────────────────────────────────

        [Fact]
        public void Bandit_AdjacentToBarracksWithSoldiers_TakesDamage()
        {
            var (state, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 3);
            state.Bandits.Add(new Bandit(Center, 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            // Production runs before combat: 3→4 soldiers, then attack: 4→3, bandit HP MaxHp→MaxHp-1.
            Assert.Equal(Bandit.MaxHp - 1, bandit.Hp);
            Assert.Equal(3, barracks.Soldiers);
        }

        [Fact]
        public void Bandit_KilledByBarracksSoldiers_IsRemovedFromState()
        {
            // Bandit has 5 HP; barracks starts with 5 soldiers.
            // Each cycle: production +1, attack -1 → net soldiers unchanged, bandit takes 1 damage.
            var (state, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 5);
            state.Bandits.Add(new Bandit(Center, 0) { Hp = 5 });

            Assert.Single(state.Bandits);

            for (int i = 0; i < 5; i++)
                clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Empty(state.Bandits);
            Assert.Equal(5, barracks.Soldiers); // production compensated each consumed soldier
        }

        [Fact]
        public void Bandit_NotAdjacentToBarracks_IsNotAttacked()
        {
            // Bandit placed far away, not adjacent to the city.
            var farHex = new HexCoord(-3, 0);
            var (state, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 5);

            var tiles = state.Map.Tiles;
            // Just add the bandit at a far position (not on map tiles, but the controller
            // only checks adjacency to city hexes, not whether the bandit is on the map).
            state.Bandits.Add(new Bandit(farHex, 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Equal(Bandit.MaxHp, bandit.Hp);
        }

        [Fact]
        public void Barracks_WithNoSoldiers_DoesNotAttackBandit()
        {
            var (state, clock, _, barracks) = CreateAdjacentSetup(initialSoldiers: 0, barracksLevel: 1);
            state.Bandits.Add(new Bandit(Center, 0));
            var bandit = state.Bandits[0];

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Equal(Bandit.MaxHp, bandit.Hp);
        }
    }
}
