using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Generator;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Tests de PandemoniumGateController : le Portail du Pandémonium surgit à la mort d'une
    /// Tentacule de l'Abysse (et d'elle seule), se bâtit par investissement au même prix que la
    /// Faille des Abysses, puis ouvre la couche Pandémonium.
    /// </summary>
    public class PandemoniumGateControllerTests
    {
        private static HexCoord Abyss1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord Abyss2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord Abyss3 => new(0, 1, LayerState.AbyssZ);

        private static (WorldState state, GameClock clock, PandemoniumGateController controller) CreateSetup()
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var abyssTiles = new List<HexTile>
            {
                new(Abyss1, TerrainType.Mountain),
                new(Abyss2, TerrainType.Mountain),
                new(Abyss3, TerrainType.Mountain),
            };
            var arrivalVertex = Vertex.Create(Abyss1, Abyss2, Abyss3);
            state.AddLayer(LayerState.AbyssZ, new LayerState(new IslandMap(abyssTiles, LayerState.AbyssZ)) { ArrivalVertex = arrivalVertex });

            // Avant-poste de l'Abysse touchant Abyss1 : requis pour investir dans le portail.
            civ.AddCity(new City(arrivalVertex) { CivilizationIndex = civ.Index });

            var clock = new GameClock();
            clock.Start();

            var controller = new PandemoniumGateController();
            controller.Initialize(state, clock, prng: new GamePRNG(1));

            return (state, clock, controller);
        }

        /// <summary>Tue une Tentacule comme le fait le combat : PV à zéro, puis retrait de la feature.</summary>
        private static void Kill(WorldState state, Tentacle tentacle)
        {
            tentacle.Hp = 0;
            state.RemoveFeature(tentacle);
        }

        private static void FillInvestment(PandemoniumGate gate)
        {
            foreach (var kvp in AbyssGate.GetGateCost())
            {
                gate.InvestedResources[kvp.Key] = kvp.Value;
                gate.InvestmentEnabled.Add(kvp.Key);
            }
        }

        [Fact]
        public void KillingAbyssTentacle_PlacesUnbuiltGateOnItsHex()
        {
            var (state, _, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);

            Kill(state, tentacle);

            var gate = Assert.Single(state.Features.OfType<PandemoniumGate>());
            Assert.Equal(Abyss1, gate.Position);
            Assert.False(gate.Built);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.PandemoniumGatePlaced);
        }

        [Fact]
        public void GateCost_MatchesAbyssGateCost()
        {
            var (state, _, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            var gate = state.Features.OfType<PandemoniumGate>().Single();
            var expected = AbyssGate.GetGateCost();
            var actual = gate.GetInvestmentCost(state.PlayerCivilization);

            Assert.Equal(expected.Keys.OrderBy(k => k), actual.Keys.OrderBy(k => k));
            foreach (var resource in expected.Keys)
                Assert.Equal(expected[resource], actual[resource]);
        }

        [Fact]
        public void RemovingLivingTentacle_PlacesNoGate()
        {
            var (state, _, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);

            // Retrait sans combat (nettoyage d'une couche perdue) : pas de récompense.
            state.RemoveFeature(tentacle);

            Assert.Empty(state.Features.OfType<PandemoniumGate>());
        }

        [Fact]
        public void KillingPandemoniumTentacle_PlacesNoGate()
        {
            var (state, _, _) = CreateSetup();
            var tentacle = new Tentacle(new HexCoord(2, 0, LayerState.PandemoniumZ));
            state.AddFeature(tentacle);

            Kill(state, tentacle);

            Assert.Empty(state.Features.OfType<PandemoniumGate>());
        }

        [Fact]
        public void SecondTentacleKill_DoesNotPlaceASecondGate()
        {
            var (state, _, _) = CreateSetup();
            var first = new Tentacle(Abyss1);
            var second = new Tentacle(Abyss2);
            state.AddFeature(first);
            state.AddFeature(second);

            Kill(state, first);
            Kill(state, second);

            var gate = Assert.Single(state.Features.OfType<PandemoniumGate>());
            Assert.Equal(Abyss1, gate.Position);
        }

        [Fact]
        public void Investment_CompletingAllResources_BuildsGate()
        {
            var (state, clock, controller) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            var gate = state.Features.OfType<PandemoniumGate>().Single();
            FillInvestment(gate);

            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks);

            Assert.True(gate.Built);
            Assert.True(controller.HasPandemoniumGateBuilt());
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.PandemoniumGateBuilt);
        }

        [Fact]
        public void Investment_CompletingAllResources_OpensPandemonium()
        {
            var (state, clock, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            var gate = state.Features.OfType<PandemoniumGate>().Single();
            FillInvestment(gate);

            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks);

            Assert.True(state.Layers.ContainsKey(LayerState.PandemoniumZ));
            var city = Assert.Single(state.PlayerCivilization.Cities.Where(c => c.Position.Z == LayerState.PandemoniumZ));
            Assert.Equal(state.Layers[LayerState.PandemoniumZ].ArrivalVertex, city.Position);

            Assert.Single(state.Features.OfType<DemonGod>());
            Assert.Equal(PandemoniumGenerator.TentacleCount,
                state.Features.OfType<Tentacle>().Count(t => t.Position.Z == LayerState.PandemoniumZ));
        }

        [Fact]
        public void OpeningPandemonium_CorruptsEachMonsterHexAndItsNeighbours()
        {
            // Le dieu démon et ses Tentacules naissent au milieu de leur flaque : hex propre et six
            // voisins au niveau de corruption de l'île (1 ici, faute de PrestigeState), une seule
            // Corruption par hex même là où deux monstres se recouvrent.
            var (state, clock, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            FillInvestment(state.Features.OfType<PandemoniumGate>().Single());
            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks);

            var map = state.Layers[LayerState.PandemoniumZ].Map;
            var monsters = state.Features.OfType<MonsterFeature>()
                .Where(m => m.Position.Z == LayerState.PandemoniumZ).ToList();
            Assert.Equal(PandemoniumGenerator.TentacleCount + 1, monsters.Count);

            foreach (var monster in monsters)
                foreach (var hex in monster.Position.Neighbors().Append(monster.Position))
                {
                    if (map.GetTile(hex) is not { } tile || tile.TerrainType == TerrainType.Void) continue;
                    var corruption = Assert.Single(state.GetFeaturesAt(hex).OfType<Corruption>());
                    Assert.Equal(1, corruption.Level);
                }
        }
        [Fact]
        public void Pandemonium_NotOpened_WhileGateNotBuilt()
        {
            var (state, clock, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks * 2);

            Assert.False(state.Layers.ContainsKey(LayerState.PandemoniumZ));
        }

        [Fact]
        public void Pandemonium_OpensOnlyOnce()
        {
            var (state, clock, _) = CreateSetup();
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            var gate = state.Features.OfType<PandemoniumGate>().Single();
            FillInvestment(gate);

            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks);
            int citiesAfterOpening = state.PlayerCivilization.Cities.Count;
            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks * 3);

            Assert.Equal(citiesAfterOpening, state.PlayerCivilization.Cities.Count);
            Assert.Single(state.Features.OfType<DemonGod>());
        }
    }
}
