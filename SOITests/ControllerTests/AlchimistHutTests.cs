using System.Collections.Generic;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>Tests de la Hutte d'Alchimie : prérequis de construction, récolte de cristaux et production de Potions de Soin.</summary>
    public class AlchimistHutTests
    {
        private static HexCoord NE     => new(0, 1, IslandMap.SurfaceLayer);
        private static HexCoord East   => new(1, 0, IslandMap.SurfaceLayer);
        private static HexCoord NE11   => new(1, 1, IslandMap.SurfaceLayer);
        private static HexCoord Center => new(0, 0, IslandMap.SurfaceLayer);

        private static (WorldState state, City city) CreateSetup()
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
            var vertex = Vertex.Create(NE, East, NE11);
            var city = new City(vertex) { CivilizationIndex = 0 };
            civ.AddCity(city);
            // Stockage avancé suffisant pour accumuler les cristaux récoltés dans les tests.
            city.Buildings.Add(new TownHall { Level = 20 });
            BuildingController.RecalculateStorageCapacity(civ);
            var state = new WorldState(map, new List<Civilization> { civ }, AtlasController.InvalidIslandId);
            return (state, city);
        }

        // ── Prérequis de construction ───────────────────────────────────────

        [Fact]
        public void HasBuildPrerequisites_AdjacentToFoundFairyCircle_ReturnsTrue()
        {
            var (state, city) = CreateSetup();
            state.AddFeature(new FairyCircle(NE) { Found = true });

            var hut = new AlchimistHut();

            Assert.True(hut.HasBuildPrerequisites(city, state));
            Assert.Null(hut.GetMissingPrerequisiteKey(city, state));
        }

        [Fact]
        public void HasBuildPrerequisites_FairyCircleNotYetFound_ReturnsFalse()
        {
            var (state, city) = CreateSetup();
            state.AddFeature(new FairyCircle(NE) { Found = false });

            var hut = new AlchimistHut();

            Assert.False(hut.HasBuildPrerequisites(city, state));
            Assert.Equal("tooltip_requires_fairy_circle", hut.GetMissingPrerequisiteKey(city, state));
        }

        [Fact]
        public void HasBuildPrerequisites_NoFairyCircleAdjacent_ReturnsFalse()
        {
            var (state, city) = CreateSetup();

            var hut = new AlchimistHut();

            Assert.False(hut.HasBuildPrerequisites(city, state));
        }

        // ── Récolte de cristaux (alignée sur les bâtiments de production) ───

        [Fact]
        public void CrystalHarvest_AdjacentFoundFairyCircle_HarvestsAfter60sBaseCooldown()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            city.Buildings.Add(new AlchimistHut { Level = 1 });
            state.AddFeature(new FairyCircle(NE) { Found = true });
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(AlchimistHut.CrystalHarvestBaseCooldownTicks - 1);
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Crystal));

            clock.SimulateAdvance(1);
            Assert.Equal(FairyCircle.CrystalsPerCycle, civ.GetResourceQuantity(Resource.Crystal));
        }

        [Fact]
        public void CrystalHarvest_NoAdjacentFairyCircle_HarvestsNothing()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            city.Buildings.Add(new AlchimistHut { Level = 1 });
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(AlchimistHut.CrystalHarvestBaseCooldownTicks * 2);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Crystal));
        }

        [Fact]
        public void CrystalHarvest_HigherLevel_HasShorterCooldown()
        {
            var hut = new AlchimistHut { Level = 5 };

            long cooldown = hut.GetAutomaticHarvestCooldown(HarvestController.AutomaticHarvestCooldownTicks);

            Assert.True(cooldown < AlchimistHut.CrystalHarvestBaseCooldownTicks);
        }

        [Fact]
        public void CrystalHarvest_WithHarvestSpeedModifier_HarvestsFaster()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            city.Buildings.Add(new AlchimistHut { Level = 1 });
            state.AddFeature(new FairyCircle(NE) { Found = true });
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.HARVEST_SPEED, nameof(BuildingType.AlchimistHut), EType.ADDITIVE, 1.0),
            }));
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            // Vitesse doublée (×2.0) : récolte dès la moitié du cooldown de base
            clock.SimulateAdvance(AlchimistHut.CrystalHarvestBaseCooldownTicks / 2);

            Assert.Equal(FairyCircle.CrystalsPerCycle, civ.GetResourceQuantity(Resource.Crystal));
        }

        // ── Production de Potions de Soin ───────────────────────────────────

        [Fact]
        public void HealingPotion_AlchimistHutLevel1_ProducesAfter1000TicksAndConsumesGlassAndCrystal()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.Resources[Resource.Glass] = 10;
            civ.Resources[Resource.Crystal] = 10;
            city.Buildings.Add(new AlchimistHut { Level = 1 });
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_HEALING_POTION, EType.ADDITIVE, 1),
            }));
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(HarvestController.AlchimistHutPotionBaseIntervalTicks);

            Assert.True(civ.GetResourceQuantity(Resource.HealingPotion) >= 1);
            Assert.Equal(10 - AlchimistHut.GlassInputPerPotion, civ.GetResourceQuantity(Resource.Glass));
            Assert.Equal(10 - AlchimistHut.CrystalInputPerPotion, civ.GetResourceQuantity(Resource.Crystal));
        }

        [Fact]
        public void HealingPotion_NoModifier_ProducesNothing()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.Resources[Resource.Glass] = 10;
            civ.Resources[Resource.Crystal] = 10;
            city.Buildings.Add(new AlchimistHut { Level = 1 });
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(HarvestController.AlchimistHutPotionBaseIntervalTicks * 5);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.HealingPotion));
        }

        [Fact]
        public void HealingPotion_NotEnoughGlassOrCrystal_ProducesNothing()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            city.Buildings.Add(new AlchimistHut { Level = 1 });
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_HEALING_POTION, EType.ADDITIVE, 1),
            }));
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(HarvestController.AlchimistHutPotionBaseIntervalTicks * 5);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.HealingPotion));
        }

        [Fact]
        public void HealingPotion_Inactive_ProducesNothing()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.Resources[Resource.Glass] = 10;
            civ.Resources[Resource.Crystal] = 10;
            var hut = new AlchimistHut { Level = 1, ActivationStatus = ActivationStatus.INACTIVE };
            city.Buildings.Add(hut);
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_HEALING_POTION, EType.ADDITIVE, 1),
            }));
            var clock = new GameClock();
            clock.Start();
            new HarvestController(state, clock);

            clock.SimulateAdvance(HarvestController.AlchimistHutPotionBaseIntervalTicks * 5);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.HealingPotion));
        }

        // ── Sauvetage de soldats (Armure d'Acier + Potion de Soin) ──────────

        [Fact]
        public void TrySaveSoldiers_BothConsumablesAvailable_ChancesSumTo100Percent()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.Resources[Resource.SteelArmor] = 1000;
            civ.Resources[Resource.HealingPotion] = 1000;
            // Arsenal niveau 3 : 35% (base armure) + 5%*3 (arsenal) + 50% (potion) = 100%.
            city.Buildings.Add(new Arsenal { Level = 3 });
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_STEEL_ARMOR, EType.ADDITIVE, 1),
                new Modifier(ECategory.UNLOCK_HEALING_POTION, EType.ADDITIVE, 1),
            }));

            int saved = SteelArmorEngine.TrySaveSoldiers(civ, city, 100, new GamePRNG(123));

            Assert.Equal(100, saved);
        }

        [Fact]
        public void TrySaveSoldiers_OnlyHealingPotionUnlocked_ConsumesPotionsNotArmor()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.Resources[Resource.HealingPotion] = 1000;
            civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(ECategory.UNLOCK_HEALING_POTION, EType.ADDITIVE, 1),
            }));

            int saved = SteelArmorEngine.TrySaveSoldiers(civ, city, 100, new GamePRNG(123));

            Assert.True(saved > 0);
            Assert.True(civ.GetResourceQuantity(Resource.HealingPotion) < 1000);
        }

        [Fact]
        public void TrySaveSoldiers_NeitherUnlocked_SavesNothing()
        {
            var (state, city) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.Resources[Resource.SteelArmor] = 100;
            civ.Resources[Resource.HealingPotion] = 100;

            int saved = SteelArmorEngine.TrySaveSoldiers(civ, city, 10, new GamePRNG(123));

            Assert.Equal(0, saved);
            Assert.Equal(100, civ.GetResourceQuantity(Resource.SteelArmor));
            Assert.Equal(100, civ.GetResourceQuantity(Resource.HealingPotion));
        }
    }
}
