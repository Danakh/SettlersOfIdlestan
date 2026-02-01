using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.Buildings;
using System.Collections.Generic;
using SOITests.TestUtilities;

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
            Assert.Throws<System.InvalidOperationException>(() => controller.Trade(0, Resource.Wood, Resource.Brick));
        }

        [Fact]
        public void Trade_WithMarket_PerformsTrade()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];
            civ.Cities[0].Buildings.Add(new Market());
            civ.AddResource(Resource.Wood, 4);

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
            civ.AddResource(Resource.Wood, 8);
            civ.AddResource(Resource.Sheep, 1);

            var controller = new TradeController(state);

            var required = new Dictionary<Resource, int> {
                { Resource.Brick, 2 },
                { Resource.Sheep, 1 }
            };

            var result = controller.TryAutoTradeForPurchase(0, required);
            Assert.True(result);

            // After trade, one brick should be present and wood decreased by 4
            Assert.Equal(4, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(1, civ.GetResourceQuantity(Resource.Brick));
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

            var required = new Dictionary<Resource, int> {
                { Resource.Brick, 1 }
            };

            var result = controller.TryAutoTradeForPurchase(0, required);
            Assert.False(result);
            Assert.Equal(3, civ.GetResourceQuantity(Resource.Wood));
            Assert.Equal(0, civ.GetResourceQuantity(Resource.Brick));
        }
    }
}
