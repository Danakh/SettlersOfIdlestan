using Xunit;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Buildings;
using SOITests.TestUtilities;
using SettlersOfIdlestan.Model;

namespace SOITests.ControllerTests
{
    public class PrestigeControllerTests
    {
        [Fact]
        public void Prestige_PrestigePointCount()
        {
            IslandState state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.Civilizations[0];

            var controller = new PrestigeController();
            controller.Initialize(civ);

            Assert.Equal(0, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings.Add(new TownHall());
            Assert.Equal(1, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings.Add(new Library());
            Assert.Equal(2, controller.CalculatePrestigePoints());

            civ.Cities[0].Buildings[0].Level = 2; // raise townhall to level 2 (no change)
            Assert.Equal(2, controller.CalculatePrestigePoints());
            civ.Cities[0].Buildings[0].Level = 3; // raise townhall to level 3 (+1 point)
            Assert.Equal(3, controller.CalculatePrestigePoints());
        }
    }
}
