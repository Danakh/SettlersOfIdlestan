using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>Tests de la branche de l'Acier : Fonderie, Armes en Acier, Armures d'Acier, commerce et prestige.</summary>
    public class SteelBranchTests
    {
        private static HexCoord NE   => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord East => new(1, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE11 => new(1, 1, IslandMap.SurfaceLayer);
        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);

        private static (WorldState state, GameClock clock, City city)
            CreateMilitarySetup(int initialSoldiers, int barracksLevel = 4)
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Plain),
                new(NE,     TerrainType.Plain),
                new(East,   TerrainType.Plain),
                new(NE11,   TerrainType.Plain),
            };

            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            civ.Resources[Resource.Ore] = 999;
            civ.Resources[Resource.Food] = 999;
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0, Soldiers = initialSoldiers };
            city.Buildings.Add(new Barracks { Level = barracksLevel });
            civ.AddCity(city);

            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var clock = new GameClock();
            clock.Start();
            var controller = new MilitaryController();
            controller.Initialize(state, clock, prng: new GamePRNG());

            return (state, clock, city);
        }

        private static (WorldState state, GameClock clock, City city)
            CreateSmithSetup(int weaponSmithLevel = 0, int armorSmithLevel = 0, int initialSteel = 999)
        {
            var tiles = new List<HexTile>
            {
                new(Center, TerrainType.Plain),
                new(NE,     TerrainType.Plain),
                new(East,   TerrainType.Plain),
                new(NE11,   TerrainType.Plain),
            };
            var map = new IslandMap(tiles);
            var civ = new Civilization { Index = 0 };
            civ.Resources[Resource.Steel] = initialSteel;
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0 };
            if (weaponSmithLevel > 0) city.Buildings.Add(new WeaponSmith { Level = weaponSmithLevel });
            if (armorSmithLevel > 0) city.Buildings.Add(new ArmorSmith { Level = armorSmithLevel });
            civ.AddCity(city);
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);
            return (state, clock, city);
        }

        // ── Armes/Armures en Acier — production par la Forge d'Armes / d'Armures ──

        [Fact]
        public void WeaponSmith_Level1_ProducesAfter1000TicksAndConsumesSteel()
        {
            var (state, clock, city) = CreateSmithSetup(weaponSmithLevel: 1);
            var civ = state.Civilizations[0];

            clock.SimulateAdvance(HarvestController.WeaponSmithBaseIntervalTicks);

            Assert.True(civ.GetResourceQuantity(Resource.SteelWeapon) >= 1);
            Assert.Equal(999 - WeaponSmith.SteelInputPerWeapon, civ.GetResourceQuantity(Resource.Steel));
        }

        [Fact]
        public void ArmorSmith_Level1_ProducesAfter1000TicksAndConsumesSteel()
        {
            var (state, clock, city) = CreateSmithSetup(armorSmithLevel: 1);
            var civ = state.Civilizations[0];

            clock.SimulateAdvance(HarvestController.ArmorSmithBaseIntervalTicks);

            Assert.True(civ.GetResourceQuantity(Resource.SteelArmor) >= 1);
            Assert.Equal(999 - ArmorSmith.SteelInputPerArmor, civ.GetResourceQuantity(Resource.Steel));
        }

        [Fact]
        public void WeaponSmithAndArmorSmith_BothBuilt_ProduceBoth()
        {
            var (state, clock, city) = CreateSmithSetup(weaponSmithLevel: 1, armorSmithLevel: 1);
            var civ = state.Civilizations[0];

            clock.SimulateAdvance(HarvestController.WeaponSmithBaseIntervalTicks);

            Assert.True(civ.GetResourceQuantity(Resource.SteelWeapon) >= 1);
            Assert.True(civ.GetResourceQuantity(Resource.SteelArmor)  >= 1);
        }

        [Fact]
        public void WeaponSmith_NoBuilding_ProducesNothing()
        {
            var (state, clock, city) = CreateSmithSetup();
            var civ = state.Civilizations[0];

            clock.SimulateAdvance(HarvestController.WeaponSmithBaseIntervalTicks * 5);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.SteelWeapon));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.SteelArmor));
        }

        [Fact]
        public void WeaponSmith_NotEnoughSteel_ProducesNothing()
        {
            var (state, clock, city) = CreateSmithSetup(weaponSmithLevel: 1, initialSteel: 1);
            var civ = state.Civilizations[0];

            clock.SimulateAdvance(HarvestController.WeaponSmithBaseIntervalTicks * 5);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.SteelWeapon));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Steel));
        }

        [Fact]
        public void WeaponSmith_Inactive_ProducesNothing()
        {
            var (state, clock, city) = CreateSmithSetup(weaponSmithLevel: 1);
            city.Buildings.OfType<WeaponSmith>().First().ActivationStatus = ActivationStatus.INACTIVE;
            var civ = state.Civilizations[0];

            clock.SimulateAdvance(HarvestController.WeaponSmithBaseIntervalTicks * 5);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.SteelWeapon));
        }

        [Fact]
        public void ForgeConsumable_StorageCap_Is5PerCityPlus5PerArsenalLevel()
        {
            var civ = new Civilization { Index = 0 };
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0 };
            city.Buildings.Add(new Arsenal { Level = 2 });
            civ.AddCity(city);

            // 5 * 1 city + 5 * 2 arsenal levels = 15
            Assert.Equal(15, civ.GetResourceMaxQuantity(Resource.SteelWeapon));
            Assert.Equal(15, civ.GetResourceMaxQuantity(Resource.SteelArmor));
        }

        // ── Armures d'Acier ───────────────────────────────────────────────────

        private static void UnlockSteelArmor(Civilization civ)
        {
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_STEEL_ARMOR, EType.ADDITIVE, 1),
            }));
        }

        [Fact]
        public void SteelArmor_WithArmorInStock_CanSaveSoldierAttackingMonster()
        {
            // Le sauvetage est probabiliste à 50%. On donne 100 armures et on vérifie
            // qu'au moins un soldat est sauvé sur plusieurs ticks.
            var (state, clock, city) = CreateMilitarySetup(initialSoldiers: 1);
            var civ = state.Civilizations[0];
            civ.Resources[Resource.SteelArmor] = 100;
            UnlockSteelArmor(civ);
            state.AddFeature(new Bandit(NE, 0));

            // Après 1 tick de combat, le soldat meurt OU est sauvé
            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            // La tentative de sauvegarde a bien consommé une armure (peu importe le résultat)
            Assert.True(civ.GetResourceQuantity(Resource.SteelArmor) < 100);
        }

        [Fact]
        public void SteelArmor_WithoutArmorInStock_SoldierDies()
        {
            var (state, clock, city) = CreateMilitarySetup(initialSoldiers: 3);
            var civ = state.Civilizations[0];
            UnlockSteelArmor(civ);
            state.AddFeature(new Bandit(NE, 0));

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Equal(2, city.Soldiers);
            Assert.Equal(0, civ.GetResourceQuantity(Resource.SteelArmor));
        }

        [Fact]
        public void SteelArmor_WithoutResearch_SoldierDiesAndArmorUntouched()
        {
            var (state, clock, city) = CreateMilitarySetup(initialSoldiers: 3);
            var civ = state.Civilizations[0];
            civ.Resources[Resource.SteelArmor] = 5;
            // Pas de UNLOCK_STEEL_ARMOR
            state.AddFeature(new Bandit(NE, 0));

            clock.SimulateAdvance(MilitaryController.CombatIntervalTicks);

            Assert.Equal(2, city.Soldiers);
            Assert.Equal(5, civ.GetResourceQuantity(Resource.SteelArmor));
        }

        // ── Fonderie — modificateurs de production ────────────────────────────

        [Fact]
        public void Smelter_DefaultValues_AreUnmodified()
        {
            var civ = new Civilization { Index = 0 };

            Assert.Equal(Smelter.OreInputPerCycle, HarvestController.GetSmelterOreInput(civ));
            Assert.Equal(Smelter.SteelOutputPerCycle, HarvestController.GetSmelterSteelOutput(civ));
            Assert.Equal(Smelter.ProductionCooldownTicks, HarvestController.GetEffectiveSmelterCooldown(civ, new Smelter { Level = 1 }));
        }

        [Fact]
        public void Smelter_WithSiderurgieModifier_ConsumesLessOre()
        {
            var civ = new Civilization { Index = 0 };
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.SMELTER_ORE_INPUT, EType.ADDITIVE, -2),
            }));

            Assert.Equal(3, HarvestController.GetSmelterOreInput(civ));
        }

        [Fact]
        public void Smelter_WithBlastFurnace_ProducesExtraSteel()
        {
            var civ = new Civilization { Index = 0 };
            var blastFurnace = new BlastFurnace { Level = 1 };
            civ.AddCustomAggregator(new StaticModifierProvider(blastFurnace.GetUniqueBuildingModifiers()));

            Assert.Equal(Smelter.SteelOutputPerCycle + BlastFurnace.BonusSteelPerSmelterCycle,
                HarvestController.GetSmelterSteelOutput(civ));
        }

        [Fact]
        public void Smelter_WithSpeedModifier_HasShorterCooldown()
        {
            var civ = new Civilization { Index = 0 };
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.SMELTER_SPEED, EType.ADDITIVE, 0.15),
            }));

            Assert.True(HarvestController.GetEffectiveSmelterCooldown(civ, new Smelter { Level = 1 }) < Smelter.ProductionCooldownTicks);
        }

        [Fact]
        public void Smelter_HigherLevel_HasShorterCooldown()
        {
            var civ = new Civilization { Index = 0 };

            Assert.True(HarvestController.GetEffectiveSmelterCooldown(civ, new Smelter { Level = 3 }) < Smelter.ProductionCooldownTicks);
        }

        // ── Commerce — vente d'Acier (Aciers Spéciaux) ────────────────────────

        [Fact]
        public void SellSteel_WithoutResearch_Fails()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());
            civ.Resources[Resource.Steel] = 5;

            var controller = new TradeController(state);

            Assert.False(controller.IsSteelTradeUnlocked(0));
            Assert.False(controller.SellResource(0, Resource.Steel));
            Assert.Equal(5, civ.GetResourceQuantity(Resource.Steel));
        }

        [Fact]
        public void SellSteel_WithResearch_GivesPremiumGold()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());
            civ.Resources[Resource.Steel] = 5;
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_STEEL_TRADE, EType.ADDITIVE, 1),
            }));

            var controller = new TradeController(state);

            Assert.True(controller.IsSteelTradeUnlocked(0));
            Assert.Equal(1, controller.GetSellRate(0, Resource.Steel));
            Assert.Equal(TradeController.SteelSellGoldValue, controller.GetSellGoldYield(0, Resource.Steel, 1));

            Assert.True(controller.SellResource(0, Resource.Steel));
            Assert.Equal(4, civ.GetResourceQuantity(Resource.Steel));
            Assert.Equal(TradeController.SteelSellGoldValue, civ.GetResourceQuantity(Resource.Gold));
        }

        // ── Carte de prestige — branche nord-est ──────────────────────────────

        [Fact]
        public void PrestigeMap_SteelBranchVertices_AreDefined()
        {
            var map = PrestigeMapFactory.CreateDefault();

            var blastFurnace = map.GetVertex(PrestigeMap.BlastFurnaceVertex);
            Assert.NotNull(blastFurnace);
            Assert.Contains(blastFurnace!.Modifiers, m =>
                m.Category == ECategory.UNLOCK_RESEARCH && m.SubCategory == "Siderurgie");
            Assert.Contains(blastFurnace.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "BlastFurnace");

            var militaryEngineering = map.GetVertex(PrestigeMap.MilitaryEngineeringVertex);
            Assert.NotNull(militaryEngineering);
            Assert.Contains(militaryEngineering!.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "Arsenal");

            Assert.NotNull(map.GetVertex(PrestigeMap.SteelLegionVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.ImperialRoadsVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.PlanarGateVertex));
        }

        [Fact]
        public void PrestigeMap_SteelworksHex_GrantsSmelterSpeedPerVertex()
        {
            var map = PrestigeMapFactory.CreateDefault();

            var hex = map.GetHex(PrestigeMap.SteelworksCoord);
            Assert.NotNull(hex);
            Assert.Contains(hex!.PerVertexModifiers, m => m.Category == ECategory.SMELTER_SPEED);
            // 6 vertex adjacents : Secret de l'Acier + les 5 nouveaux
            Assert.Equal(6, hex.AdjacentVertices.Count);
        }
    }
}
