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
            city.AddBuilding(new Sawmill());

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

        /// <summary>
        /// HARVEST_PRODUCTION_BONUS n'est pas plafonné à un doublement : comme pour la Forge, la
        /// partie entière du bonus est acquise et seul le reste est tiré au sort. À 200% (ex. Corne
        /// d'Abondance cumulée à un autre bonus de rendement), chaque récolte rapporte donc 3 unités
        /// sans aucun aléa.
        /// </summary>
        [Fact]
        public void AutomaticHarvest_WithProductionBonusAbove100Percent_YieldsMoreThanDouble()
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
            civ.Cities[0].AddBuilding(new Sawmill());

            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.HARVEST_PRODUCTION_BONUS, EType.ADDITIVE, 200),
            }));

            var clock = new GameClock();
            clock.Start();
            var harvestController = new HarvestController(state, clock);
            harvestController.Initialize(state, clock, prng: new GamePRNG(1));

            clock.SimulateAdvance(10);
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Wood));

            clock.SimulateAdvance(500);
            Assert.Equal(6, civ.GetResourceQuantity(Resource.Wood));
        }

        /// <summary>
        /// Régression : sur un hexagone jamais récolté manuellement, le tracker de cooldown partagé
        /// vaut 0 par défaut. Sans <c>coldStartOnZero</c>, le premier appel après un déblocage tardif
        /// de "Main de Dieu" (tick courant très avancé) calculait <c>cycles = now / cooldownTicks</c> et
        /// rejouait cette récolte des centaines de milliers de fois de façon synchrone (freeze à l'achat).
        /// </summary>
        [Fact]
        public void PeriodicHandOfGodHarvest_FirstCallOnLateGameTick_DoesNotCatchUpFromTickZero()
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

            var clock = new GameClock();
            clock.Start();
            var harvestController = new HarvestController(state, clock);

            // Simule une partie déjà très avancée (bien au-delà du cooldown de récolte) avant que le
            // joueur n'obtienne "Main de Dieu" : l'hexagone "a" n'a jamais été récolté manuellement.
            clock.SimulateAdvance(50_000_000);

            harvestController.PerformPeriodicHandOfGodHarvest(civ.Index, a);
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));

            // Le cooldown reprend normalement à partir de maintenant : une seule récolte après un cycle complet.
            clock.SimulateAdvance(HarvestController.HarvestCooldownTicks);
            harvestController.PerformPeriodicHandOfGodHarvest(civ.Index, a);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));
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
            city.AddBuilding(new Sawmill());

            // Dominion niveau 5 (+10%/niveau = +50%) amplifié par 2 vertex de prestige (×1.2)
            // ⇒ +60% de vitesse, cooldown effectif = 500 / 1.6 = 312 ticks.
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

            // Récolte au tick 10. Cooldown effectif = 312 ticks : toujours rien à 300 ticks écoulés depuis (tick 310).
            clock.SimulateAdvance(300);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // 320 ticks écoulés depuis la première récolte (tick 330) ⩾ 312 ⇒ nouvelle récolte.
            clock.SimulateAdvance(20);
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
            city.AddBuilding(new Sawmill());

            // Bonus intrinsèque seul : Dominion niveau 5 ⇒ +50% de vitesse, cooldown = 500 / 1.5 = 333 ticks.
            state.AddFeature(new Dominion(a, level: 5));

            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(10);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Récolte au tick 10. Cooldown effectif = 333 ticks : toujours rien à 320 ticks écoulés depuis (tick 330).
            clock.SimulateAdvance(320);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // 335 ticks écoulés depuis la première récolte (tick 345) ⩾ 333 ⇒ nouvelle récolte.
            clock.SimulateAdvance(15);
            Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void AutomaticHarvest_Sawmill_DoesNotHarvestWoodOnMushroomCave_WithoutResearch()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.MushroomCave),
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
            city.AddBuilding(new Sawmill());

            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(500);
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void AutomaticHarvest_Sawmill_HarvestsWoodOnMushroomCaveAtHalfSpeed_WithResearch()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.MushroomCave),
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
            city.AddBuilding(new Sawmill());

            civ.TechnologyTree.CompleteResearch(TechnologyId.BoisDeChampignon);

            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            // First harvest is immediate.
            clock.SimulateAdvance(10);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // Cooldown is doubled on Mushroom Cave (1000 ticks instead of 500) : still nothing at 910 ticks elapsed.
            clock.SimulateAdvance(900);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Wood));

            // 1010 ticks elapsed since the first harvest >= 1000 -> new harvest.
            clock.SimulateAdvance(100);
            Assert.Equal(2, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void Sawmill_IsBuildingAvailableForCity_NextToMushroomCave_RequiresBoisDeChampignonResearch()
        {
            var a = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var b = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var c = new HexCoord(0, 1, IslandMap.SurfaceLayer);

            var tiles = new[]
            {
                new HexTile(a, TerrainType.MushroomCave),
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

            var sawmill = new Sawmill();

            // Pas de Forêt adjacente et recherche non complétée : non constructible.
            Assert.False(sawmill.IsBuildingAvailableForCity(map, city, civ));

            // La recherche Bois de Champignon la rend constructible à côté d'une Caverne aux Champignons.
            civ.TechnologyTree.CompleteResearch(TechnologyId.BoisDeChampignon);
            Assert.True(sawmill.IsBuildingAvailableForCity(map, city, civ));
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
            city.AddBuilding(new Sawmill());

            return (state, civ, city);
        }

        [Fact]
        public void AutomaticHarvest_SellsOverflow_WhenAutomaticMarketUnlockedAndCityHasMarketLevel4()
        {
            var (state, civ, city) = CreateOverflowSetup();
            city.AddBuilding(new Market { Level = 4 });
            civ.RecalculateStorageCapacity();
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
            city.AddBuilding(new Market { Level = 4 });
            civ.RecalculateStorageCapacity();

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
            city.AddBuilding(new Market { Level = 3 });
            civ.RecalculateStorageCapacity();
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
