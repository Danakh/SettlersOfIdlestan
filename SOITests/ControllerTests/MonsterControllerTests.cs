using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;
using Xunit;
using SettlersOfIdlestan.Model.Monsters;

namespace SOITests.ControllerTests
{
    public class MonsterControllerTests
    {
        // Map layout for trapped scenario (axial coordinates):
        //
        //   NW(-1,1)  NE(0,1)
        // W(-1,0)  [0,0]  E(1,0)
        //   SW(0,-1)  SE(1,-1)
        //
        // Water: W and E
        // Plain: NW, NE, SW, SE
        // City A at Vertex(center, NE, NW) → barracks cover NE and NW
        // City B at Vertex(center, SW, SE) → barracks cover SW and SE
        //
        // With active barracks: all non-water neighbors are protected → bandit stays.
        // With inactive barracks: bandit can move to any plain neighbor.

        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);
        private static HexCoord East   => new(1, 0, IslandMap.SurfaceLayer);
        private static HexCoord West   => new(-1, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE     => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord NW     => new(-1, 1, IslandMap.SurfaceLayer);
        private static HexCoord SE     => new(1, -1, IslandMap.SurfaceLayer);
        private static HexCoord SW     => new(0, -1, IslandMap.SurfaceLayer);

        private static (WorldState state, GameClock clock, MonsterFeatureController controller) CreateTrappedSetup(bool activeBarracks)
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(East,   TerrainType.Water),
                new(West,   TerrainType.Water),
                new(NE,     TerrainType.Plain),
                new(NW,     TerrainType.Plain),
                new(SE,     TerrainType.Plain),
                new(SW,     TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };

            // Vertex A: center + NE + NW  →  protects NE and NW (and center itself)
            var vertexA = Vertex.Create(Center, NE, NW);
            var cityA = new City(vertexA) { CivilizationIndex = 0 };
            cityA.Buildings.Add(new Barracks { Level = activeBarracks ? 1 : 0 });

            // Vertex B: center + SW + SE  →  protects SW and SE (and center itself)
            var vertexB = Vertex.Create(Center, SW, SE);
            var cityB = new City(vertexB) { CivilizationIndex = 0 };
            cityB.Buildings.Add(new Barracks { Level = activeBarracks ? 1 : 0 });

            civ.AddCity(cityA);
            civ.AddCity(cityB);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();

            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            return (state, clock, controller);
        }

        // ── Water exclusion ──────────────────────────────────────────────────

