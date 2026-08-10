using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Tests d'AbyssGateController : éligibilité et évolution de la Spire de Corruption en Faille des
    /// Abysses. L'éligibilité (IsAbyssGateEligible) se base sur RunRecord.MaxCorruptionLevelCleared
    /// — le meilleur nettoyage de Corruption réalisé sur l'île courante, n'importe où sur la carte et
    /// par n'importe quel mécanisme (Temple, débordement, annulation par le Dominion, décroissance de
    /// monument) — et non sur la corruption courante ou passée du seul hex de la Spire, ni sur le
    /// record global de la partie (PrestigeState.MaxCorruptionLevelCleared, réservé au bonus de
    /// prestige) : une Faille ouverte lors d'un run précédent ne dispense pas du nettoyage sur la
    /// nouvelle île. Voir CorruptionControllerTests pour un scénario de bout en bout mettant à jour ce
    /// record via annulation avec le Dominion sur un hex distinct de celui de la Spire.
    /// </summary>
    public class AbyssGateControllerTests
    {
        private static HexCoord UnderworldHex => new(0, 0, LayerState.UnderworldZ);

        private const int TownHallLevel = 20;

        private static (WorldState state, GameClock clock, CorruptionSpireController spireController, AbyssGateController gateController, PrestigeState prestigeState) CreateSetup(int maxCorruptionLevelCleared = 0, int lifetimeCorruptionLevelCleared = 0)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].AddBuilding(new TownHall { Level = TownHallLevel });
            BuildingController.RecalculateStorageCapacity(state.PlayerCivilization);

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            // Zone corrompue requise pour placer la Spire — son niveau n'entre plus en jeu dans l'éligibilité.
            state.AddFeature(new Corruption(UnderworldHex));

            // Un avant-poste de l'Inframonde touchant UnderworldHex : requis pour investir
            // (l'investissement d'un Monument n'est possible que ville adjacente).
            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var clock = new GameClock();
            clock.Start();

            // Record du run (île courante) : c'est lui, et lui seul, qui conditionne l'éligibilité.
            state.RunRecord.MaxCorruptionLevelCleared = maxCorruptionLevelCleared;
            // Record global de la partie : ne sert qu'au bonus de prestige, jamais à l'éligibilité.
            var prestigeState = new PrestigeState { MaxCorruptionLevelCleared = lifetimeCorruptionLevelCleared };

            var spireController = new CorruptionSpireController();
            spireController.Initialize(state, clock);
            var gateController = new AbyssGateController();
            gateController.Initialize(state, clock);

            return (state, clock, spireController, gateController, prestigeState);
        }

        private static void BuildSpireInstantly(WorldState state, CorruptionSpire spire)
        {
            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.Built = true;
        }

        [Fact]
        public void IsAbyssGateEligible_FalseWithoutSpire()
        {
            var (_, _, _, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            Assert.False(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_FalseWhenSpireNotBuilt()
        {
            var (_, _, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            spireController.PlaceCorruptionSpire(UnderworldHex);

            Assert.False(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_FalseWhenCleanupRecordBelowThreshold()
        {
            var (state, _, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel - 1);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);

            Assert.False(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_TrueWhenCleanupRecordAtThreshold()
        {
            var (state, _, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);

            Assert.True(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_TrueWhenCleanupHappenedOnAnUnrelatedHex()
        {
            // Le hex de la Spire (UnderworldHex) ne porte qu'une corruption de niveau 1 (voir
            // CreateSetup) et ne l'a jamais dépassé — seul un nettoyage ailleurs sur la carte (simulé
            // ici directement via RunRecord ; voir CorruptionControllerTests pour le scénario
            // complet via annulation avec le Dominion) suffit à rendre la Spire éligible.
            var (state, _, spireController, gateController, _) = CreateSetup();
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);
            Assert.False(gateController.IsAbyssGateEligible());

            state.RunRecord.MaxCorruptionLevelCleared = AbyssGate.RequiredCorruptionLevel;

            Assert.True(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_FalseWhenOnlyLifetimeRecordAtThreshold()
        {
            // Run suivant une partie où une Faille a déjà été ouverte : le record global de la partie
            // reste au-dessus du seuil, mais rien n'a encore été nettoyé sur cette île — la Faille doit
            // être re-méritée, pas offerte par l'historique.
            var (state, _, spireController, gateController, _) = CreateSetup(
                maxCorruptionLevelCleared: 0,
                lifetimeCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel * 2);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);

            Assert.False(gateController.IsAbyssGateEligible());
            Assert.Null(gateController.PlaceAbyssGate());
        }

        [Fact]
        public void PlaceAbyssGate_ReplacesSpireOnSameHex()
        {
            var (state, _, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);

            var gate = gateController.PlaceAbyssGate();

            Assert.NotNull(gate);
            Assert.Equal(UnderworldHex, gate!.Position);
            Assert.False(gate.Built);
            Assert.DoesNotContain(state.Features.OfType<CorruptionSpire>(), f => f.Position.Equals(UnderworldHex));
            Assert.Contains(state.Features.OfType<AbyssGate>(), f => f.Position.Equals(UnderworldHex));
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGatePlaced);
        }

        [Fact]
        public void PlaceAbyssGate_ReturnsNullWhenNotEligible()
        {
            var (_, _, spireController, gateController, _) = CreateSetup();
            spireController.PlaceCorruptionSpire(UnderworldHex);

            Assert.Null(gateController.PlaceAbyssGate());
        }

        [Fact]
        public void Investment_CompletingAllResources_BuildsAbyssGate()
        {
            var (state, clock, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);
            var gate = gateController.PlaceAbyssGate()!;

            var cost = AbyssGate.GetGateCost();
            foreach (var kvp in cost)
            {
                gate.InvestedResources[kvp.Key] = kvp.Value;
                gate.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(AbyssGateController.InvestmentIntervalTicks);

            Assert.True(gate.Built);
            Assert.True(gateController.HasAbyssGateBuilt());
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateBuilt);
        }

        [Fact]
        public void Investment_CompletingAllResources_OpensAbyss()
        {
            var (state, clock, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);
            var gate = gateController.PlaceAbyssGate()!;
            int citiesBefore = state.PlayerCivilization.Cities.Count;

            var cost = AbyssGate.GetGateCost();
            foreach (var kvp in cost)
            {
                gate.InvestedResources[kvp.Key] = kvp.Value;
                gate.InvestmentEnabled.Add(kvp.Key);
            }

            clock.SimulateAdvance(AbyssGateController.InvestmentIntervalTicks);

            // L'Abysse s'ouvre dans la foulée de la Faille avec un avant-poste pour le joueur, entouré de Void
            Assert.True(gate.Built);
            Assert.True(state.Layers.ContainsKey(LayerState.AbyssZ));
            Assert.Equal(citiesBefore + 1, state.PlayerCivilization.Cities.Count);
            Assert.Contains(state.PlayerCivilization.Cities, c => c.Position.Z == LayerState.AbyssZ);
            Assert.Contains(state.Layers[LayerState.AbyssZ].Map.Tiles.Values, t => t.TerrainType == TerrainType.Void);
        }

        [Fact]
        public void Abyss_NotOpened_WhileGateNotBuilt()
        {
            var (state, clock, spireController, gateController, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);
            gateController.PlaceAbyssGate();

            clock.SimulateAdvance(AbyssGateController.InvestmentIntervalTicks * 2);

            Assert.False(state.Layers.ContainsKey(LayerState.AbyssZ));
        }

        [Fact]
        public void CorruptionSpireController_RaisesAbyssGateEligibleToast_WhenSpireFinishesAndCleanupRecordAlreadyAtThreshold()
        {
            var (state, clock, spireController, _, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.InvestmentEnabled.Add(Resource.Stone);

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateEligible && e.Toast);
        }

        [Fact]
        public void CorruptionSpireController_DoesNotRaiseAbyssGateEligibleToast_WhenCleanupRecordBelowThreshold()
        {
            var (state, clock, spireController, _, _) = CreateSetup(maxCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel - 1);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.InvestmentEnabled.Add(Resource.Stone);

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateEligible);
        }

        [Fact]
        public void CorruptionSpireController_DoesNotRaiseAbyssGateEligibleToast_WhenOnlyLifetimeRecordAtThreshold()
        {
            var (state, clock, spireController, _, _) = CreateSetup(
                maxCorruptionLevelCleared: 0,
                lifetimeCorruptionLevelCleared: AbyssGate.RequiredCorruptionLevel * 2);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;

            var cost = CorruptionSpire.GetSpireCost();
            foreach (var kvp in cost)
                spire.InvestedResources[kvp.Key] = kvp.Value;
            spire.InvestmentEnabled.Add(Resource.Stone);

            clock.SimulateAdvance(CorruptionSpireController.InvestmentIntervalTicks);

            Assert.True(spire.Built);
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateEligible);
        }
    }
}
