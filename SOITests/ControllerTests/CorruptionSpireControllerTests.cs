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
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex));
            state.AddFeature(new CorruptionSource(UnderworldHex, corruptionLevel: 1));

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
        public void DestroyCorruptionSpire_RemovesSpireAndAllowsReplacement()
        {
            var (state, _, controller) = CreateSetup();
            UnlockAbyss(state.PlayerCivilization, 3);
            var spire = controller.PlaceCorruptionSpire(UnderworldHex);
            spire!.Built = true;
            spire.Radius = 4;

            Assert.True(controller.DestroyCorruptionSpire());

            Assert.Empty(state.Features.OfType<CorruptionSpire>());
            Assert.False(controller.HasCorruptionSpireBuilt());
            Assert.True(controller.CanPlaceCorruptionSpire(state.PlayerCivilization));

            // Reconstruction complète : la nouvelle Spire repart d'un rayon 1, non bâtie.
            var rebuilt = controller.PlaceCorruptionSpire(UnderworldHex);
            Assert.False(rebuilt!.Built);
            Assert.Equal(1, rebuilt.Radius);
        }

        [Fact]
        public void DestroyCorruptionSpire_FalseWhenNoSpire()
        {
            var (_, _, controller) = CreateSetup();
            Assert.False(controller.DestroyCorruptionSpire());
        }

        [Fact]
        public void GetPlaceableHexes_OnlyUnderworldHexesWithACorruptionSource()
        {
            var (state, _, controller) = CreateSetup();
            var hexes = controller.GetPlaceableHexes();
            Assert.Equal(new[] { UnderworldHex }, hexes);
        }

        [Fact]
        public void GetPlaceableHexes_ExcludesCorruptedHexWithoutASource()
        {
            // Une zone simplement corrompue, sans Source de Corruption, ne suffit plus (voir
            // AutoExtendController.TrySpawnUnderworldDenizen : seule une Source, semée avec 50% de
            // chance quand le tirage de Corruption atteint le plafond de l'île, rend l'hex éligible).
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex));

            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var controller = new CorruptionSpireController();
            controller.Initialize(state);

            Assert.Empty(controller.GetPlaceableHexes());
        }

        [Fact]
        public void GetPlaceableHexes_IncludesSourceHexEvenWhenDominionReplacedItsCorruption()
        {
            // CorruptionController.GrowOrSeedCorruptionOnHex fait combattre un Dominion existant par
            // la Source plutôt que d'y semer de la Corruption par-dessus : l'hex peut donc se
            // retrouver avec la Source mais du Dominion à la place de la Corruption. La Spire doit
            // rester plaçable sur cet hex — la Source y est toujours présente.
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            state.PlayerCivilization.RecalculateStorageCapacity();

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new CorruptionSource(UnderworldHex, corruptionLevel: 1));
            state.AddFeature(new Dominion(UnderworldHex, level: 4));

            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var controller = new CorruptionSpireController();
            controller.Initialize(state);

            Assert.Equal(new[] { UnderworldHex }, controller.GetPlaceableHexes());
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

        [Fact]
        public void Investment_CompletingAllResources_DestroysTheCorruptionSourceOnItsHex()
        {
            var (state, clock, controller) = CreateSetup();
            var spire = controller.PlaceCorruptionSpire(UnderworldHex)!;
            Assert.NotEmpty(state.Features.OfType<CorruptionSource>());

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
            {
                spire.InvestedResources[kvp.Key] = kvp.Value;
                spire.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.Empty(state.Features.OfType<CorruptionSource>());
            // La Corruption qu'elle engendrait, elle, n'est pas retirée par ce mécanisme.
            Assert.Contains(state.Features.OfType<Corruption>(), f => f.Position.Equals(UnderworldHex));
        }
    }
}
