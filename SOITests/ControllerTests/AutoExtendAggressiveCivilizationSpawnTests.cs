using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Apparition des civilisations NPC agressives de l'Inframonde (<see cref="AutoExtendController.TryExtendMapAfterRoad"/>
    /// → TrySpawnAggressiveCivilization), déclenchée par la construction de routes du joueur qui
    /// révèlent de nouveaux hexagones à distance suffisante du point d'arrivée. L'Abysse ne génère
    /// plus jamais de civilisation NPC (territoire exclusivement joueur — voir
    /// AutoExtendController.OnHexesRevealed/TrySpawnAggressiveCivilization) : seul l'Inframonde est
    /// concerné par ce mécanisme. Le cap de civilisations (MaxTotalCivilizations) reste néanmoins
    /// compté séparément par couche plutôt que sur <c>WorldState.Civilizations</c> dans son ensemble
    /// (voir AutoExtendController.CountCivilizationsOnLayer), pour ne jamais laisser les civs de
    /// surface épuiser à elles seules le budget de l'Inframonde.
    /// </summary>
    public class AutoExtendAggressiveCivilizationSpawnTests
    {
        /// <summary>
        /// PrestigeState d'une île située au-delà de l'anneau sûr des premières îles
        /// (AutoExtendController.UnderworldSafeRadiusBonusByIsland : +8 / +6 / +4 / +2 hexagones de
        /// distance minimale au point d'arrivée sur les quatre premières). Sans lui, ces tests
        /// mesureraient cet anneau : à portée des trente routes explorées ici, aucune civilisation ne
        /// peut apparaître sur une première île. Le numéro d'île se lit sur le nombre de prestiges
        /// déjà enregistrés.
        /// </summary>
        private static PrestigeState LateIslandPrestigeState()
        {
            var prestigeState = new PrestigeState();
            for (int i = 0; i < 4; i++)
                prestigeState.RunHistory.Add(new PrestigeRunStats());
            return prestigeState;
        }

        private static (WorldState state, Civilization civ, LayerState layer, AutoExtendController controller) CreateUnderworldSetup(GamePRNG prng)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ);
            state.AddLayer(LayerState.UnderworldZ, underworldLayer);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, prng, prestigeState: LateIslandPrestigeState());

            return (state, civ, underworldLayer, controller);
        }

        /// <summary>
        /// Construit une route sur l'arête donnée en reproduisant les effets de bord de
        /// <see cref="Controller.Island.RoadController.BuildRoad"/> pertinents ici (enregistrement de
        /// la route, rafraîchissement de la visibilité) avant de déclencher
        /// <see cref="AutoExtendController.TryExtendMapAfterRoad"/>, sans les vérifications de
        /// coût/prérequis de RoadController qui ne sont pas pertinentes pour ce test.
        /// </summary>
        private static void BuildRoad(WorldState state, Civilization civ, AutoExtendController controller, Edge edge)
        {
            civ.AddRoad(new Road(edge) { CivilizationIndex = civ.Index });
            state.Visibility.RecalculateFor(civ.Index);
            controller.TryExtendMapAfterRoad(civ.Index, edge);
        }

        /// <summary>Étend la carte en construisant des routes sur des arêtes entre hexagones déjà connus, jusqu'à <paramref name="maxSteps"/> fois ou jusqu'à ce qu'il n'y ait plus d'arête disponible.</summary>
        private static void ExploreOutward(WorldState state, Civilization civ, LayerState layer, AutoExtendController controller, int maxSteps)
        {
            for (int step = 0; step < maxSteps; step++)
            {
                var edge = layer.Map.Tiles.Keys
                    .SelectMany(h => h.Neighbors().Where(layer.Map.HasTile).Select(n => Edge.Create(h, n)))
                    .FirstOrDefault(e => !civ.Roads.Any(r => r.Position.Equals(e)));
                if (edge == null) break;
                BuildRoad(state, civ, controller, edge);
            }
        }

        [Fact]
        public void AggressiveCivilization_CanSpawn_WhenExploringUnderworldFarFromArrival()
        {
            // Cherche la plus petite graine pour laquelle explorer l'Inframonde vers l'extérieur
            // (jusqu'à 30 routes) fait apparaître une civilisation NPC agressive.
            for (int seed = 0; seed < 300; seed++)
            {
                var (state, civ, layer, controller) = CreateUnderworldSetup(new GamePRNG(seed));
                ExploreOutward(state, civ, layer, controller, maxSteps: 30);

                if (state.Civilizations.Any(c => c.IsNpc))
                {
                    var npc = state.Civilizations.First(c => c.IsNpc);
                    Assert.NotEmpty(npc.Cities);
                    Assert.All(npc.Cities, c => Assert.Equal(LayerState.UnderworldZ, c.Position.Z));
                    return;
                }
            }

            Assert.Fail("Aucune civilisation agressive de l'Inframonde n'est apparue sur 300 graines testées (30 routes chacune) : le mécanisme de spawn semble cassé.");
        }

        [Fact]
        public void ExploringAbyss_NeverSpawnsNpcCivilization_EvenFarFromArrival()
        {
            var prng = new GamePRNG(1);
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var abyssLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ, LayerState.AbyssZ, surroundWithVoid: true);
            state.AddLayer(LayerState.AbyssZ, abyssLayer);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, prng, prestigeState: LateIslandPrestigeState());

            // L'Abysse est un territoire exclusivement joueur : ni la révélation de Void (nouvelles
            // îles) ni la construction de routes ne doivent jamais y faire apparaître de civilisation
            // NPC (voir AutoExtendController.OnHexesRevealed/TrySpawnAggressiveCivilization).
            ExploreOutward(state, civ, abyssLayer, controller, maxSteps: 30);

            Assert.Empty(state.Civilizations.Where(c => c.IsNpc));
        }
    }
}
