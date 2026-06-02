using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    public class WonderControllerTests
    {
        private static HexCoord WonderHex => new(0, 0, IslandMap.SurfaceLayer);

        /// <summary>
        /// TownHall niveau 20 → basic max = 5*(2+20) = 110, advanced max = 5*(20-2) = 90.
        /// Suffisant pour que stock/100 = 1 avec AddResource(r, max).
        /// </summary>
        private const int TownHallLevel = 20;

        private static (IslandState state, Wonder wonder, GameClock clock, WonderController controller) CreateSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var city = state.PlayerCivilization.Cities[0];
            city.Buildings.Add(new TownHall { Level = TownHallLevel });

            var wonder = new Wonder(WonderHex) { Level = 0 };
            state.AddFeature(wonder);

            var clock = new GameClock();
            clock.Start();

            var controller = new WonderController();
            controller.Initialize(state, clock);

            return (state, wonder, clock, controller);
        }

        // ── Activation de l'investissement ───────────────────────────────────

        [Fact]
        public void Wonder_Investment_WhenDisabled_NoResourceConsumed()
        {
            var (state, _, clock, _) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.AddResource(Resource.Food, 110);
            // InvestmentEnabled est vide → aucun investissement

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            Assert.Equal(110, civ.GetResourceQuantity(Resource.Food));
        }

        [Fact]
        public void Wonder_Investment_WhenEnabled_ConsumesResourceAndInvests()
        {
            var (state, wonder, clock, _) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.AddResource(Resource.Food, 110); // max = 110, amount = 110/100 = 1
            wonder.InvestmentEnabled.Add(Resource.Food);

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            Assert.Equal(109, civ.GetResourceQuantity(Resource.Food));
            Assert.Equal(1L, wonder.InvestedResources[Resource.Food]);
        }

        [Fact]
        public void Wonder_Investment_RespectsIntervalCooldown()
        {
            var (state, wonder, clock, _) = CreateSetup();
            var civ = state.PlayerCivilization;
            civ.AddResource(Resource.Food, 110);
            wonder.InvestmentEnabled.Add(Resource.Food);

            // Avance juste en dessous de l'intervalle → pas d'investissement
            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks - 1);

            Assert.Equal(110, civ.GetResourceQuantity(Resource.Food));
            Assert.Empty(wonder.InvestedResources);
        }

        [Fact]
        public void Wonder_Investment_NoEffect_WhenStockTooLow()
        {
            var (state, wonder, clock, _) = CreateSetup();
            var civ = state.PlayerCivilization;
            wonder.InvestmentEnabled.Add(Resource.Food);

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            Assert.Equal(0, civ.GetResourceQuantity(Resource.Food));
            Assert.Empty(wonder.InvestedResources);
        }

        // ── Passage de niveau ────────────────────────────────────────────────

        [Fact]
        public void Wonder_Investment_LevelsUpWhenAllResourcesFullyInvested()
        {
            var (_, wonder, clock, _) = CreateSetup();

            // Pré-remplir InvestedResources exactement au seuil → la boucle détecte
            // invested >= required pour chaque ressource et déclenche le level-up
            // sans avoir besoin de stock joueur.
            var cost = WonderController.GetLevelCost(1);
            foreach (var kvp in cost)
            {
                wonder.InvestedResources[kvp.Key] = kvp.Value;
                wonder.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            Assert.Equal(1, wonder.Level);
        }

        [Fact]
        public void Wonder_LevelUp_ClearsInvestedResourcesAndEnabled()
        {
            var (_, wonder, clock, _) = CreateSetup();

            var cost = WonderController.GetLevelCost(1);
            foreach (var kvp in cost)
            {
                wonder.InvestedResources[kvp.Key] = kvp.Value;
                wonder.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            Assert.Empty(wonder.InvestedResources);
            Assert.Empty(wonder.InvestmentEnabled);
        }

        [Fact]
        public void Wonder_Investment_StopsResourceWhenFullyInvested()
        {
            var (state, wonder, clock, _) = CreateSetup();
            var civ = state.PlayerCivilization;
            var cost = WonderController.GetLevelCost(1);

            // Food manque 1 unité, le joueur a de quoi fournir cette dernière unité
            wonder.InvestedResources[Resource.Food] = cost[Resource.Food] - 1;
            civ.AddResource(Resource.Food, 110); // amount = 1 → complète Food
            wonder.InvestmentEnabled.Add(Resource.Food);

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            // Food atteint son seuil → retiré de InvestmentEnabled
            Assert.DoesNotContain(Resource.Food, wonder.InvestmentEnabled);
        }

        // ── Pas de progression sans wonder ───────────────────────────────────

        [Fact]
        public void Wonder_NoWonderInState_NothingHappens()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.PlayerCivilization;
            civ.Cities[0].Buildings.Add(new TownHall { Level = TownHallLevel });
            civ.AddResource(Resource.Food, 110);

            var clock = new GameClock();
            clock.Start();
            var controller = new WonderController();
            controller.Initialize(state, clock);

            clock.SimulateAdvance(WonderController.InvestmentIntervalTicks);

            Assert.Equal(110, civ.GetResourceQuantity(Resource.Food));
        }
    }
}
