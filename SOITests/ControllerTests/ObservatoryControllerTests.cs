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

namespace SOITests.ControllerTests
{
    public class ObservatoryControllerTests
    {
        private static HexCoord MountainHex => new(0, -1, IslandMap.SurfaceLayer);

        private const int TownHallLevel = 20;

        private static void UnlockObservatory(Civilization civ)
            => civ.AddCustomAggregator(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.UNLOCK_OBSERVATORY, EType.ADDITIVE, 1),
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

        private static (WorldState state, GameClock clock, ObservatoryController controller) CreateSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            BuildingController.RecalculateStorageCapacity(state.PlayerCivilization);

            var clock = new GameClock();
            clock.Start();

            var controller = new ObservatoryController();
            controller.Initialize(state, clock);

            return (state, clock, controller);
        }

        /// <summary>Amène l'Observatoire au bord du level-up : tout est investi, il ne manque que le tick.</summary>
        private static void FillInvestment(Observatory observatory, Civilization playerCiv)
        {
            foreach (var kvp in observatory.GetInvestmentCost(playerCiv))
            {
                observatory.InvestedResources[kvp.Key] = kvp.Value;
                observatory.InvestmentEnabled.Add(kvp.Key);
            }
            observatory.InvestedResearch = observatory.GetRequiredResearch(playerCiv);
        }

        // ── Déblocage et placement ───────────────────────────────────────────

