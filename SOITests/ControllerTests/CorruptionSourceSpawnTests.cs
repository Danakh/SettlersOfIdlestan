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
    /// Apparition des Sources de Corruption (voir <see cref="CorruptionSource"/>) : tirage plat de 10%
    /// sur chaque nouvel hex de l'Inframonde révélé par AutoExtendController.TrySpawnUnderworldDenizen,
    /// indépendant du tirage de Corruption (l'hex peut en porter une sans être corrompu) et du niveau
    /// de corruption de l'île ; seule contrainte, une distance au point d'arrivée d'au moins 3.
    ///
    /// Dispositif repris d'AutoExtendAggressiveCivilizationSpawnTests : un avant-poste de l'Inframonde
    /// que l'on étend en construisant des routes vers l'extérieur, ce qui génère de nouveaux hexes
    /// (donc de nouvelles chances de Corruption/Source) à chaque route. La carte de départ se réduit
    /// aux 3 hexagones du vertex d'arrivée (voir LayerState.EstablishOupostInNewAutoExpandLayer), donc
    /// tout hex à distance ≥ 3 présent à la fin a bel et bien été soumis au tirage.
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

        /// <summary>Distance d'un hex au vertex d'arrivée : le minimum sur ses 3 hexagones.</summary>
        private static int DistanceToArrival(LayerState layer, HexCoord hex) =>
            layer.ArrivalVertex!.GetHexes().Where(h => hex.HasSameZ(h)).Min(hex.DistanceTo);

        [Fact]
        public void CorruptionSource_SpawnsOnOneTenthOfNewHexes_RegardlessOfCorruption()
        {
            int eligibleHexCount = 0;
            int sourceCount = 0;
            int sourcesOnCleanHex = 0;

            for (int seed = 0; seed < 30; seed++)
            {
                var (state, civ, layer, controller) = CreateUnderworldSetup(new GamePRNG(seed));
                ExploreOutward(state, civ, layer, controller, maxSteps: 400);

                var corruptions = state.Features.OfType<Corruption>().ToList();
                var sources = state.Features.OfType<CorruptionSource>().ToList();
                eligibleHexCount += layer.Map.Tiles.Keys.Count(h => DistanceToArrival(layer, h) >= 3);
                sourceCount += sources.Count;

                foreach (var source in sources)
                {
                    // Sans PrestigeState, le niveau de corruption de l'île vaut 1 : le plafond de
                    // production de chaque Source est figé à ce même niveau.
                    Assert.Equal(1, source.CorruptionLevel);
                    Assert.Equal(1, source.GetCorruptionCap());

                    // L'anneau sûr autour de la porte d'arrivée.
                    Assert.True(DistanceToArrival(layer, source.Position) >= 3,
                        $"Source de Corruption posée à distance {DistanceToArrival(layer, source.Position)} du point d'arrivée.");

                    if (!corruptions.Any(c => c.Position.Equals(source.Position)))
                        sourcesOnCleanHex++;
                }
            }

            Assert.True(eligibleHexCount > 0, "Aucun hex à distance ≥ 3 révélé sur 30 graines — dispositif de test invalide.");
            Assert.True(sourceCount > 0, "Aucune Source de Corruption sur 30 graines — le tirage ne semble jamais se déclencher.");

            // Le tirage est indépendant de celui de la Corruption : des Sources doivent apparaître sur
            // des hexagones sains (c'est CorruptionController qui y sèmera ensuite la Corruption).
            Assert.True(sourcesOnCleanHex > 0,
                "Toutes les Sources sont sur un hex déjà corrompu — le tirage semble encore adossé à celui de la Corruption.");

            // 10% des hexes éligibles, marge large pour éviter tout flakiness.
            Assert.InRange(sourceCount, eligibleHexCount * 5 / 100, eligibleHexCount * 16 / 100);
        }
    }
}
