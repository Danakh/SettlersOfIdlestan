using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SOITests.TestUtilities;
using System.Linq;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// AutoExtendController.EnsureRiverPlanned/IsRiverHex : planification de la rivière de
    /// l'Inframonde, y compris le cas où le triangle de départ porte déjà de l'Eau (race Sirène, voir
    /// RaceDefinition.UndergroundStartVertexTerrain, câblé par DeepestMineController.TryInitializeUnderworld)
    /// — la rivière doit alors démarrer littéralement sur cet hex plutôt que respecter la distance
    /// minimale habituelle de 3 hexes (MinRiverDistanceFromArrival, privée : reprise ici en dur).
    /// </summary>
    public class AutoExtendRiverPlanningTests
    {
        private const int Z = LayerState.UnderworldZ;
        private const int MinRiverDistanceFromArrival = 3;

        private static (LayerState layer, AutoExtendController controller) CreateLayerWithArrivalTerrains(
            TerrainType[] triangleTerrains, int seed)
        {
            var state = IslandTestFactory.CreateSevenHexIslandState();
            var h1 = new HexCoord(0, 0, Z);
            var h2 = new HexCoord(1, 0, Z);
            var h3 = new HexCoord(0, 1, Z);
            var tiles = new[]
            {
                new HexTile(h1, triangleTerrains[0]),
                new HexTile(h2, triangleTerrains[1]),
                new HexTile(h3, triangleTerrains[2]),
            };
            var layer = new LayerState
            {
                Map = new IslandMap(tiles, Z),
                AutoExtend = true,
                ArrivalVertex = Vertex.Create(h1, h2, h3),
            };
            state.AddLayer(Z, layer);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(seed));
            return (layer, controller);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void EnsureRiverPlanned_WithoutWaterInTriangle_KeepsRiverAwayFromArrival(int seed)
        {
            var (layer, controller) = CreateLayerWithArrivalTerrains(
                new[] { TerrainType.MushroomCave, TerrainType.Mountain, TerrainType.Hill }, seed);

            controller.EnsureRiverPlanned(layer);

            Assert.NotEmpty(layer.RiverCycleHexes);
            var arrivalHexes = layer.ArrivalVertex!.GetHexes();
            Assert.All(layer.RiverCycleHexes, h =>
                Assert.True(arrivalHexes.Min(a => a.DistanceTo(h)) >= MinRiverDistanceFromArrival));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(42)]
        public void EnsureRiverPlanned_WithWaterInTriangle_StartsRiverLiterallyOnThatHex(int seed)
        {
            var waterHex = new HexCoord(0, 1, Z);
            var (layer, controller) = CreateLayerWithArrivalTerrains(
                new[] { TerrainType.MushroomCave, TerrainType.Mountain, TerrainType.Water }, seed);

            controller.EnsureRiverPlanned(layer);

            Assert.NotEmpty(layer.RiverCycleHexes);
            Assert.Equal(waterHex, layer.RiverCycleHexes[0]);
            Assert.True(AutoExtendController.IsRiverHex(waterHex, layer));
        }
    }
}
