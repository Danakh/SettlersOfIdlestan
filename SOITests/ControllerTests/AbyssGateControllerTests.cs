using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    public class AbyssGateControllerTests
    {
        private static HexCoord UnderworldHex => new(0, 0, LayerState.UnderworldZ);

        private const int TownHallLevel = 20;

        private static (WorldState state, GameClock clock, CorruptionSpireController spireController, AbyssGateController gateController) CreateSetup(int corruptionLevel)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            state.PlayerCivilization.Cities[0].Buildings.Add(new TownHall { Level = TownHallLevel });
            BuildingController.RecalculateStorageCapacity(state.PlayerCivilization);

            var tiles = new[] { new HexTile(UnderworldHex, TerrainType.Mountain) };
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(tiles, LayerState.UnderworldZ)));
            state.AddFeature(new Corruption(UnderworldHex, corruptionLevel));

            // Un avant-poste de l'Inframonde touchant UnderworldHex : requis pour investir
            // (l'investissement d'un Monument n'est possible que ville adjacente).
            var vertex = Vertex.Create(UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE));
            var outpost = new City(vertex) { CivilizationIndex = state.PlayerCivilization.Index };
            state.PlayerCivilization.AddCity(outpost);

            var clock = new GameClock();
            clock.Start();

            var spireController = new CorruptionSpireController();
            spireController.Initialize(state, clock);
            var gateController = new AbyssGateController();
            gateController.Initialize(state, clock);

            return (state, clock, spireController, gateController);
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
            var (_, _, _, gateController) = CreateSetup(corruptionLevel: 4);
            Assert.False(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_FalseWhenSpireNotBuilt()
        {
            var (state, _, spireController, gateController) = CreateSetup(corruptionLevel: 4);
            spireController.PlaceCorruptionSpire(UnderworldHex);

            Assert.False(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_FalseWhenCorruptionBelowThreshold()
        {
            var (state, _, spireController, gateController) = CreateSetup(corruptionLevel: 3);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);

            Assert.False(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void IsAbyssGateEligible_TrueWhenSpireBuiltOnHighCorruption()
        {
            var (state, _, spireController, gateController) = CreateSetup(corruptionLevel: AbyssGate.RequiredCorruptionLevel);
            var spire = spireController.PlaceCorruptionSpire(UnderworldHex)!;
            BuildSpireInstantly(state, spire);

            Assert.True(gateController.IsAbyssGateEligible());
        }

        [Fact]
        public void PlaceAbyssGate_ReplacesSpireOnSameHex()
        {
            var (state, _, spireController, gateController) = CreateSetup(corruptionLevel: AbyssGate.RequiredCorruptionLevel);
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
            var (state, _, spireController, gateController) = CreateSetup(corruptionLevel: 1);
            spireController.PlaceCorruptionSpire(UnderworldHex);

            Assert.Null(gateController.PlaceAbyssGate());
        }

        [Fact]
        public void Investment_CompletingAllResources_BuildsAbyssGate()
        {
            var (state, clock, spireController, gateController) = CreateSetup(corruptionLevel: AbyssGate.RequiredCorruptionLevel);
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
        public void CorruptionSpireController_RaisesAbyssGateEligibleToast_WhenSpireFinishesOnHighCorruption()
        {
            var (state, clock, spireController, _) = CreateSetup(corruptionLevel: AbyssGate.RequiredCorruptionLevel);
            var civ = state.PlayerCivilization;
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
        public void CorruptionSpireController_DoesNotRaiseAbyssGateEligibleToast_WhenCorruptionBelowThreshold()
        {
            var (state, clock, spireController, _) = CreateSetup(corruptionLevel: AbyssGate.RequiredCorruptionLevel - 1);
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
