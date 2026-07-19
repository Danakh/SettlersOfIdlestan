using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    public class CorruptionSpireControllerTests
    {
        private static HexCoord UnderworldHex => new(0, 0, LayerState.UnderworldZ);

        private const int TownHallLevel = 20;

        private static void UnlockAbyss(Civilization civ, double level)
            => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.UNLOCK_ABYSS, EType.ADDITIVE, level),
            }));

        private static (WorldState state, GameClock clock, CorruptionSpireController controller) CreateSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].Buildings.Add(new TownHall { Level = TownHallLevel });
            BuildingController.RecalculateStorageCapacity(state.PlayerCivilization);

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex));

            // Un avant-poste de l'Inframonde touchant UnderworldHex : requis pour investir
            // (l'investissement d'un Monument n'est possible que ville adjacente).
            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var clock = new GameClock();
            clock.Start();

            var controller = new CorruptionSpireController();
            controller.Initialize(state, clock);

            return (state, clock, controller);
        }

        [Fact]
        public void CanPlaceCorruptionSpire_FalseBelowAbyssThreshold()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 2);
            Assert.False(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceCorruptionSpire_TrueAtAbyssThreshold()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 3);
            Assert.True(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceCorruptionSpire_FalseWhenAlreadyPlaced()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 3);
            controller.PlaceCorruptionSpire(UnderworldHex);
            Assert.False(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));
        }

        [Fact]
        public void GetPlaceableHexes_OnlyCorruptedUnderworldHexes()
        {
            var (state, _, controller) = CreateSetup();
            var hexes = controller.GetPlaceableHexes();
            Assert.Equal(new[] { UnderworldHex }, hexes);
        }

        [Fact]
        public void GetPlaceableHexes_ExcludesHexWithOtherFeature()
        {
            var (state, _, controller) = CreateSetup();
            state.AddFeature(new TreasureTrove(UnderworldHex));
            Assert.Empty(controller.GetPlaceableHexes());
        }

        [Fact]
        public void PlaceCorruptionSpire_AddsFeatureAndLogsEvent()
        {
            var (state, _, controller) = CreateSetup();
            var spire = controller.PlaceCorruptionSpire(UnderworldHex);

            Assert.NotNull(spire);
            Assert.False(spire!.Built);
            Assert.Contains(state.Features.OfType<CorruptionSpire>(), f => f.Position.Equals(UnderworldHex));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.CorruptionSpirePlaced);
        }

        // ── Construction par investissement ──────────────────────────────────

        [Fact]
        public void SpireCost_Includes200Mithril()
        {
            var cost = CorruptionSpire.GetSpireCost();
            Assert.Equal(20000, cost[Resource.Stone]);
            Assert.Equal(20000, cost[Resource.Gold]);
            Assert.Equal(2000, cost[Resource.Steel]);
            Assert.Equal(1000, cost[Resource.Crystal]);
            Assert.Equal(200, cost[Resource.Mithril]);
        }

        [Fact]
        public void Investment_ConsumesResourceAndInvests()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            civ.AddResource(Resource.Stone, 110); // basic max = 110, amount = 1
            spire.InvestmentEnabled.Add(Resource.Stone);

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.Equal(109, civ.GetResourceQuantity(Resource.Stone));
            Assert.Equal(1L, spire.InvestedResources[Resource.Stone]);
            Assert.False(spire.Built);
        }

        [Fact]
        public void Investment_CompletingAllResources_BuildsSpire()
        {
            var (state, clock, controller) = CreateSetup();
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.CorruptionSpireBuilt);
            Assert.True(controller.HasCorruptionSpireBuilt());
            Assert.Equal(1, spire.Radius);

            // Investissement de construction réinitialisé — le panneau bascule désormais sur le
            // coût d'amélioration du rayon (Radius + 1).
            Assert.Empty(spire.InvestedResources);
            Assert.Empty(spire.InvestmentEnabled);
        }

        [Fact]
        public void Investment_ContinuesAfterBuilt_UpgradesRadiusIndefinitely()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;

            var buildCost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in buildCost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.InvestmentEnabled.Add(Resource.Stone);
            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);
            Assert.True(spire.Built);
            Assert.Equal(1, spire.Radius);

            // Une fois bâtie, l'investissement reprend pour améliorer le rayon : le coût du premier
            // niveau (rayon 2) est celui de base, chaque niveau suivant coûtant 50% de plus.
            var radius2Cost = CorruptionSpire.GetRadiusUpgradeCost(2);
            var radius3Cost = CorruptionSpire.GetRadiusUpgradeCost(3);
            Assert.Equal(buildCost[Resource.Stone], radius2Cost[Resource.Stone]);
            Assert.Equal((int)Math.Round(radius2Cost[Resource.Stone] * 1.5), radius3Cost[Resource.Stone]);

            foreach (var kvp in radius2Cost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.InvestmentEnabled.Add(Resource.Stone);
            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.Equal(2, spire.Radius);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.CorruptionSpireRadiusUpgraded);
            Assert.Empty(spire.InvestedResources);
            Assert.Empty(spire.InvestmentEnabled);
        }

        [Fact]
        public void HasCorruptionSpireBuilt_FalseWhileUnderConstruction()
        {
            var (_, _, controller) = CreateSetup();
            controller.PlaceCorruptionSpire(UnderworldHex);
            Assert.False(controller.HasCorruptionSpireBuilt());
        }
    }
}
