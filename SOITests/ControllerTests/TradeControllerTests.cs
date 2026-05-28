using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Buildings;
using SOITests.TestUtilities;
using SettlersOfIdlestan.Model;

namespace SOITests.ControllerTests
{
    public class TradeControllerTests
    {
        [Fact]
        public void Trade_NotAvailableWithoutMarketOrSeaport()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new TradeController(state);

            Assert.False(controller.IsTradeAvailable(0));

            // can't trade when not available
            civ.AddResource(Resource.Wood, 4);
            Assert.False(controller.Trade(0, Resource.Wood, Resource.Brick));
        }

        [Fact]
        public void Trade_WithMarket_PerformsTrade()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());
            civ.AddResource(Resource.Wood, 5);

            var controller = new TradeController(state);

            Assert.True(controller.IsTradeAvailable(0));

            controller.Trade(0, Resource.Wood, Resource.Brick);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Brick));
        }

        [Fact]
        public void TryAutoTradeForPurchase_PerformsTradeWhenPossible()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());

            // Owned: wood 8, brick 0, sheep 1
            civ.AddResource(Resource.Wood, 10);
            civ.AddResource(Resource.Food, 1);

            var controller = new TradeController(state);

            var required = new ResourceSet {
                { Resource.Brick, 2 },
                { Resource.Food, 1 }
            };

            var result = controller.TryAutoTradeForPurchase(0, required);
            Assert.True(result);

            // After trade, one brick should be present and wood decreased by 4
            Assert.Equal(5, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Brick));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Food));
        }

        [Fact]
        public void TryAutoTradeForPurchase_DoesNotTradeIfNoSuitableSource()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());

            // Owned: wood 3 only (not enough to trade)
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
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            var controller = new TradeController(state);

            Assert.True(controller.CanTradeResource(civ, Resource.Wood));
            Assert.False(controller.CanTradeResource(civ, Resource.Crystal));
        }

        [Fact]
        public void CanRecieveTrade_ReturnsFalseWhenStorageWouldOverflow()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            var controller = new TradeController(state);

            var maxWood = civ.GetResourceMaxQuantity(Resource.Wood);
            civ.AddResource(Resource.Wood, maxWood);

            Assert.False(controller.CanRecieveTrade(civ, Resource.Wood));
            Assert.True(controller.CanRecieveTrade(civ, Resource.Brick));
        }

        [Fact]
        public void BuyRate_OreIsOne_OthersAreDefault()
        {
            var controller = new TradeController();

            Assert.Equal(1, controller.BuyRate(Resource.Ore));
            Assert.Equal(5, controller.BuyRate(Resource.Glass));
            Assert.Equal(5, controller.BuyRate(Resource.Crystal));
        }

        [Fact]
        public void BuyAdvancedResource_Ore_CostsOneGold()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());
            civ.Cities[0].Buildings.Add(new TownHall { Level = 3 }); // city.Level=3 → advancedCityResourceMax=1 → Ore has capacity
            civ.AddResource(Resource.Gold, 3);

            var controller = new TradeController(state);

            Assert.True(controller.CanBuyAdvancedResource(0, Resource.Ore, 3));
            controller.BuyAdvancedResource(0, Resource.Ore, 3);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Gold));
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Ore));
        }
    }
}
