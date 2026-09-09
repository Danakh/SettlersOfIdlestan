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

        /// <summary>
        /// Le journal ne doit annoncer l'ouverture du portail que pour la Tentacule qui l'a
        /// réellement fait surgir : les suivantes tombent sur un portail déjà là, celles du
        /// Pandémonium n'en ouvrent jamais.
        /// </summary>
        [Fact]
        public void TentacleDefeatedEvent_AnnouncesTheGateOnlyForTheOneThatOpenedIt()
        {
            var (state, _, _) = CreateSetup();
            var first = new Tentacle(Abyss1);
            var second = new Tentacle(Abyss2);
            var pandemonium = new Tentacle(new HexCoord(2, 0, LayerState.PandemoniumZ));
            state.AddFeature(first);
            state.AddFeature(second);
            state.AddFeature(pandemonium);

            Kill(state, first);
            Kill(state, second);
            Kill(state, pandemonium);

            Assert.Equal(GameEventType.TentacleDefeated, first.RemovedEventType);
            Assert.Equal(GameEventType.TentacleDefeatedNoGate, second.RemovedEventType);
            Assert.Equal(GameEventType.TentacleDefeatedNoGate, pandemonium.RemovedEventType);
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

        /// <summary>Ouvre le Pandémonium et rend la main sur le portail et l'avant-poste qui y est né.</summary>
        private static (PandemoniumGate gate, City outpost) OpenPandemonium(WorldState state, GameClock clock)
        {
            var tentacle = new Tentacle(Abyss1);
            state.AddFeature(tentacle);
            Kill(state, tentacle);

            var gate = state.Features.OfType<PandemoniumGate>().Single();
            FillInvestment(gate);
            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks);

            return (gate, state.PlayerCivilization.Cities.Single(c => c.Position.Z == LayerState.PandemoniumZ));
        }

        [Fact]
        public void LosingLastPandemoniumCity_ResetsGateToHalfInvestment()
        {
            var (state, clock, controller) = CreateSetup();
            var (gate, outpost) = OpenPandemonium(state, clock);

            state.PlayerCivilization.RemoveCity(outpost);
            controller.OnCityDestroyed(outpost.Position, state.PlayerCivilization.Index);

            Assert.False(gate.Built);
            Assert.True(gate.WasEverBuilt);
            var cost = gate.GetInvestmentCost(state.PlayerCivilization);
            foreach (var kvp in cost)
                Assert.Equal(kvp.Value / 2, gate.InvestedResources[kvp.Key]);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.PandemoniumGateLost);
        }

        /// <summary>
        /// Le revers doit rester un choix à réparer : aucune ressource ne doit se remettre à couler
        /// vers le portail toute seule, même "Automatiser les Monuments" actif.
        /// </summary>
        [Fact]
        public void LosingLastPandemoniumCity_DoesNotRestartInvestmentOnItsOwn()
        {
            var (state, clock, controller) = CreateSetup();
            state.AutomationSettings.MonumentInvestmentAutomationEnabled = true;
            var (gate, outpost) = OpenPandemonium(state, clock);

            state.PlayerCivilization.RemoveCity(outpost);
            controller.OnCityDestroyed(outpost.Position, state.PlayerCivilization.Index);

            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks * 3);

            Assert.Empty(gate.InvestmentEnabled);
            Assert.False(gate.Built);
            Assert.False(state.Layers[LayerState.PandemoniumZ].Map.Tiles.Any());
        }

        [Fact]
        public void LosingLastPandemoniumCity_ClearsTheArena()
        {
            var (state, clock, controller) = CreateSetup();
            var (_, outpost) = OpenPandemonium(state, clock);

            state.PlayerCivilization.RemoveCity(outpost);
            controller.OnCityDestroyed(outpost.Position, state.PlayerCivilization.Index);

            Assert.Empty(state.Features.Where(f => f.Position.Z == LayerState.PandemoniumZ));
            Assert.Empty(state.Layers[LayerState.PandemoniumZ].Map.Tiles);
        }

        /// <summary>Perdre une ville des autres couches ne touche évidemment pas au portail.</summary>
        [Fact]
        public void LosingAnAbyssCity_LeavesTheGateBuilt()
        {
            var (state, clock, controller) = CreateSetup();
            var (gate, _) = OpenPandemonium(state, clock);
            var abyssCity = state.PlayerCivilization.Cities.Single(c => c.Position.Z == LayerState.AbyssZ);

            state.PlayerCivilization.RemoveCity(abyssCity);
            controller.OnCityDestroyed(abyssCity.Position, state.PlayerCivilization.Index);

            Assert.True(gate.Built);
            Assert.NotEmpty(state.Layers[LayerState.PandemoniumZ].Map.Tiles);
        }
    }
}
