using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Apparition des Démons mineurs sur les îles de l'Abysse générées dynamiquement : 50% de chance
    /// par île, indépendamment du niveau de corruption (contrairement aux Tentacules, voir
    /// AbyssTentacleSpawnTests). L'île d'arrivée du joueur ne passe jamais par ce chemin (elle est
    /// posée par AbyssGateController), ce qui l'exclut de fait du tirage.
    ///
    /// Dispositif repris d'AbyssTentacleSpawnTests : une Tour de Guet étend le rayon de vision,
    /// révèle l'hex de Void voisin et déclenche la génération de l'île au-delà.
    /// </summary>
    public class AbyssIslandMinorDemonSpawnTests
    {
        private static HexCoord Arrival1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord Arrival2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord Arrival3 => new(0, 1, LayerState.AbyssZ);

        private static readonly HashSet<HexCoord> ArrivalSet = new() { Arrival1, Arrival2, Arrival3 };

        /// <summary>Génère une île de l'Abysse avec le niveau de corruption donné et retourne l'état résultant.</summary>
        private static WorldState GenerateIsland(int corruptionLevel, int seed)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var voidHex = Arrival2.Neighbors().First(n => !ArrivalSet.Contains(n));
            var tiles = new List<HexTile>
            {
                new(Arrival1, TerrainType.Mountain),
                new(Arrival2, TerrainType.Mountain),
                new(Arrival3, TerrainType.Mountain),
                new(voidHex, TerrainType.Void),
            };
            var arrivalVertex = Vertex.Create(Arrival1, Arrival2, Arrival3);
            state.AddLayer(LayerState.AbyssZ, new LayerState(new IslandMap(tiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex });

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(seed), null, new PrestigeState { CurrentCorruptionLevel = corruptionLevel });

            city.AddBuilding(new Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(civ.Index);

            return state;
        }

        [Fact]
        public void SpawnsAtMostOneMinorDemonPerIsland_AcrossManySeeds()
        {
            int withDemon = 0;
            const int seedCount = 200;

            for (int seed = 0; seed < seedCount; seed++)
            {
                var state = GenerateIsland(corruptionLevel: 1, seed);
                var demons = state.Features.OfType<MinorDemon>().ToList();
                Assert.True(demons.Count <= 1, $"Attendu au plus un démon mineur, obtenu {demons.Count} (graine {seed})");

                if (demons.Count == 1)
                {
                    withDemon++;
                    var map = state.Layers[LayerState.AbyssZ].Map;
                    Assert.Equal(LayerState.AbyssZ, demons[0].Position.Z);
                    Assert.NotEqual(TerrainType.Void, map.GetTile(demons[0].Position)!.TerrainType);
                    Assert.DoesNotContain(demons[0].Position, ArrivalSet);
                }
            }

            // Chance = 50% : sur 200 graines, on attend une proportion proche de la moitié (marge large
            // pour éviter tout flakiness, l'objectif étant surtout d'exclure 0% et 100%).
            Assert.InRange(withDemon, seedCount / 4, 3 * seedCount / 4);
        }

        [Fact]
        public void MinorDemonChance_IsIndependentOfCorruptionLevel()
        {
            // Contrairement aux Tentacules (seuil de corruption), le Démon mineur d'île doit pouvoir
            // apparaître même au niveau de corruption minimal.
            bool foundDemon = false;
            for (int seed = 0; seed < 200 && !foundDemon; seed++)
            {
                var state = GenerateIsland(corruptionLevel: 1, seed);
                foundDemon = state.Features.OfType<MinorDemon>().Any();
            }

            Assert.True(foundDemon, "Un démon mineur devrait pouvoir apparaître même au niveau de corruption 1.");
        }
    }
}
