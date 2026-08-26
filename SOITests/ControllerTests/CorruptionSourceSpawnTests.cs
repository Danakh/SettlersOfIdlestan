using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Apparition des Sources de Corruption (voir <see cref="CorruptionSource"/>) : quand la
    /// Corruption semée par AutoExtendController.TrySpawnUnderworldDenizen sur un nouvel hex de
    /// l'Inframonde atteint le niveau de corruption maximal de l'île (garanti ici, PrestigeState
    /// absent ⇒ niveau 1, donc Corruption.RollLevel(prng, 1) retourne toujours 1 = le plafond), 50%
    /// de chance de plus de poser également une Source sur ce même hex.
    ///
    /// Dispositif repris d'AutoExtendAggressiveCivilizationSpawnTests : un avant-poste de l'Inframonde
    /// que l'on étend en construisant des routes vers l'extérieur, ce qui génère de nouveaux hexes
    /// (donc de nouvelles chances de Corruption/Source) à chaque route.
    /// </summary>
    public class CorruptionSourceSpawnTests
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

        private static void BuildRoad(WorldState state, Civilization civ, AutoExtendController controller, Edge edge)
        {
            civ.AddRoad(new Road(edge) { CivilizationIndex = civ.Index });
            state.Visibility.RecalculateFor(civ.Index);
            controller.TryExtendMapAfterRoad(civ.Index, edge);
        }

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
        public void CorruptionSource_SpawnsOnlyOnHexesAtTheIslandCorruptionLevel()
        {
            int corruptionCount = 0;
            int sourceCount = 0;

            for (int seed = 0; seed < 30; seed++)
            {
                var (state, civ, layer, controller) = CreateUnderworldSetup(new GamePRNG(seed));
                ExploreOutward(state, civ, layer, controller, maxSteps: 400);

                var corruptions = state.Features.OfType<Corruption>().ToList();
                var sources = state.Features.OfType<CorruptionSource>().ToList();
                corruptionCount += corruptions.Count;
                sourceCount += sources.Count;

                foreach (var source in sources)
                {
                    // Sans PrestigeState, le niveau de corruption de l'île vaut 1 : toute Corruption
                    // semée est donc déjà à son plafond, et chaque Source doit avoir son propre
                    // plafond figé à ce même niveau.
                    Assert.Equal(1, source.CorruptionLevel);
                    Assert.Equal(1, source.GetCorruptionCap());
                    Assert.Contains(corruptions, c => c.Position.Equals(source.Position));
                }
            }

            Assert.True(corruptionCount > 0, "Aucune Corruption semée sur 30 graines — dispositif de test invalide.");
            Assert.True(sourceCount > 0, "Aucune Source de Corruption sur 30 graines — la chance de 50% ne semble jamais se déclencher.");
            Assert.True(sourceCount < corruptionCount, "Une Source de Corruption a été posée à chaque Corruption semée — la chance de 50% ne semble jamais échouer.");

            // Chance = 50% par Corruption semée : marge large pour éviter tout flakiness.
            Assert.InRange(sourceCount, corruptionCount / 4, 3 * corruptionCount / 4);
        }
    }
}
