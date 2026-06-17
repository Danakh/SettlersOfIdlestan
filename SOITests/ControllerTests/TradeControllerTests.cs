using System;
using System.Collections.Generic;
using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Island;
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
            civ.Cities[0].Buildings.Add(new Market());
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
            civ.Cities[0].Buildings.Add(new Market());
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
            civ.Cities[0].Buildings.Add(new Market());

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
            civ.Cities[0].Buildings.Add(new Market());

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
            civ.Cities[0].Buildings.Add(new Market());
            civ.Cities[0].Buildings.Add(new TownHall { Level = 8 }); // capacity = 5*(2+8)=50
            BuildingController.RecalculateStorageCapacity(civ);

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
            civ.Cities[0].Buildings.Add(new Market());
            civ.Cities[0].Buildings.Add(new TownHall { Level = 8 }); // capacity=50
            BuildingController.RecalculateStorageCapacity(civ);

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
            civ.Cities[0].Buildings.Add(new Market());
            civ.Cities[0].Buildings.Add(new TownHall { Level = 8 }); // capacity=50
            BuildingController.RecalculateStorageCapacity(civ);
            civ.AddResource(Resource.Wood, 50);

            var controller = new TradeController(state);
            controller.SellResource(0, Resource.Wood, 10);

            Assert.Equal(10, civ.GetResourceQuantity(Resource.Gold));
        }

        [Fact]
        public void BuyRate_BasicIsOne_OreIsFive_AdvancedIsTwenty()
        {
            var controller = new TradeController();

            Assert.Equal(1, controller.BuyRate(Resource.Wood));
            Assert.Equal(1, controller.BuyRate(Resource.Brick));
            Assert.Equal(5, controller.BuyRate(Resource.Ore));
            Assert.Equal(20, controller.BuyRate(Resource.Glass));
            Assert.Equal(20, controller.BuyRate(Resource.Crystal));
        }

        [Fact]
        public void BuyResource_Ore_CostsFiveGold()
        {
            WorldState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());
            civ.Cities[0].Buildings.Add(new TownHall { Level = 3 }); // city.Level=3 → Ore has capacity
            BuildingController.RecalculateStorageCapacity(civ);
            civ.AddResource(Resource.Gold, 15);

            var controller = new TradeController(state);

            Assert.True(controller.CanBuyResource(0, Resource.Ore, 3));
            controller.BuyResource(0, Resource.Ore, 3);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Ore));
        }
    }

}
