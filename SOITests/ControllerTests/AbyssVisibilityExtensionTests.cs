using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Génération dynamique d'îles de l'Abysse : dès qu'un hex de Void devient visible pour une
    /// civilisation (par ex. via une Tour de Guet qui étend le rayon de vision), AutoExtendController
    /// doit faire pousser une nouvelle île au-delà de ce Void, sans jamais affecter les autres
    /// couches (Surface, Inframonde) ni s'activer tant qu'aucun layer Abysse n'existe.
    /// </summary>
    public class AbyssVisibilityExtensionTests
    {
        private static HexCoord Arrival1 => new(0, 0, LayerState.AbyssZ);
        private static HexCoord Arrival2 => new(1, 0, LayerState.AbyssZ);
        private static HexCoord Arrival3 => new(0, 1, LayerState.AbyssZ);

        private static readonly HashSet<HexCoord> ArrivalSet = new() { Arrival1, Arrival2, Arrival3 };

        private static (WorldState state, City city, HexCoord voidHex) CreateAbyssSetup(GamePRNG prng)
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
            var layer = new LayerState(new IslandMap(tiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex };
            state.AddLayer(LayerState.AbyssZ, layer);

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, prng);

            return (state, city, voidHex);
        }

        [Fact]
        public void OnHexesRevealed_GeneratesNewIsland_WhenVoidHexBecomesVisible()
        {
            var (state, city, voidHex) = CreateAbyssSetup(new GamePRNG(1));
            var map = state.Layers[LayerState.AbyssZ].Map;

            // Le hex de Void n'est pas encore visible (rayon 1, il ne touche pas le vertex d'arrivée).
            Assert.False(state.Visibility.GetForZ(LayerState.AbyssZ)[city.CivilizationIndex].HasTile(voidHex));

            // Une Tour de Guet niveau 1 porte le rayon de vision à 2, ce qui révèle le hex de Void.
            city.Buildings.Add(new Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(city.CivilizationIndex);

            Assert.True(state.Visibility.GetForZ(LayerState.AbyssZ)[city.CivilizationIndex].HasTile(voidHex));

            var beyondVoid = voidHex.Neighbors().Where(n => !ArrivalSet.Contains(n));
            Assert.Contains(beyondVoid, n => map.HasTile(n) && map.GetTile(n)!.TerrainType != TerrainType.Void);

            // Chaque île générée doit avoir sa propre civilisation NPC (une seule ici).
            var npcCiv = Assert.Single(state.Civilizations.Where(c => c.IsNpc));
            Assert.Single(npcCiv.Cities);
        }

        [Fact]
        public void OnHexesRevealed_DoesNotFire_ForUnderworldZ()
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var u1 = new HexCoord(0, 0, LayerState.UnderworldZ);
            var u2 = new HexCoord(1, 0, LayerState.UnderworldZ);
            var u3 = new HexCoord(0, 1, LayerState.UnderworldZ);
            var arrivalVertex = Vertex.Create(u1, u2, u3);
            var underworldTiles = new List<HexTile>
            {
                new(u1, TerrainType.Mountain),
                new(u2, TerrainType.Mountain),
                new(u3, TerrainType.Mountain),
            };
            var layer = new LayerState(new IslandMap(underworldTiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex };
            state.AddLayer(LayerState.UnderworldZ, layer);

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(1));

            city.Buildings.Add(new Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(civ.Index);

            // Aucun hex de Void n'existe sur cette couche : le mécanisme Abysse ne doit rien générer.
            var underworldMap = state.Layers[LayerState.UnderworldZ].Map;
            Assert.DoesNotContain(underworldMap.Tiles.Values, t => t.TerrainType == TerrainType.Void);
        }

        [Fact]
        public void OnHexesRevealed_NoOp_WhenAbyssLayerAbsent()
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(1));

            var exception = Record.Exception(() => state.Visibility.RecalculateFor(civ.Index));

            Assert.Null(exception);
            Assert.False(state.Layers.ContainsKey(LayerState.AbyssZ));
        }
    }
}
