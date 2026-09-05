using System;
using System.Collections.Generic;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Controller.Island.Production;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Buildings;
using SOITests.TestUtilities;
using SettlersOfIdlestan.Model;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    public class TradeControllerTests
    {
        private sealed class FlatModifierProvider : IModifierProvider
        {
            private readonly List<Modifier> _mods;
            public FlatModifierProvider(params Modifier[] mods) => _mods = new(mods);
            public IEnumerable<Modifier> GetModifiers() => _mods;
#pragma warning disable CS0067
            public event Action? OnModifiersChanged;
#pragma warning restore CS0067
        }

        [Fact]
        public void Trade_NotAvailableWithoutMarket()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new TradeController(state);

            Assert.False(controller.IsTradeAvailable(0));

            civ.AddResource(Resource.Wood, 5);
            Assert.False(controller.SellResource(0, Resource.Wood));
        }

        [Fact]
        public void SellResource_WithMarket_ConvertsToGold()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.AddResource(Resource.Wood, 5);

            var controller = new TradeController(state);

            Assert.True(controller.IsTradeAvailable(0));

            bool result = controller.SellResource(0, Resource.Wood);

            Assert.True(result);
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void BuyResource_BasicResource_CostsOneGold()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.AddResource(Resource.Gold, 3);

            var controller = new TradeController(state);

            Assert.True(controller.CanBuyResource(0, Resource.Brick, 3));
            controller.BuyResource(0, Resource.Brick, 3);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Brick));
        }

        [Fact]
        public void TryAutoTradeForPurchase_SellsSurplusAndBuysInOneStep()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());

            civ.AddResource(Resource.Wood, 10);

            var controller = new TradeController(state);

            var required = new ResourceSet {
                { Resource.Brick, 2 },
                { Resource.Food, 1 }
            };

            // One call: sells 5 Wood → 1 gold, then immediately buys 1 Brick (1 gold → 1 Brick)
            var result = controller.TryAutoTradeForPurchase(0, required);
            Assert.True(result);
            Assert.Equal(5, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Brick));
        }

        [Fact]
        public void TryAutoTradeForPurchase_DoesNotTradeIfNoSuitableSource()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());

            // Not enough wood to sell (need 5, have 3)
            civ.AddResource(Resource.Wood, 3);

            var controller = new TradeController(state);

            var required = new ResourceSet {
                { Resource.Brick, 1 }
            };

            var result = controller.TryAutoTradeForPurchase(0, required);
            Assert.False(result);
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
        }

        [Fact]
        public void CanTradeResource_ReturnsFalseWhenCapacityIsZero()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            var controller = new TradeController(state);

            Assert.True(controller.CanTradeResource(civ, Resource.Wood));
            Assert.False(controller.CanTradeResource(civ, Resource.Crystal));
        }

        [Fact]
        public void CanRecieveTrade_ReturnsFalseWhenStorageWouldOverflow()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            var controller = new TradeController(state);

            var maxWood = civ.GetResourceMaxQuantity(Resource.Wood);
            civ.AddResource(Resource.Wood, maxWood);

            Assert.False(controller.CanRecieveTrade(civ, Resource.Wood));
            Assert.True(controller.CanRecieveTrade(civ, Resource.Brick));
        }

        [Fact]
        public void SellResource_BulkGoldBonus_AddsOncePerTenPacks()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.Cities[0].AddBuilding(new TownHall { Level = 8 }); // capacity = 5*(2+8)=50
            civ.RecalculateStorageCapacity();

            civ.TechnologyTree.CompleteResearch(TechnologyId.EfficientTrading); // TRADE_BULK_GOLD_BONUS +1

            civ.AddResource(Resource.Wood, 50); // 10 packs at sell-rate 5

            var controller = new TradeController(state);
            bool result = controller.SellResource(0, Resource.Wood, 10);

            Assert.True(result);
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(11, civ.GetResourceQuantity(Resource.Gold)); // 10 + floor(10/10)*1
        }

        [Fact]
        public void SellResource_BulkGoldBonus_ScalesWithBonusValue()
        {
            // Verify floor(quantity/10)*bonus: 10 packs with bonus=3 → 10 + 3 = 13
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.Cities[0].AddBuilding(new TownHall { Level = 8 }); // capacity=50
            civ.RecalculateStorageCapacity();

            civ.AddCustomAggregator(new FlatModifierProvider(
                new Modifier(ECategory.TRADE_BULK_GOLD_BONUS, EType.ADDITIVE, 3)));

            civ.AddResource(Resource.Wood, 50);

            var controller = new TradeController(state);
            bool result = controller.SellResource(0, Resource.Wood, 10);

            Assert.True(result);
            Assert.Equal(13, civ.GetResourceQuantity(Resource.Gold)); // 10 + floor(10/10)*3
        }

        [Fact]
        public void SellResource_BulkGoldBonus_NoBonusWithoutModifier()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.Cities[0].AddBuilding(new TownHall { Level = 8 }); // capacity=50
            civ.RecalculateStorageCapacity();
            civ.AddResource(Resource.Wood, 50);

            var controller = new TradeController(state);
            controller.SellResource(0, Resource.Wood, 10);

            Assert.Equal(10, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void TradeRatioBonus_IncreasesSellYieldAndReducesBuyCost()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.Cities[0].AddBuilding(new TownHall { Level = 8 }); // capacity=50
            civ.RecalculateStorageCapacity();

            civ.AddCustomAggregator(new FlatModifierProvider(
                new Modifier(ECategory.TRADE_RATIO_BONUS, EType.ADDITIVE, 0.2)));

            var controller = new TradeController(state);

            Assert.Equal(24, controller.GetSellGoldYield(0, Resource.Wood, 20)); // 20 * 1.2

            civ.AddResource(Resource.Gold, 100);
            Assert.Equal(4, controller.GetBuyCost(0, Resource.Ore)); // 5 * (1 - 0.2)
        }

        [Fact]
        public void BuyResource_Ore_CostsFiveGold()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.Cities[0].AddBuilding(new TownHall { Level = 3 }); // city.Level=3 → Ore has capacity
            civ.RecalculateStorageCapacity();
            civ.AddResource(Resource.Gold, 15);

            var controller = new TradeController(state);

            Assert.True(controller.CanBuyResource(0, Resource.Ore, 3));
            controller.BuyResource(0, Resource.Ore, 3);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Ore));
        }

        // ── Market specialization (SpecializedMarket research) ──────────────────

        [Fact]
        public void GetSellRate_ReturnsDefault_WithoutSpecializedMarketResearch()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());

            var controller = new TradeController(state);

            Assert.Equal(5, controller.GetSellRate(0, Resource.Wood));
        }

        [Fact]
        public void GetSellRate_ReturnsFourToOne_ForAllBasicResources_OnceResearchCompleted()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.TechnologyTree.CompleteResearch(TechnologyId.StorageOptimization);
            civ.TechnologyTree.CompleteResearch(TechnologyId.SpecializedMarket);

            var controller = new TradeController(state);

            foreach (var resource in ResourceUtils.BasicResources)
                Assert.Equal(4, controller.GetSellRate(0, resource));
        }

        // ── Achat Automatique (auto-buy on gold overflow) ───────────────────────

        [Fact]
        public void IsAutoBuyUnlocked_RequiresModifierAndLevel4Market()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market { Level = 4 });

            var controller = new TradeController(state);
            Assert.False(controller.IsAutoBuyUnlocked(0));

            civ.AddCustomAggregator(new FlatModifierProvider(
                new Modifier(ECategory.UNLOCK_AUTO_BUY_TRADE, EType.ADDITIVE, 1)));

            Assert.True(controller.IsAutoBuyUnlocked(0));
        }

        [Fact]
        public void IsAutoBuyUnlocked_FalseWithoutLevel4Market()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market { Level = 2 });
            civ.AddCustomAggregator(new FlatModifierProvider(
                new Modifier(ECategory.UNLOCK_AUTO_BUY_TRADE, EType.ADDITIVE, 1)));

            var controller = new TradeController(state);
            Assert.False(controller.IsAutoBuyUnlocked(0));
        }

        [Fact]
        public void TryAutoBuyOnGoldOverflow_BuysScarcestResource_DownToKeepThreshold()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());

            int maxGold = civ.GetResourceMaxQuantity(Resource.Gold);
            civ.AddResource(Resource.Gold, maxGold);
            civ.AddResource(Resource.Wood, 5);
            civ.AddResource(Resource.Brick, 2);
            // Food and Stone are left at 0 — Food is scarcest (first in BasicResources order).

            int keepThreshold = maxGold * state.AutomationSettings.AutoBuyGoldKeepPercent / 100;
            int expected = maxGold + 1 - keepThreshold;

            var controller = new TradeController(state);
            bool bought = controller.TryAutoBuyOnGoldOverflow(0);

            Assert.True(bought);
            // Tout l'excédent est dépensé en un seul achat, pas une unité par appel.
            Assert.Equal(maxGold - expected, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(expected, civ.GetResourceQuantity(Resource.Food));
        }

        [Fact]
        public void TryAutoBuyOnGoldOverflow_BuysNonBasicResource_WhenScarcest()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.SetStorageCapacityCache(1000, 1000);

            // Toutes les ressources de base au plafond : seul le Minerai reste rare, et il n'était
            // jamais acheté tant que l'Achat Automatique se limitait à ResourceUtils.BasicResources.
            foreach (var basic in ResourceUtils.BasicResources)
                civ.AddResource(basic, civ.GetResourceMaxQuantity(basic));

            int maxGold = civ.GetResourceMaxQuantity(Resource.Gold);
            civ.AddResource(Resource.Gold, maxGold);

            var controller = new TradeController(state);
            int keepThreshold = maxGold * state.AutomationSettings.AutoBuyGoldKeepPercent / 100;
            int expected = (maxGold + 1 - keepThreshold) / controller.GetBuyCost(0, Resource.Ore);

            bool bought = controller.TryAutoBuyOnGoldOverflow(0);

            Assert.True(bought);
            Assert.True(expected > 0);
            Assert.Equal(expected, civ.GetResourceQuantity(Resource.Ore));
        }

        [Fact]
        public void TryAutoTradeOnOverflow_SellsEnoughToOffsetIncomingProduction()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            var city = civ.Cities[0];
            city.AddBuilding(new Market { Level = 4 });
            civ.AddCustomAggregator(new FlatModifierProvider(
                new Modifier(ECategory.UNLOCK_AUTO_MARKET_TRADE, EType.ADDITIVE, 1),
                new Modifier(ECategory.UNLOCK_INTERMEDIATE_TRADE, EType.ADDITIVE, 1)));
            // Après AddCustomAggregator : tout changement de modificateurs relance
            // RecalculateStorageCapacity, qui écraserait ces valeurs injectées.
            civ.SetStorageCapacityCache(1000, 1000);

            civ.AddResource(Resource.Ore, 1000); // au plafond, donc au-dessus du seuil de vente

            var trader = new ProductionOverflowTrader();
            trader.Initialize(state, new TradeController(state));

            trader.TryAutoTradeOnOverflow(civ, city, Resource.Ore, 25);

            // Une passe de Minerai vaut une seule unité : vendre une passe fixe (l'ancien
            // comportement) ne pouvait pas compenser les 25 unités sur le point d'être ajoutées,
            // et le stock restait collé au plafond.
            Assert.Equal(975, civ.GetResourceQuantity(Resource.Ore));
        }

        [Fact]
        public void TryAutoBuyOnGoldOverflow_DoesNothingWhenGoldNotFull()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());
            civ.AddResource(Resource.Gold, 1);

            var controller = new TradeController(state);
            bool bought = controller.TryAutoBuyOnGoldOverflow(0);

            Assert.False(bought);
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void SellResource_AutoBuysOnGoldOverflow_WhenUnlocked()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market { Level = 4 });
            civ.AddCustomAggregator(new FlatModifierProvider(
                new Modifier(ECategory.UNLOCK_AUTO_BUY_TRADE, EType.ADDITIVE, 1)));

            int maxGold = civ.GetResourceMaxQuantity(Resource.Gold);
            civ.AddResource(Resource.Gold, maxGold);
            civ.AddResource(Resource.Wood, 5);

            int keepThreshold = maxGold * state.AutomationSettings.AutoBuyGoldKeepPercent / 100;
            int freed = maxGold + 1 - keepThreshold;

            var controller = new TradeController(state);
            bool result = controller.SellResource(0, Resource.Wood);

            Assert.True(result);
            // Achat Automatique a libéré tout l'excédent au-dessus de la part conservée (achat de
            // Food) juste avant que la vente ne rapporte 1 or.
            Assert.Equal(maxGold - freed + 1, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(freed, civ.GetResourceQuantity(Resource.Food));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
        }

        [Fact]
        public void SellResource_FailsOnGoldOverflow_WithoutAutoBuyUnlock()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].AddBuilding(new Market());

            int maxGold = civ.GetResourceMaxQuantity(Resource.Gold);
            civ.AddResource(Resource.Gold, maxGold);
            civ.AddResource(Resource.Wood, 5);

            var controller = new TradeController(state);
            bool result = controller.SellResource(0, Resource.Wood);

            Assert.False(result);
            Assert.Equal(5, civ.GetResourceQuantity(Resource.Wood));
        }
    }

}