        [Fact]
        public void CanPlaceObservatory_FalseWithoutResearch()
        {
            var (state, _, controller) = CreateSetup();
            Assert.False(controller.CanPlaceObservatory(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceObservatory_TrueWithResearch()
        {
            var (state, _, controller) = CreateSetup();
            UnlockObservatory(state.PlayerCivilization);
            Assert.True(controller.CanPlaceObservatory(state.PlayerCivilization));
        }

        [Fact]
        public void CanPlaceObservatory_FalseWhenAlreadyPlaced()
        {
            var (state, _, controller) = CreateSetup();
            UnlockObservatory(state.PlayerCivilization);
            controller.PlaceObservatory(MountainHex);
            Assert.False(controller.CanPlaceObservatory(state.PlayerCivilization));
        }

        [Fact]
        public void GetPlaceableHexes_OnlyMountainHexesAdjacentToPlayerCities()
        {
            var (state, _, controller) = CreateSetup();

            // La ville par défaut (Plain/Plain/Forest) n'offre aucune Montagne
            Assert.Empty(controller.GetPlaceableHexes());

            AddMountainCity(state);
            Assert.Equal(new[] { MountainHex }, controller.GetPlaceableHexes());
        }

        [Fact]
        public void PlaceObservatory_AddsFeatureAndLogsEvent()
        {
            var (state, _, controller) = CreateSetup();
            var observatory = controller.PlaceObservatory(MountainHex);

            Assert.NotNull(observatory);
            Assert.Equal(0, observatory!.Level);
            Assert.Contains(state.Features.OfType<Observatory>(), o => o.Position.Equals(MountainHex));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.ObservatoryPlaced);
        }

        // ── Investissement ───────────────────────────────────────────────────

        [Fact]
        public void Investment_ConsumesResearchPoints()
        {
            var (state, clock, controller) = CreateSetup();
            AddMountainCity(state);
            var civ = state.PlayerCivilization;
            var observatory = controller.PlaceObservatory(MountainHex)!;

            civ.TechnologyTree.ResearchPoints = 1000;
            observatory.ResearchInvestmentEnabled = true;

            clock.SimulateAdvance(ObservatoryController.InvestmentIntervalTicks);

            // 1% du pool par cycle, comme l'investissement en ressources
            Assert.Equal(990, civ.TechnologyTree.ResearchPoints);
            Assert.Equal(10L, observatory.InvestedResearch);
            Assert.Equal(0, observatory.Level);
        }

        [Fact]
        public void Investment_ResourcesCompleteButResearchMissing_DoesNotLevelUp()
        {
            var (state, clock, controller) = CreateSetup();
            AddMountainCity(state);
            var civ = state.PlayerCivilization;
            var observatory = controller.PlaceObservatory(MountainHex)!;

            foreach (var kvp in observatory.GetInvestmentCost(civ))
            {
                observatory.InvestedResources[kvp.Key] = kvp.Value;
                observatory.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(ObservatoryController.InvestmentIntervalTicks);

            Assert.Equal(0, observatory.Level);
        }

        [Fact]
        public void Investment_CompletingResourcesAndResearch_LevelsUpAndResetsPools()
        {
            var (state, clock, controller) = CreateSetup();
            AddMountainCity(state);
            var civ = state.PlayerCivilization;
            var observatory = controller.PlaceObservatory(MountainHex)!;

            FillInvestment(observatory, civ);

            clock.SimulateAdvance(ObservatoryController.InvestmentIntervalTicks);

            Assert.Equal(1, observatory.Level);
            Assert.Empty(observatory.InvestedResources);
            Assert.Empty(observatory.InvestmentEnabled);
            Assert.Equal(0L, observatory.InvestedResearch);
            Assert.False(observatory.ResearchInvestmentEnabled);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.ObservatoryLevelUp);
        }

        [Fact]
        public void Investment_StopsAtMaxLevel()
        {
            var (state, clock, controller) = CreateSetup();
            AddMountainCity(state);
            var civ = state.PlayerCivilization;
            var observatory = controller.PlaceObservatory(MountainHex)!;

            for (int i = 0; i < Observatory.MaxLevel; i++)
            {
                FillInvestment(observatory, civ);
                clock.SimulateAdvance(ObservatoryController.InvestmentIntervalTicks);
            }

            Assert.True(observatory.IsMaxLevel);

            // Un cycle de plus ne dépasse jamais le niveau maximum
            FillInvestment(observatory, civ);
            clock.SimulateAdvance(ObservatoryController.InvestmentIntervalTicks);
            Assert.Equal(Observatory.MaxLevel, observatory.Level);
        }

        // ── Effet sur le coût des routes du Vide ─────────────────────────────

        [Fact]
        public void VoidRouteCostMultiplier_GoesFrom3To2AcrossLevels()
        {
            Assert.Equal(3.0, Observatory.GetVoidRouteCostMultiplierForLevel(0), 6);
            Assert.Equal(2.0, Observatory.GetVoidRouteCostMultiplierForLevel(Observatory.MaxLevel), 6);

            // Strictement décroissant d'un niveau au suivant
            for (int level = 1; level <= Observatory.MaxLevel; level++)
                Assert.True(Observatory.GetVoidRouteCostMultiplierForLevel(level)
                          < Observatory.GetVoidRouteCostMultiplierForLevel(level - 1));
        }

        [Fact]
        public void VoidRouteResearchCost_UsesMultiplier()
        {
            // Sans Observatoire : 1 000 000 × 3^n
            Assert.Equal(1_000_000L, RoadController.GetVoidRouteResearchCost(0));
            Assert.Equal(9_000_000L, RoadController.GetVoidRouteResearchCost(2));

            // Observatoire complet : 1 000 000 × 2^n
            Assert.Equal(4_000_000L, RoadController.GetVoidRouteResearchCost(2, Observatory.CompletedVoidRouteCostMultiplier));
        }

        [Fact]
        public void RoadController_MultiplierFollowsObservatoryLevel()
        {
            var (state, clock, controller) = CreateSetup();
            AddMountainCity(state);
            var civ = state.PlayerCivilization;

            var roadController = new RoadController();
            roadController.Initialize(state);

            Assert.Equal(Observatory.BaseVoidRouteCostMultiplier, roadController.GetVoidRouteCostMultiplier(), 6);

            var observatory = controller.PlaceObservatory(MountainHex)!;
            Assert.Equal(Observatory.BaseVoidRouteCostMultiplier, roadController.GetVoidRouteCostMultiplier(), 6);

            for (int i = 0; i < Observatory.MaxLevel; i++)
            {
                FillInvestment(observatory, civ);
                clock.SimulateAdvance(ObservatoryController.InvestmentIntervalTicks);
            }

            Assert.Equal(Observatory.CompletedVoidRouteCostMultiplier, roadController.GetVoidRouteCostMultiplier(), 6);
        }
    }
}
