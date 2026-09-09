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
using SettlersOfIdlestan.Model.Prestige;
using SOITests.TestUtilities;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Perte en chaîne des couches profondes sur une partie qui les possède toutes les quatre
    /// (Surface, Inframonde, Abysse, Pandémonium) : on perd d'abord la dernière ville de l'Abysse,
    /// puis celle du Pandémonium, en rejouant l'ordre exact de
    /// MainGameController.OnCityDestroyedHandler (Faille des Abysses d'abord, Portail du Pandémonium
    /// ensuite).
    ///
    /// <para>Le point délicat est que le Portail du Pandémonium siège sur un hex de l'Abysse (celui
    /// de la Tentacule abattue) : la perte de l'Abysse vide cette couche entière, portail compris,
    /// alors que le Pandémonium et ses villes, eux, survivent. La perte du Pandémonium qui suit doit
    /// donc se passer sans portail à remettre à 50 %.</para>
    /// </summary>
    public class DeepLayerLossChainTests
    {
        private static HexCoord UnderworldHex => new(0, 0, LayerState.UnderworldZ);
        private static HexCoord Abyss1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord Abyss2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord Abyss3 => new(0, 1, LayerState.AbyssZ);

        private sealed record Setup(
            WorldState State,
            GodState GodState,
            AbyssGateController AbyssController,
            PandemoniumGateController PandemoniumController,
            AbyssGate AbyssGate,
            PandemoniumGate PandemoniumGate);

        /// <summary>
        /// Monte les quatre couches d'un coup : ville de surface (IslandTestFactory), avant-poste
        /// d'Inframonde portant la Faille des Abysses bâtie, avant-poste d'Abysse portant le Portail
        /// du Pandémonium bâti, et arène du Pandémonium générée pour de vrai (dieu démon, Tentacules
        /// et avant-poste d'arrivée, voir PandemoniumGenerator).
        /// </summary>
        private static Setup CreateFourLayerSetup()
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var civ = state.PlayerCivilization;
            var prng = new GamePRNG(1);

            // --- Inframonde : porte la Faille des Abysses, bâtie ---
            state.AddLayer(LayerState.UnderworldZ, new LayerState(new IslandMap(
                new[]
                {
                    new HexTile(UnderworldHex, TerrainType.Mountain),
                    new HexTile(UnderworldHex.Neighbor(HexDirection.E), TerrainType.Hill),
                    new HexTile(UnderworldHex.Neighbor(HexDirection.NE), TerrainType.Forest),
                }, LayerState.UnderworldZ)));
            civ.AddCity(new City(Vertex.Create(
                UnderworldHex, UnderworldHex.Neighbor(HexDirection.E), UnderworldHex.Neighbor(HexDirection.NE)))
            { CivilizationIndex = civ.Index });
            var abyssGate = new AbyssGate(UnderworldHex) { Built = true, WasEverBuilt = true };
            state.AddFeature(abyssGate);

            // --- Abysse : porte le Portail du Pandémonium, bâti ---
            var abyssVertex = Vertex.Create(Abyss1, Abyss2, Abyss3);
            state.AddLayer(LayerState.AbyssZ, new LayerState(new IslandMap(
                new[]
                {
                    new HexTile(Abyss1, TerrainType.Mountain),
                    new HexTile(Abyss2, TerrainType.Mountain),
                    new HexTile(Abyss3, TerrainType.Hill),
                }, LayerState.AbyssZ))
            { ArrivalVertex = abyssVertex });
            civ.AddCity(new City(abyssVertex) { CivilizationIndex = civ.Index });
            civ.AddRoad(new Road(Edge.Create(Abyss1, Abyss2)) { CivilizationIndex = civ.Index });
            // Os Divins : feature de l'Abysse qui doit disparaître avec la couche.
            state.AddFeature(new DivineBones(Abyss3, 1));
            var pandemoniumGate = new PandemoniumGate(Abyss2) { Built = true, WasEverBuilt = true };
            state.AddFeature(pandemoniumGate);

            // --- Pandémonium : arène réelle, avec son avant-poste d'arrivée ---
            var layout = PandemoniumGenerator.Create(civ, prng);
            state.AddLayer(LayerState.PandemoniumZ, layout.Layer);
            foreach (var monster in layout.Monsters)
                state.AddFeature(monster);
            civ.AddRoad(new Road(Edge.Create(
                new HexCoord(0, 0, LayerState.PandemoniumZ), new HexCoord(1, 0, LayerState.PandemoniumZ)))
            { CivilizationIndex = civ.Index });

            var godState = new GodState { DivineEssence = 7 };
            var clock = new GameClock();
            clock.Start();

            var abyssController = new AbyssGateController();
            abyssController.Initialize(state, clock, godState: godState);
            var pandemoniumController = new PandemoniumGateController();
            pandemoniumController.Initialize(state, clock, prng: prng);

            return new Setup(state, godState, abyssController, pandemoniumController, abyssGate, pandemoniumGate);
        }

        /// <summary>Détruit la ville et notifie les deux contrôleurs dans l'ordre de MainGameController.</summary>
        private static void DestroyCity(Setup setup, City city)
        {
            setup.State.PlayerCivilization.RemoveCity(city);
            setup.AbyssController.OnCityDestroyed(city.Position, setup.State.PlayerCivilization.Index);
            setup.PandemoniumController.OnCityDestroyed(city.Position, setup.State.PlayerCivilization.Index);
        }

        private static City CityOn(WorldState state, int z)
            => state.PlayerCivilization.Cities.Single(c => c.Position.Z == z);

        [Fact]
        public void FourLayerSetup_IsComplete()
        {
            var setup = CreateFourLayerSetup();
            var state = setup.State;

            foreach (int z in new[] { IslandMap.SurfaceLayer, LayerState.UnderworldZ, LayerState.AbyssZ, LayerState.PandemoniumZ })
            {
                Assert.NotEmpty(state.GetMapForZ(z)!.Tiles);
                Assert.NotNull(CityOn(state, z));
            }

            Assert.True(setup.AbyssGate.Built);
            Assert.True(setup.PandemoniumGate.Built);
            Assert.Single(state.Features.OfType<DemonGod>());
        }

        /// <summary>
        /// Perte de l'Abysse : la couche est vidée (Os Divins et Portail du Pandémonium compris,
        /// puisqu'il y siège), la Faille retombe à 50 %, les essences divines du run sont perdues —
        /// et le Pandémonium, lui, n'est pas touché.
        /// </summary>
        [Fact]
        public void LosingAbyssFirst_ClearsAbyssOnly_AndLeavesPandemoniumIntact()
        {
            var setup = CreateFourLayerSetup();
            var state = setup.State;

            DestroyCity(setup, CityOn(state, LayerState.AbyssZ));

            // Couche de l'Abysse vidée
            Assert.Empty(state.GetMapForZ(LayerState.AbyssZ)!.Tiles);
            Assert.Empty(state.Features.Where(f => f.Position.Z == LayerState.AbyssZ));
            Assert.Empty(state.PlayerCivilization.Roads.Where(r => r.Position.Z == LayerState.AbyssZ));

            // Faille retombée à 50 %, sans investissement automatique relancé
            Assert.False(setup.AbyssGate.Built);
            Assert.True(setup.AbyssGate.WasEverBuilt);
            Assert.Empty(setup.AbyssGate.InvestmentEnabled);
            foreach (var kvp in setup.AbyssGate.GetInvestmentCost(state.PlayerCivilization))
                Assert.Equal(kvp.Value / 2, setup.AbyssGate.InvestedResources[kvp.Key]);

            Assert.Equal(0, setup.GodState.DivineEssence);
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.AbyssGateLost);

            // Le Portail du Pandémonium siégeait dans l'Abysse : il part avec la couche...
            Assert.Empty(state.Features.OfType<PandemoniumGate>());
            // ...mais le Pandémonium lui-même survit, ville et arène comprises.
            Assert.NotEmpty(state.GetMapForZ(LayerState.PandemoniumZ)!.Tiles);
            Assert.NotNull(CityOn(state, LayerState.PandemoniumZ));
            Assert.Single(state.Features.OfType<DemonGod>());
            Assert.DoesNotContain(state.EventLog.Entries, e => e.Type == GameEventType.PandemoniumGateLost);

            // Surface et Inframonde intacts
            Assert.NotEmpty(state.GetMapForZ(LayerState.UnderworldZ)!.Tiles);
            Assert.NotNull(CityOn(state, LayerState.UnderworldZ));
            Assert.NotNull(CityOn(state, IslandMap.SurfaceLayer));
        }

        /// <summary>
        /// Perte du Pandémonium juste après celle de l'Abysse : l'arène est vidée proprement bien
        /// qu'il n'y ait plus aucun portail à remettre à 50 % (il a disparu avec l'Abysse).
        /// </summary>
        [Fact]
        public void LosingAbyssThenPandemonium_ClearsBothWithoutAGateToReset()
        {
            var setup = CreateFourLayerSetup();
            var state = setup.State;

            DestroyCity(setup, CityOn(state, LayerState.AbyssZ));
            DestroyCity(setup, CityOn(state, LayerState.PandemoniumZ));

            Assert.Empty(state.GetMapForZ(LayerState.PandemoniumZ)!.Tiles);
            Assert.Empty(state.Features.Where(f => f.Position.Z == LayerState.PandemoniumZ));
            Assert.Empty(state.PlayerCivilization.Roads.Where(r => r.Position.Z == LayerState.PandemoniumZ));
            Assert.Empty(state.Features.OfType<DemonGod>());
            Assert.Contains(state.EventLog.Entries, e => e.Type == GameEventType.PandemoniumGateLost);

            // Les deux couches profondes sont vides, les deux autres intactes.
            Assert.Empty(state.GetMapForZ(LayerState.AbyssZ)!.Tiles);
            Assert.NotEmpty(state.GetMapForZ(LayerState.UnderworldZ)!.Tiles);
            Assert.NotEmpty(state.GetMapForZ(IslandMap.SurfaceLayer)!.Tiles);
            Assert.Equal(
                new[] { IslandMap.SurfaceLayer, LayerState.UnderworldZ },
                state.PlayerCivilization.Cities.Select(c => c.Position.Z).OrderBy(z => z));
        }

        /// <summary>
        /// Ordre inverse — le Pandémonium tombe d'abord : son portail, toujours là, retombe bien à
        /// 50 %, puis la perte de l'Abysse l'emporte avec le reste de la couche.
        /// </summary>
        [Fact]
        public void LosingPandemoniumThenAbyss_ResetsGateThenRemovesItWithTheLayer()
        {
            var setup = CreateFourLayerSetup();
            var state = setup.State;

            DestroyCity(setup, CityOn(state, LayerState.PandemoniumZ));

            Assert.False(setup.PandemoniumGate.Built);
            Assert.True(setup.PandemoniumGate.WasEverBuilt);
            Assert.Empty(setup.PandemoniumGate.InvestmentEnabled);
            foreach (var kvp in setup.PandemoniumGate.GetInvestmentCost(state.PlayerCivilization))
                Assert.Equal(kvp.Value / 2, setup.PandemoniumGate.InvestedResources[kvp.Key]);
            // L'Abysse n'a pas bougé : sa Faille reste bâtie, sa ville et sa carte aussi.
            Assert.True(setup.AbyssGate.Built);
            Assert.NotEmpty(state.GetMapForZ(LayerState.AbyssZ)!.Tiles);

            DestroyCity(setup, CityOn(state, LayerState.AbyssZ));

            Assert.False(setup.AbyssGate.Built);
            Assert.Empty(state.Features.OfType<PandemoniumGate>());
            Assert.Empty(state.GetMapForZ(LayerState.AbyssZ)!.Tiles);
            Assert.Empty(state.GetMapForZ(LayerState.PandemoniumZ)!.Tiles);
        }

        /// <summary>
        /// Le Pandémonium ne se rouvre pas tout seul après la perte : il faut un portail bâti, et
        /// celui-ci a disparu avec l'Abysse. Rejoue plusieurs cycles d'horloge pour s'en assurer.
        /// </summary>
        [Fact]
        public void AfterBothLosses_PandemoniumDoesNotReopenOnItsOwn()
        {
            var setup = CreateFourLayerSetup();
            var state = setup.State;
            var clock = new GameClock();
            clock.Start();
            setup.PandemoniumController.Initialize(state, clock, prng: new GamePRNG(1));

            DestroyCity(setup, CityOn(state, LayerState.AbyssZ));
            DestroyCity(setup, CityOn(state, LayerState.PandemoniumZ));

            clock.SimulateAdvance(PandemoniumGateController.InvestmentIntervalTicks * 5);

            Assert.Empty(state.GetMapForZ(LayerState.PandemoniumZ)!.Tiles);
            Assert.Empty(state.Features.OfType<MonsterFeature>()
                .Where(m => m.Position.Z == LayerState.PandemoniumZ));
            Assert.DoesNotContain(state.PlayerCivilization.Cities, c => c.Position.Z == LayerState.PandemoniumZ);
        }
    }
}
