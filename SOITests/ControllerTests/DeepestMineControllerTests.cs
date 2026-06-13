using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
// Lève l'ambiguïté avec le bâtiment legacy Model.Buildings.DeepestMine
using DeepestMine = SettlersOfIdlestan.Model.IslandFeatures.DeepestMine;

namespace SOITests.ControllerTests
{
    public class DeepestMineControllerTests
    {
        private static HexCoord MountainHex => new(0, -1, IslandMap.SurfaceLayer);

        private const int TownHallLevel = 20;

        private static void UnlockDeepestMine(Civilization civ)
            => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.UNLOCK_DEEPEST_MINE, EType.ADDITIVE, 1),
            }));

        /// <summary>Ajoute une ville du joueur touchant l'hex Montagne (0,-1) de l'île de test.</summary>
        private static void AddMountainCity(WorldState state)
        {
            var center = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var se = new HexCoord(1, -1, IslandMap.SurfaceLayer);
            var vertex = Vertex.Create(center, MountainHex, se);
            var city = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(city);
        }

        private static (WorldState state, GameClock clock, DeepestMineController controller) CreateSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].Buildings.Add(new TownHall { Level = TownHallLevel });

            var clock = new GameClock();
            clock.Start();

            var controller = new DeepestMineController();
            controller.Initialize(state, clock);

            return (state, clock, controller);
        }

        // ── Déblocage et placement ───────────────────────────────────────────

        [Fact]
        public void CanPlaceDeepestMine_FalseWithoutPrestigeUnlock()
        {
            var (state, _, controller) = CreateSetup();
            Assert.False(controller.CanPlaceDeepestMine(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceDeepestMine_TrueWithPrestigeUnlock()
        {
            var (state, _, controller) = CreateSetup();
            UnlockDeepestMine(state.PlayerCivilization);
            Assert.True(controller.CanPlaceDeepestMine(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceDeepestMine_FalseWhenAlreadyPlaced()
        {
            var (state, _, controller) = CreateSetup();
            UnlockDeepestMine(state.PlayerCivilization);
            controller.PlaceDeepestMine(MountainHex);
            Assert.False(controller.CanPlaceDeepestMine(state.PlayerCivilization));
        }

        [Fact]
        public void GetPlaceableHexes_OnlyMountainHexesAdjacentToPlayerCities()
        {
            var (state, _, controller) = CreateSetup();

            // La ville par défaut (Plain/Plain/Forest) n'offre aucune Montagne
            Assert.Empty(controller.GetPlaceableHexes());

            // Une ville touchant la Montagne (0,-1) la rend éligible
            AddMountainCity(state);
            var hexes = controller.GetPlaceableHexes();
            Assert.Equal(new[] { MountainHex }, hexes);
        }

        [Fact]
        public void PlaceDeepestMine_AddsFeatureAndLogsEvent()
        {
            var (state, _, controller) = CreateSetup();
            var mine = controller.PlaceDeepestMine(MountainHex);

            Assert.NotNull(mine);
            Assert.Contains(state.Features.OfType<DeepestMine>(), m => m.Position.Equals(MountainHex));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.DeepestMinePlaced);
        }

        // ── Creusement par investissement ────────────────────────────────────

        [Fact]
        public void Investment_ConsumesResourceAndInvests()
        {
            var (state, clock, controller) = CreateSetup();
            var civ = state.PlayerCivilization;
            var mine = controller.PlaceDeepestMine(MountainHex)!;

            civ.AddResource(Resource.Stone, 110); // basic max = 110, amount = 1
            mine.InvestmentEnabled.Add(Resource.Stone);

            clock.SimulateAdvance(DeepestMineController.InvestmentIntervalTicks);

            Assert.Equal(109, civ.GetResourceQuantity(Resource.Stone));
            Assert.Equal(1L, mine.InvestedResources[Resource.Stone]);
            Assert.False(mine.Dug);
        }

        [Fact]
        public void Investment_DigCostIncludes1000Steel()
        {
            var cost = DeepestMine.GetDigCost();
            Assert.Equal(1000, cost[Resource.Steel]);
        }

        [Fact]
        public void Investment_CompletingAllResources_DigsAndOpensUnderworld()
        {
            var (state, clock, controller) = CreateSetup();
            var mine = controller.PlaceDeepestMine(MountainHex)!;
            int citiesBefore = state.PlayerCivilization.Cities.Count;

            var cost = DeepestMine.GetDigCost();
            foreach (var kvp in cost)
            {
                mine.InvestedResources[kvp.Key] = kvp.Value;
                mine.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(DeepestMineController.InvestmentIntervalTicks);

            Assert.True(mine.Dug);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.DeepestMineDug);

            // L'Inframonde s'ouvre au tick suivant avec un avant-poste pour le joueur
            clock.SimulateAdvance(1);
            Assert.True(state.Layers.ContainsKey(LayerState.UnderworldZ));
            Assert.Equal(citiesBefore + 1, state.PlayerCivilization.Cities.Count);
            Assert.Contains(state.PlayerCivilization.Cities, c => c.Position.Z == LayerState.UnderworldZ);
        }

        [Fact]
        public void Underworld_NotOpened_WhileMineNotDug()
        {
            var (state, clock, controller) = CreateSetup();
            controller.PlaceDeepestMine(MountainHex);

            clock.SimulateAdvance(DeepestMineController.InvestmentIntervalTicks * 2);

            Assert.False(state.Layers.ContainsKey(LayerState.UnderworldZ));
        }

        // ── Perte de l'Inframonde ────────────────────────────────────────────

        private static DeepestMine DigMineAndOpenUnderworld(WorldState state, GameClock clock, DeepestMineController controller)
        {
            var mine = controller.PlaceDeepestMine(MountainHex)!;
            var cost = DeepestMine.GetDigCost();
            foreach (var kvp in cost)
            {
                mine.InvestedResources[kvp.Key] = kvp.Value;
                mine.InvestmentEnabled.Add(kvp.Key);
            }
            clock.SimulateAdvance(DeepestMineController.InvestmentIntervalTicks + 1);
            return mine;
        }

        [Fact]
        public void LastUnderworldCityDestroyed_ResetsMineTo50Percent()
        {
            var (state, clock, controller) = CreateSetup();
            var mine = DigMineAndOpenUnderworld(state, clock, controller);

            var underworldCity = state.PlayerCivilization.Cities.First(c => c.Position.Z == LayerState.UnderworldZ);
            state.PlayerCivilization.RemoveCity(underworldCity);
            controller.OnCityDestroyed(underworldCity.Position, state.PlayerCivilization.Index);

            // La couche reste (vide) pour que GetMapFor reste valide
            Assert.True(state.Layers.ContainsKey(LayerState.UnderworldZ));
            Assert.Empty(state.GetMapForZ(LayerState.UnderworldZ)!.Tiles);
            // Plus d'avant-poste joueur
            Assert.DoesNotContain(state.PlayerCivilization.Cities, c => c.Position.Z == LayerState.UnderworldZ);
            // Mine réouvrable
            Assert.False(mine.Dug);
            // Ressources à 50 %
            var cost = DeepestMine.GetDigCost();
            foreach (var kvp in cost)
                Assert.Equal(kvp.Value / 2, mine.InvestedResources[kvp.Key]);
        }

        [Fact]
        public void LastUnderworldCityDestroyed_IconUnchanged()
        {
            var (state, clock, controller) = CreateSetup();
            var mine = DigMineAndOpenUnderworld(state, clock, controller);
            var iconBefore = mine.SvgIconResourceName;

            var underworldCity = state.PlayerCivilization.Cities.First(c => c.Position.Z == LayerState.UnderworldZ);
            state.PlayerCivilization.RemoveCity(underworldCity);
            controller.OnCityDestroyed(underworldCity.Position, state.PlayerCivilization.Index);

            Assert.True(mine.WasEverDug);
            Assert.Equal(iconBefore, mine.SvgIconResourceName);
        }

        [Fact]
        public void LastUnderworldCityDestroyed_NewUnderworldOpensAfterRedigging()
        {
            var (state, clock, controller) = CreateSetup();
            var mine = DigMineAndOpenUnderworld(state, clock, controller);

            var underworldCity = state.PlayerCivilization.Cities.First(c => c.Position.Z == LayerState.UnderworldZ);
            state.PlayerCivilization.RemoveCity(underworldCity);
            controller.OnCityDestroyed(underworldCity.Position, state.PlayerCivilization.Index);

            // Complète l'investissement restant (50 %)
            var cost = DeepestMine.GetDigCost();
            foreach (var kvp in cost)
                mine.InvestedResources[kvp.Key] = kvp.Value;
            mine.InvestmentEnabled.AddRange(cost.Keys);

            clock.SimulateAdvance(DeepestMineController.InvestmentIntervalTicks + 1);

            Assert.True(mine.Dug);
            Assert.True(state.Layers.ContainsKey(LayerState.UnderworldZ));
        }
    }
}
