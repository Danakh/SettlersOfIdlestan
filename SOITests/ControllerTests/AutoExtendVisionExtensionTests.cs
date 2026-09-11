using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Extension des couches auto-étendues au rayon de vision des villes du joueur — voir
    /// AutoExtendController.TryExtendMapsToPlayerVision. Sans elle, le bonus de vision d'Oeil de Dieu
    /// serait lettre morte sous terre : la carte n'y existe que là où des routes l'ont fait pousser.
    /// Sous terre ce bonus civ-wide est la seule source de rayon, les Tours de Guet y étant interdites
    /// (voir Watchtower.IsAvailableInLayer) — c'est ce qui permet de n'appeler la passe qu'aux quelques
    /// sources connues plutôt qu'à chaque changement de bâtiment.
    /// </summary>
    public class AutoExtendVisionExtensionTests
    {
        private static HexCoord H1(int z) => new(0, 0, z);
        private static HexCoord H2(int z) => new(1, 0, z);
        private static HexCoord H3(int z) => new(0, 1, z);

        private static (WorldState state, Civilization civ, City city, AutoExtendController controller) CreateLayerSetup(
            int z, bool surroundWithVoid = false)
        {
            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var triangle = new HashSet<HexCoord> { H1(z), H2(z), H3(z) };
            var tiles = triangle.Select(h => new HexTile(h, TerrainType.Mountain)).ToList();
            if (surroundWithVoid)
            {
                var ring = new HashSet<HexCoord>();
                foreach (var hex in triangle)
                    foreach (var n in hex.Neighbors())
                        if (!triangle.Contains(n)) ring.Add(n);
                tiles.AddRange(ring.Select(h => new HexTile(h, TerrainType.Void)));
            }

            var arrivalVertex = Vertex.Create(H1(z), H2(z), H3(z));
            var layer = new LayerState(new IslandMap(tiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex };
            state.AddLayer(z, layer);

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.Visibility.RecalculateFor(civ.Index);

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(1));

            return (state, civ, city, controller);
        }

        private static void GrantVisionRangeBonus(Civilization civ, int bonus)
            => civ.AddCustomAggregator(new StaticModifierProvider(new[]
            {
                new Modifier(Modifier.ECategory.CITY_VISION_RANGE, Modifier.EType.ADDITIVE, bonus),
            }));

        [Fact]
        public void TryExtendMapsToPlayerVision_WithVisionRangeBonus_GrowsUnderworldToTheVisionRadius()
        {
            int z = LayerState.UnderworldZ;
            var (state, civ, _, controller) = CreateLayerSetup(z);
            var map = state.Layers[z].Map;

            var triangle = new HashSet<HexCoord> { H1(z), H2(z), H3(z) };
            var ring = triangle.SelectMany(h => h.Neighbors()).Where(n => !triangle.Contains(n)).Distinct().ToList();
            Assert.All(ring, hex => Assert.False(map.HasTile(hex)));

            // Oeil de Dieu : rayon 1 -> 2, l'anneau autour du triangle passe à portée de vue.
            GrantVisionRangeBonus(civ, 1);
            controller.TryExtendMapsToPlayerVision();

            Assert.All(ring, hex => Assert.True(map.HasTile(hex), $"Expected {hex} to be generated."));
            // Et le joueur le voit vraiment : la visibilité est recalculée dans la foulée.
            var visible = state.Visibility.GetForZ(z)[civ.Index];
            Assert.All(ring, hex => Assert.True(visible.HasTile(hex), $"Expected {hex} to be visible."));
        }

        [Fact]
        public void TryExtendMapsToPlayerVision_WithoutAnyVisionBonus_LeavesUnderworldUntouched()
        {
            int z = LayerState.UnderworldZ;
            var (state, _, _, controller) = CreateLayerSetup(z);
            var map = state.Layers[z].Map;

            controller.TryExtendMapsToPlayerVision();

            // Rayon 1 : les 3 hexagones du sommet, déjà posés — rien à générer.
            Assert.Equal(3, map.Tiles.Count);
        }

        [Fact]
        public void TryExtendMapsToPlayerVision_NeverRollsTerrainInTheAbyss()
        {
            int z = LayerState.AbyssZ;
            var (state, civ, _, controller) = CreateLayerSetup(z, surroundWithVoid: true);
            var map = state.Layers[z].Map;

            // L'anneau de Void a déjà pu faire pousser des îles au moment du RecalculateFor initial :
            // c'est le mécanisme propre à l'Abysse (OnHexesRevealed), pas cette passe-ci.
            GrantVisionRangeBonus(civ, 1);
            state.Visibility.RecalculateFor(civ.Index);
            int tileCountBeforePass = map.Tiles.Count;

            controller.TryExtendMapsToPlayerVision();

            Assert.Equal(tileCountBeforePass, map.Tiles.Count);
        }

        [Fact]
        public void PurchasingEyeOfGod_ExtendsTheUnderworldImmediately()
        {
            int z = LayerState.UnderworldZ;
            var (state, civ, _, controller) = CreateLayerSetup(z);
            var map = state.Layers[z].Map;

            var godState = new GodState { GodPoints = 100 };
            var ascension = new AscensionController();
            ascension.Initialize(state, null, new GamePRNG(1), new HarvestController(), godState,
                autoExtendController: controller);
            civ.AddCustomAggregator(ascension);

            Assert.True(ascension.PurchasePower(AscensionPowerId.Faith));
            Assert.True(ascension.PurchasePower(AscensionPowerId.MemoryOfGod));
            Assert.Equal(3, map.Tiles.Count);

            // Pas d'attente ni de battement d'horloge : l'achat étend la couche lui-même.
            Assert.True(ascension.PurchasePower(AscensionPowerId.EyeOfGod));

            Assert.True(map.Tiles.Count > 3);
        }

        [Fact]
        public void TryExtendMapsToPlayerVision_IsIdempotent()
        {
            int z = LayerState.UnderworldZ;
            var (state, civ, _, controller) = CreateLayerSetup(z);
            var map = state.Layers[z].Map;

            GrantVisionRangeBonus(civ, 1);
            controller.TryExtendMapsToPlayerVision();
            int tileCountAfterFirstPass = map.Tiles.Count;
            Assert.True(tileCountAfterFirstPass > 3);

            // Appelée depuis plusieurs sources (fondation de ville, achat de pouvoir, chargement) :
            // une passe qui n'a plus rien à générer ne doit rien changer.
            controller.TryExtendMapsToPlayerVision();

            Assert.Equal(tileCountAfterFirstPass, map.Tiles.Count);
        }
    }
}
