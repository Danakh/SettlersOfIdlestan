using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    public class HarvestControllerTests
    {
        [Fact]
        public void AutomaticHarvest_ByProductionBuilding_AddsResourceWithCooldown()
        {
            // Create a small map where one of the hexes produces wood
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest),
                new HexTile(b, TerrainType.Plain),
                new HexTile(c, TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new List<Civilization> { civ };
            var state = new WorldState(map, civs, AtlasController.InvalidIslandId);

            // Place a city adjacent to the wood tile and add a Sawmill (produces wood)
            var vertex = Vertex.Create(a, b, c);
            IslandMapGenerator generator = new IslandMapGenerator(new GamePRNG(42));
            generator.PopulatePlayerCivilization(map, civ, vertex);
            var city = civ.Cities[0];
            city.Buildings.Add(new Sawmill());

            var clock = new GameClock();
            clock.Start();

            // Create a harvest controller that listens to the clock
            var harvestController = new HarvestController(state, clock);

            // Initially no wood
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));

            // First small advance should trigger an automatic harvest (first time)
            clock.SimulateAdvance(10); // 0.1 s
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Advance a bit but stay within automatic cooldown -> no new harvest
            clock.SimulateAdvance(100); // 1 s
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Advance beyond the automatic cooldown (5s = 500 ticks) -> another harvest should occur
            clock.SimulateAdvance(500); // 5 s
            Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void AutomaticHarvest_WithDominionOnHex_HarvestsFaster()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest),
                new HexTile(b, TerrainType.Plain),
                new HexTile(c, TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new List<Civilization> { civ };
            var state = new WorldState(map, civs, AtlasController.InvalidIslandId);

            var vertex = Vertex.Create(a, b, c);
            IslandMapGenerator generator = new IslandMapGenerator(new GamePRNG(42));
            generator.PopulatePlayerCivilization(map, civ, vertex);
            var city = civ.Cities[0];
            city.Buildings.Add(new Sawmill());

            // Dominion niveau 5 (+20%/niveau = +100%) amplifié par 2 vertex de prestige (×1.2)
            // ⇒ +120% de vitesse, cooldown effectif = 500 / 2.2 = 227 ticks.
            state.AddFeature(new Dominion(a, level: 5));
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.DOMINION_HARVEST_SPEED_PER_LEVEL, EType.ADDITIVE, 0.2),
            }));

            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(10);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Cooldown effectif = 227 ticks : toujours rien à +190 ticks (200 écoulés).
            clock.SimulateAdvance(190);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // 250 ticks écoulés depuis la première récolte ⩾ 227 ⇒ nouvelle récolte.
            clock.SimulateAdvance(50);
            Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void AutomaticHarvest_WithDominionOnHex_IntrinsicBonusWithoutPrestigeModifier()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest),
                new HexTile(b, TerrainType.Plain),
                new HexTile(c, TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new List<Civilization> { civ };
            var state = new WorldState(map, civs, AtlasController.InvalidIslandId);

            var vertex = Vertex.Create(a, b, c);
            IslandMapGenerator generator = new IslandMapGenerator(new GamePRNG(42));
            generator.PopulatePlayerCivilization(map, civ, vertex);
            var city = civ.Cities[0];
            city.Buildings.Add(new Sawmill());

            // Bonus intrinsèque seul : Dominion niveau 5 ⇒ +100% de vitesse, cooldown = 500 / 2 = 250 ticks.
            state.AddFeature(new Dominion(a, level: 5));

            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(10);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Toujours rien à +100 ticks (110 écoulés < 250).
            clock.SimulateAdvance(100);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // 310 ticks écoulés depuis la première récolte ⩾ 250 ⇒ nouvelle récolte.
            clock.SimulateAdvance(200);
            Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void CorruptionHarvestTimeMultiplier_ReducedByCorruptionLevelReduction_WithFloorAtLevel1()
        {
            var civ = new Civilization { Index = 0 };

            // Sans modificateur : niv. 3 ⇒ ×8.
            var corruption = new Corruption(new HexCoord(0, 0, IslandMap.SurfaceLayer), level: 3);
            Assert.Equal(8.0, corruption.GetHarvestTimeMultiplier(civ), 5);

            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.CORRUPTION_LEVEL_REDUCTION, EType.ADDITIVE, 2),
            }));

            // Niveau effectif 3 - 2 = 1 ⇒ ×2.
            Assert.Equal(2.0, corruption.GetHarvestTimeMultiplier(civ), 5);

            // Plancher au niveau 1 : la corruption n'est jamais annulée par la recherche.
            var lowCorruption = new Corruption(new HexCoord(1, 0, IslandMap.SurfaceLayer), level: 1);
            Assert.Equal(2.0, lowCorruption.GetHarvestTimeMultiplier(civ), 5);
        }

        [Fact]
        public void MarketGoldGenerationCooldown_ReducesBy10PercentPerLevel()
        {
            var civ = new Civilization { Index = 0 };

            long level1 = HarvestController.GetEffectiveMarketGoldGenerationCooldown(civ, 1);
            long level2 = HarvestController.GetEffectiveMarketGoldGenerationCooldown(civ, 2);

            Assert.Equal(HarvestController.MarketGoldGenerationCooldownTicks, level1);
            Assert.Equal((long)(HarvestController.MarketGoldGenerationCooldownTicks * 0.9), level2);
        }

        private static (WorldState state, Civilization civ, City city) CreateOverflowSetup()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.Forest),
                new HexTile(b, TerrainType.Plain),
                new HexTile(c, TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            var civs = new List<Civilization> { civ };
            var state = new WorldState(map, civs, AtlasController.InvalidIslandId);

            var vertex = Vertex.Create(a, b, c);
            IslandMapGenerator generator = new IslandMapGenerator(new GamePRNG(42));
            generator.PopulatePlayerCivilization(map, civ, vertex);
            var city = civ.Cities[0];
            city.Buildings.Add(new Sawmill());

            return (state, civ, city);
        }

        [Fact]
        public void AutomaticHarvest_SellsOverflow_WhenAutomaticMarketUnlockedAndCityHasMarketLevel4()
        {
            var (state, civ, city) = CreateOverflowSetup();
            city.Buildings.Add(new Market { Level = 4 });
            BuildingController.RecalculateStorageCapacity(civ);
            civ.TechnologyTree.CompleteResearch(TechnologyId.AutomaticMarket);

            int maxWood = civ.GetResourceMaxQuantity(Resource.Wood);
            civ.AddResource(Resource.Wood, maxWood);

            var clock = new GameClock();
            clock.Start();
            var tradeController = new TradeController(state);
            var harvestController = new HarvestController();
            harvestController.Initialize(state, clock, tradeController);

            clock.SimulateAdvance(10); // first automatic harvest

            int sellRate = tradeController.GetSellRate(civ.Index, Resource.Wood);
            Assert.Equal(maxWood - sellRate + 1, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void AutomaticHarvest_DoesNotSellOverflow_WithoutAutomaticMarketResearch()
        {
            var (state, civ, city) = CreateOverflowSetup();
            city.Buildings.Add(new Market { Level = 4 });
            BuildingController.RecalculateStorageCapacity(civ);

            int maxWood = civ.GetResourceMaxQuantity(Resource.Wood);
            civ.AddResource(Resource.Wood, maxWood);

            var clock = new GameClock();
            clock.Start();
            var tradeController = new TradeController(state);
            var harvestController = new HarvestController();
            harvestController.Initialize(state, clock, tradeController);

            clock.SimulateAdvance(10);

            Assert.Equal(maxWood, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void AutomaticHarvest_DoesNotSellOverflow_WhenCityMarketBelowLevel4()
        {
            var (state, civ, city) = CreateOverflowSetup();
            city.Buildings.Add(new Market { Level = 3 });
            BuildingController.RecalculateStorageCapacity(civ);
            civ.TechnologyTree.CompleteResearch(TechnologyId.AutomaticMarket);

            int maxWood = civ.GetResourceMaxQuantity(Resource.Wood);
            civ.AddResource(Resource.Wood, maxWood);

            var clock = new GameClock();
            clock.Start();
            var tradeController = new TradeController(state);
            var harvestController = new HarvestController();
            harvestController.Initialize(state, clock, tradeController);

            clock.SimulateAdvance(10);

            Assert.Equal(maxWood, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
        }
    }
}
