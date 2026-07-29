using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Apparition des civilisations NPC agressives de l'Inframonde (<see cref="AutoExtendController.TryExtendMapAfterRoad"/>
    /// → TrySpawnAggressiveCivilization), déclenchée par la construction de routes du joueur qui
    /// révèlent de nouveaux hexagones à distance suffisante du point d'arrivée.
    ///
    /// Régression couverte : le cap de civilisations (8) était compté sur
    /// <c>WorldState.Civilizations</c> dans son ensemble, partagé avec les civs de surface ET les
    /// civs de l'Abysse. L'Abysse génère une civilisation à chaque île révélée (déterministe),
    /// beaucoup plus vite que le tirage à 10% par hexagone de l'Inframonde, donc dès que l'Abysse
    /// était ouvert et un peu exploré, le budget global était épuisé et l'Inframonde ne pouvait plus
    /// jamais faire apparaître de nouvelle civilisation pour le reste de la partie. Le cap est
    /// désormais compté séparément par couche (voir AutoExtendController.CountCivilizationsOnLayer).
    /// </summary>
    public class AutoExtendAggressiveCivilizationSpawnTests
    {
        private static (WorldState state, Civilization civ, LayerState layer, AutoExtendController controller) CreateUnderworldSetup(GamePRNG prng)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ);
            state.AddLayer(LayerState.UnderworldZ, underworldLayer);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, prng);

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
        public void AggressiveCivilization_CanStillSpawn_AfterAbyssExhaustsTheOldSharedCap()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var prng = new GamePRNG(seed);
                var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
                var civ = new Civilization { Index = 0 };
                var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

                // 5 civs NPC déjà placées en surface (comme AtlasController au démarrage d'une île,
                // 4-6 civs) : avec le joueur cela fait 6 civs, soit seulement 2 places restantes sur
                // l'ancien cap global de 8 (MaxTotalCivilizations), avant même d'ouvrir l'Inframonde
                // ou l'Abysse.
                for (int i = 1; i <= 5; i++)
                    state.Civilizations.Add(new Civilization { Index = i, IsNpc = true });

                var underworldLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ, LayerState.UnderworldZ);
                state.AddLayer(LayerState.UnderworldZ, underworldLayer);

                var abyssLayer = LayerState.EstablishOupostInNewAutoExpandLayer(civ, LayerState.AbyssZ, surroundWithVoid: true);
                state.AddLayer(LayerState.AbyssZ, abyssLayer);
                state.Visibility.RecalculateFor(civ.Index);

                var controller = new AutoExtendController();
                controller.Initialize(state, prng);

                // Révèle le Void autour de l'avant-poste de l'Abysse en construisant des routes :
                // chaque hex de Void révélé génère systématiquement une île + sa civ (voir
                // SpawnAbyssIslandCivilization), ce qui épuisait autrefois le budget global de 8.
                ExploreOutward(state, civ, abyssLayer, controller, maxSteps: 3);
                if (state.Civilizations.Count < 8) continue; // graine sans assez de void révélé, essaie la suivante

                ExploreOutward(state, civ, underworldLayer, controller, maxSteps: 30);

                bool underworldCivSpawned = state.Civilizations.Any(c =>
                    c.IsNpc && c.Cities.Any(city => city.Position.Z == LayerState.UnderworldZ));
                if (underworldCivSpawned)
                    return;
            }

            Assert.Fail("Aucune civilisation de l'Inframonde n'a pu apparaître après que l'Abysse a rempli l'ancien cap global de 8 : régression réintroduite.");
        }
    }
}
