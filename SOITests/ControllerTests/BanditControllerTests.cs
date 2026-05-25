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
    public class BanditControllerTests
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

        private static HexCoord Center => new(0, 0);
        private static HexCoord East   => new(1, 0);
        private static HexCoord West   => new(-1, 0);
        private static HexCoord NE     => new(0, 1);
        private static HexCoord NW     => new(-1, 1);
        private static HexCoord SE     => new(1, -1);
        private static HexCoord SW     => new(0, -1);

        private static (IslandState state, GameClock clock, BanditController controller) CreateTrappedSetup(bool activeBarracks)
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

            civ.Cities.Add(cityA);
            civ.Cities.Add(cityB);

            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.Bandits.Add(new Bandit(Center, 0));

            var clock = new GameClock();
            clock.Start();

            var controller = new BanditController();
            controller.Initialize(state, clock);

            return (state, clock, controller);
        }

        // ── Trapped scenario ─────────────────────────────────────────────────

        [Fact]
        public void Bandit_TrappedBetweenWaterAndActiveBarracks_DoesNotMove()
        {
            var (state, clock, _) = CreateTrappedSetup(activeBarracks: true);
            var bandit = state.Bandits[0];

            for (int i = 0; i < 5; i++)
                clock.SimulateAdvance(BanditController.MovementIntervalTicks);

            Assert.Equal(Center, bandit.Position);
        }

        [Fact]
        public void Bandit_TrappedWithInactiveBarracks_CanMoveToPLainTile()
        {
            var (state, clock, _) = CreateTrappedSetup(activeBarracks: false);
            var bandit = state.Bandits[0];

            // With barracks at level 0 the plain tiles are not protected.
            clock.SimulateAdvance(BanditController.MovementIntervalTicks);

            var reachable = new HashSet<HexCoord> { NE, NW, SE, SW };
            Assert.Contains(bandit.Position, reachable);
        }

        [Fact]
        public void Bandit_TrappedWithActiveBarracks_HarvestNotBlockedOnProtectedTiles()
        {
            // The bandit is stuck at center; the plain tiles are protected but the bandit
            // never visits them, so no cooldown should be set on those tiles.
            var (state, clock, controller) = CreateTrappedSetup(activeBarracks: true);

            for (int i = 0; i < 3; i++)
                clock.SimulateAdvance(BanditController.MovementIntervalTicks);

            // Bandit is still at center; plain tiles should not have a departure cooldown.
            Assert.False(controller.IsHarvestBlocked(NE, clock.CurrentTick));
            Assert.False(controller.IsHarvestBlocked(NW, clock.CurrentTick));
            Assert.False(controller.IsHarvestBlocked(SE, clock.CurrentTick));
            Assert.False(controller.IsHarvestBlocked(SW, clock.CurrentTick));
        }

        // ── Water exclusion ──────────────────────────────────────────────────

        [Fact]
        public void Bandit_NeverMovesToWaterTile()
        {
            // Small map: center (desert) with one plain neighbor and one water neighbor.
            // The bandit should always end up on the plain tile, never on water.
            var plain = new HexCoord(1, 0);
            var water = new HexCoord(-1, 0);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
                new(water,  TerrainType.Water),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.Bandits.Add(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new BanditController();
            controller.Initialize(state, clock);

            for (int i = 0; i < 10; i++)
                clock.SimulateAdvance(BanditController.MovementIntervalTicks);

            Assert.NotEqual(water, state.Bandits[0].Position);
        }

        // ── Harvest blocking and cooldown ────────────────────────────────────

        [Fact]
        public void IsHarvestBlocked_ReturnsTrueWhileBanditPresent()
        {
            var state = new IslandState(
                new IslandMap(new List<HexTile> { new(Center, TerrainType.Desert) }),
                new List<Civilization> { new() { Index = 0 } },
                AtlasController.InvalidIslandId);

            state.Bandits.Add(new Bandit(Center, 0));

            var clock = new GameClock();
            clock.Start();
            var controller = new BanditController();
            controller.Initialize(state, clock);

            Assert.True(controller.IsHarvestBlocked(Center, clock.CurrentTick));
        }

        [Fact]
        public void IsHarvestBlocked_ReturnsTrueImmediatelyAfterBanditLeaves()
        {
            // Only two tiles: bandit starts at center and can only go to plain.
            var plain = new HexCoord(1, 0);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.Bandits.Add(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new BanditController();
            controller.Initialize(state, clock);

            // Trigger one move: bandit must go to plain (only valid destination).
            clock.SimulateAdvance(BanditController.MovementIntervalTicks);

            Assert.Equal(plain, state.Bandits[0].Position);
            Assert.True(controller.IsHarvestBlocked(Center, clock.CurrentTick),
                "Cooldown should be active on the tile the bandit just left");
        }

        [Fact]
        public void IsHarvestBlocked_ReturnsFalseAfterCooldownExpires()
        {
            var plain = new HexCoord(1, 0);

            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Desert),
                new(plain,  TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.Bandits.Add(new Bandit(Center, 0) { Found = true });

            var clock = new GameClock();
            clock.Start();
            var controller = new BanditController();
            controller.Initialize(state, clock);

            clock.SimulateAdvance(BanditController.MovementIntervalTicks);
            Assert.Equal(plain, state.Bandits[0].Position);

            // Advance past the departure cooldown (bandit won't move again because its
            // LastMovedTick was just updated — next move requires another MovementIntervalTicks).
            clock.SimulateAdvance(BanditController.DepartureCooldownTicks + 1);

            Assert.False(controller.IsHarvestBlocked(Center, clock.CurrentTick),
                "Cooldown should have expired");
        }

        // ── Raid mechanic ────────────────────────────────────────────────────

        private static (IslandState state, GameClock clock, BanditController controller, Civilization civ)
            CreateRaidSetup()
        {
            // City at Vertex(NE, East, NE11) — bandit at Center is adjacent to NE and East.
            var ne   = new HexCoord(0, 1);
            var east = new HexCoord(1, 0);
            var ne11 = new HexCoord(1, 1);

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
            civ.Cities.Add(city);

            var state = new IslandState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            state.Bandits.Add(new Bandit(Center, 0));

            var clock = new GameClock();
            clock.Start();
            var controller = new BanditController();
            controller.Initialize(state, clock);

            return (state, clock, controller, civ);
        }

        [Fact]
        public void Bandit_AdjacentToCity_RaidsAndStealsResource()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            civ.AddResource(Resource.Wood, 5);

            clock.SimulateAdvance(Bandit.RaidIntervalTicks);

            Assert.Equal(4, civ.GetResourceQuantity(Resource.Wood));
            Assert.NotNull(state.Bandits[0].LastRaidTargetVertex);
            Assert.Equal(nameof(Resource.Wood), state.Bandits[0].LastStolenResource);
        }

        [Fact]
        public void Bandit_AdjacentToCity_NoResources_DoesNotCrash()
        {
            var (state, clock, _, civ) = CreateRaidSetup();
            // No resources given to civ

            clock.SimulateAdvance(Bandit.RaidIntervalTicks);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
            Assert.Null(state.Bandits[0].LastRaidTargetVertex);
            Assert.Null(state.Bandits[0].LastStolenResource);
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
    }
}