        [Fact]
        public void Bandit_NeverMovesToWaterTile()
        {
            // Small map: center (desert) with one plain neighbor and one water neighbor.
            // The bandit should always end up on the plain tile, never on water.
            var plain = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var water = new HexCoord(-1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
                new(water,  TerrainType.Water),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            for (int i = 0; i < 10; i++)
                clock.SimulateAdvance(MonsterFeatureController.MovementIntervalTicks);

            Assert.NotEqual(water, state.Features.OfType<Bandit>().First().Position);
        }

        // ── Harvest blocking and cooldown ────────────────────────────────────

        [Fact]
        public void IsHarvestBlocked_ReturnsTrueWhileBanditPresent()
        {
            var state = new WorldState(
                new IslandMap(new List<HexTile> { new(Center, TerrainType.Desert) }),
                new List<Civilization> { new() { Index = 0 } },
                AtlasController.InvalidIslandId);

            state.AddFeature(new Bandit(Center, 0));

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            Assert.True(controller.IsHarvestBlocked(Center, clock.CurrentTick));
        }

        [Fact]
        public void IsHarvestBlocked_ReturnsTrueImmediatelyAfterBanditLeaves()
        {
            // Only two tiles: bandit starts at center and can only go to plain.
            var plain = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            // Trigger one move: bandit must go to plain (only valid destination).
            clock.SimulateAdvance(MonsterFeatureController.MovementIntervalTicks);

            Assert.Equal(plain, state.Features.OfType<Bandit>().First().Position);
            Assert.True(controller.IsHarvestBlocked(Center, clock.CurrentTick),
                "Cooldown should be active on the tile the bandit just left");
        }

        [Fact]
        public void IsHarvestBlocked_ReturnsFalseAfterCooldownExpires()
        {
            var plain = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            clock.SimulateAdvance(MonsterFeatureController.MovementIntervalTicks);
            Assert.Equal(plain, state.Features.OfType<Bandit>().First().Position);

            // Advance past the departure cooldown (bandit won't move again because its
            // LastMovedTick was just updated — next move requires another MovementIntervalTicks).
            var bandit = state.Features.OfType<Bandit>().First();
            clock.SimulateAdvance(bandit.DepartureCooldownTicks + 1);

            Assert.False(controller.IsHarvestBlocked(Center, clock.CurrentTick),
                "Cooldown should have expired");
        }

        // ── Raid mechanic ────────────────────────────────────────────────────

        private static (WorldState state, GameClock clock, MonsterFeatureController controller, Civilization civ)
            CreateRaidSetup()
        {
            // City at Vertex(NE, East, NE11) — bandit at Center is adjacent to NE and East.
            var ne   = new HexCoord(0, 1, IslandMap.SurfaceLayer);
            var east = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var ne11 = new HexCoord(1, 1, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(ne,     TerrainType.Plain),
                new(east,   TerrainType.Plain),
                new(ne11,   TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var city = new City(Vertex.Create(ne, east, Center)) { CivilizationIndex = 0 };
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.AddFeature(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            return (state, clock, controller, civ);
        }

        [Fact]
        public void Bandit_AdjacentToCity_RaidsAndStealsResource()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            civ.AddResource(Resource.Wood, 5);

            clock.SimulateAdvance(Bandit.RaidIntervalTicks);

            Assert.Equal(4, civ.GetResourceQuantity(Resource.Wood));
            var bandit = state.Features.OfType<Bandit>().First();
            Assert.NotNull(bandit.LastAttackTargetVertex);
            Assert.NotNull(bandit.LastAttackResourcesString);
            Assert.Contains(nameof(Resource.Wood), bandit.LastAttackResourcesString);
        }

        [Fact]
        public void Bandit_AdjacentToCity_NoResources_DoesNotCrash()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            // No resources given to civ

            clock.SimulateAdvance(Bandit.RaidIntervalTicks);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
            var bandit = state.Features.OfType<Bandit>().First();
            Assert.Null(bandit.LastAttackTargetVertex);
            Assert.Null(bandit.LastAttackResourcesString);
        }

        [Fact]
        public void Bandit_RaidsOncePerInterval()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            civ.AddResource(Resource.Wood, 10);

            // First raid at tick 100
            clock.SimulateAdvance(Bandit.RaidIntervalTicks);
            Assert.Equal(9, civ.GetResourceQuantity(Resource.Wood));

            // No second raid until another interval passes (bandit also hasn't moved)
            clock.SimulateAdvance(Bandit.RaidIntervalTicks - 1);
            Assert.Equal(9, civ.GetResourceQuantity(Resource.Wood));

            // Second raid fires at tick 200
            clock.SimulateAdvance(1);
            Assert.Equal(8, civ.GetResourceQuantity(Resource.Wood));
        }

        // ── Dragon target consistency after a city is destroyed ────────────────

        [Fact]
        public void Dragon_AfterDestroyingCity_StopsTargetingItAndKeepsCountsConsistent()
        {
            // City A sits on the dragon's own hex (always in range). City B is a
            // separate cluster far away, never adjacent to the dragon, and must
            // never be attacked nor destroyed.
            var ne   = new HexCoord(0, 1, IslandMap.SurfaceLayer);
            var east = new HexCoord(1, 0, IslandMap.SurfaceLayer);

            var farCenter = new HexCoord(20, 0, IslandMap.SurfaceLayer);
            var farNE     = new HexCoord(20, 1, IslandMap.SurfaceLayer);
            var farEast   = new HexCoord(21, 0, IslandMap.SurfaceLayer);

            var tiles = new List<HexTile>
            {
                new(Center,    TerrainType.Desert),
                new(ne,        TerrainType.Plain),
                new(east,      TerrainType.Plain),
                new(farCenter, TerrainType.Plain),
                new(farNE,     TerrainType.Plain),
                new(farEast,   TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };

            // City A: 5 soldiers absorb the first hit, a level-1 TownHall falls on the second.
            var cityA = new City(Vertex.Create(Center, ne, east)) { CivilizationIndex = 0, Soldiers = 5 };
            cityA.Buildings.Add(new TownHall { Level = 1 });

            // City B: out of the dragon's attack range (own hex + neighbors) for its entire life.
            var cityB = new City(Vertex.Create(farCenter, farNE, farEast)) { CivilizationIndex = 0 };
            cityB.Buildings.Add(new TownHall { Level = 5 });

            civ.AddCity(cityA);
            civ.AddCity(cityB);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var dragon = new Dragon(Center) { Found = true };
            state.AddFeature(dragon);

            var clock = new GameClock();
            clock.Start();
            var controller = new MonsterFeatureController();
            controller.Initialize(state, clock, new GamePRNG());

            void AssertCityCountsConsistent()
            {
                Assert.Equal(civ.Cities.Count, state.GetAllCities().Count());
                Assert.Contains(cityB, civ.Cities);
                Assert.NotEqual(cityB.Position, dragon.LastAttackTargetVertex);
            }

            // Attack 1: city A's garrison absorbs the hit, city A survives.
            clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);
            Assert.Equal(2, civ.Cities.Count);
            Assert.Equal(cityA.Position, dragon.LastAttackTargetVertex);
            AssertCityCountsConsistent();

            // Attack 2: garrison is gone, the TownHall falls → city A is destroyed.
            clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);
            Assert.Equal(1, civ.Cities.Count);
            Assert.DoesNotContain(cityA, civ.Cities);
            AssertCityCountsConsistent();

            // From now on the dragon has no reachable target (its former target is gone).
            // At every following attack interval, the dragon must NOT keep reporting a
            // target at the destroyed city's vertex — that was the reported bug (the
            // dragon visually keeps "attacking" a location after its city was destroyed).
            for (int i = 0; i < 5; i++)
            {
                clock.SimulateAdvance(Dragon.DragonAttackIntervalTicks);

                Assert.Null(dragon.LastAttackTargetVertex);
                Assert.Null(state.FindCityAt(cityA.Position));
                Assert.Equal(1, civ.Cities.Count);
                AssertCityCountsConsistent();
            }
        }
    }
}
