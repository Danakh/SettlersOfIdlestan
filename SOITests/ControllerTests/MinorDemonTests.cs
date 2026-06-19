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
    public class MinorDemonTests
    {
        [Fact]
        public void MinorDemon_HasDragonPowerButFasterMovementAndLongerPillageCooldown()
        {
            var demon = new MinorDemon(new HexCoord(0, 0, IslandMap.SurfaceLayer));

            // Aussi puissant qu'un dragon.
            Assert.Equal(Dragon.DragonMaxHp, demon.MaxHp);
            Assert.Equal(2, demon.AttackRangeInHexes);
            Assert.True(demon.IgnoresPalisade);
            Assert.Equal(2, demon.AttackDamage);
            Assert.Equal(5, demon.AttackResources);

            // Mais beaucoup plus mobile, avec un cooldown de pillage allongé en conséquence.
            Assert.True(demon.CanMove);
            Assert.Equal(1_000L, demon.MovementIntervalTicks);
            Assert.Equal(2, demon.MovementRangeInHexes);
            Assert.Equal(2_000L, demon.AttackIntervalTicks);
        }

        /// <summary>
        /// Couloir de 3 hexes : Center - Mid - Far, où Center et Far n'ont qu'un seul voisin sur
        /// la carte (Mid). Le premier des deux pas de déplacement est donc forcé (Center → Mid),
        /// et le second ne peut que revenir à Center ou continuer vers Far : quel que soit le
        /// tirage aléatoire, le démon ne peut jamais s'arrêter sur Mid après un déplacement complet.
        /// Ceci prouve que les deux pas ont bien eu lieu (MovementRangeInHexes = 2).
        /// </summary>
        [Fact]
        public void MinorDemon_MovesTwoHexesPerInterval_NeverEndsOnMidpoint()
        {
            var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var mid    = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var far    = new HexCoord(2, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(center, TerrainType.Plain),
                new(mid,    TerrainType.Plain),
                new(far,    TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var demon = new MinorDemon(center) { Found = true };
            state.AddFeature(demon);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            clock.SimulateAdvance(MinorDemon.MinorDemonMovementIntervalTicks);

            Assert.NotEqual(mid, demon.Position);
            Assert.True(demon.Position.Equals(center) || demon.Position.Equals(far));
        }
    }
}
