using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Prestige;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>
    /// Rééquilibrage des civilisations NPC de l'Inframonde et de l'Abysse : la base économique/tech
    /// est désormais celle d'une civilisation de surface de Tier+1 (Inframonde) ou Tier+2 (Abysse),
    /// en plus des bonus fixes de couche (inchangés pour l'Inframonde, doublés pour l'Abysse).
    /// </summary>
    public class AggressiveCivilizationRebalanceTests
    {
        private static (ECategory Category, string SubCategory, EType Type, double Value)[] Snapshot(IEnumerable<Modifier> modifiers) =>
            modifiers.Select(m => (m.Category, m.SubCategory, m.Type, m.Value)).ToArray();

        [Fact]
        public void BuildAggressiveModifiers_ScalesAllFourConstantsByMultiplier()
        {
            var baseline = AutoExtendController.BuildAggressiveModifiers(1);
            var doubled = AutoExtendController.BuildAggressiveModifiers(2);

            Assert.Equal(4, baseline.Count);
            Assert.Equal(4, doubled.Count);
            for (int i = 0; i < baseline.Count; i++)
            {
                Assert.Equal(baseline[i].Category, doubled[i].Category);
                Assert.Equal(baseline[i].SubCategory, doubled[i].SubCategory);
                Assert.Equal(baseline[i].Type, doubled[i].Type);
                Assert.Equal(baseline[i].Value * 2, doubled[i].Value);
            }

            var soldiersBonus = baseline.Single(m => m.Category == ECategory.CITY_MAX_SOLDIERS_BONUS);
            Assert.Equal(85, soldiersBonus.Value);
        }

        [Fact]
        public void BuildLayerCivModifiers_AppendsExactFixedBonusTail_OnTopOfTierBaseline()
        {
            var state = new WorldState(
                new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) }),
                new List<Civilization> { new() { Index = 0 } },
                AtlasController.InvalidIslandId);
            var prestigeState = new PrestigeState(); // TotalPrestigePointsEarned = 0 -> Tier == 1

            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(1), prestigeState: prestigeState);

            var underworldModifiers = controller.BuildLayerCivModifiers(tierOffset: 1, fixedBonusMultiplier: 1);
            var abyssModifiers = controller.BuildLayerCivModifiers(tierOffset: 2, fixedBonusMultiplier: 2);

            // Tier == 1 -> Underworld baseline = Tier 2 surface civ, Abyss baseline = Tier 3 surface civ.
            var expectedUnderworldBaseline = Snapshot(NpcModifierSetMaker.Create(maxTechTier: 3, maxPrestigeDistance: 2).GetModifiers());
            var expectedAbyssBaseline = Snapshot(NpcModifierSetMaker.Create(maxTechTier: 4, maxPrestigeDistance: 3).GetModifiers());

            Assert.Equal(expectedUnderworldBaseline, Snapshot(underworldModifiers.Take(underworldModifiers.Count - 4)));
            Assert.Equal(expectedAbyssBaseline, Snapshot(abyssModifiers.Take(abyssModifiers.Count - 4)));

            // Tail = fixed depths bonus, x1 for Underworld and x2 for Abyss.
            Assert.Equal(Snapshot(AutoExtendController.BuildAggressiveModifiers(1)), Snapshot(underworldModifiers.TakeLast(4)));
            Assert.Equal(Snapshot(AutoExtendController.BuildAggressiveModifiers(2)), Snapshot(abyssModifiers.TakeLast(4)));
        }

        [Fact]
        public void OnHexesRevealed_NeverSpawnsNpcCivilization_WhenNewAbyssIslandIsGenerated()
        {
            var arrival1 = new HexCoord(0, 0, LayerState.AbyssZ);
            var arrival2 = new HexCoord(1, 0, LayerState.AbyssZ);
            var arrival3 = new HexCoord(0, 1, LayerState.AbyssZ);
            var arrivalSet = new HashSet<HexCoord> { arrival1, arrival2, arrival3 };
            var voidHex = arrival2.Neighbors().First(n => !arrivalSet.Contains(n));

            var surfaceMap = new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) });
            var civ = new Civilization { Index = 0 };
            var state = new WorldState(surfaceMap, new List<Civilization> { civ }, AtlasController.InvalidIslandId);

            var tiles = new List<HexTile>
            {
                new(arrival1, TerrainType.Mountain),
                new(arrival2, TerrainType.Mountain),
                new(arrival3, TerrainType.Mountain),
                new(voidHex, TerrainType.Void),
            };
            var arrivalVertex = Vertex.Create(arrival1, arrival2, arrival3);
            var layer = new LayerState(new IslandMap(tiles)) { AutoExtend = true, ArrivalVertex = arrivalVertex };
            state.AddLayer(LayerState.AbyssZ, layer);

            var city = new City(arrivalVertex) { CivilizationIndex = civ.Index };
            civ.AddCity(city);
            state.Visibility.RecalculateFor(civ.Index);

            var prestigeState = new PrestigeState(); // Tier == 1
            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(1), prestigeState: prestigeState);

            // Révèle le hex de Void voisin de l'avant-poste : une nouvelle île de l'Abysse est
            // générée au-delà, mais l'Abysse est un territoire exclusivement joueur — aucune
            // civilisation NPC ne doit y apparaître (voir OnHexesRevealed).
            city.AddBuilding(new SettlersOfIdlestan.Model.Buildings.Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(civ.Index);

            Assert.Empty(state.Civilizations.Where(c => c.IsNpc));
        }

        // ── Bâtiments verrouillés des villes PNJ de couche ────────────────────

        /// <summary>Les trois hexs du vertex de la ville, avec des terrains variés pour qu'un maximum
        /// de bâtiments passe le filtre de terrain de PopulateAggressiveCity.</summary>
        private static (IslandMap Map, Vertex Vertex) BuildCityVertexMap()
        {
            var h1 = new HexCoord(0, 0, IslandMap.SurfaceLayer);
            var h2 = new HexCoord(1, 0, IslandMap.SurfaceLayer);
            var h3 = new HexCoord(0, 1, IslandMap.SurfaceLayer);
            var map = new IslandMap(new[]
            {
                new HexTile(h1, TerrainType.Mountain),
                new HexTile(h2, TerrainType.Forest),
                new HexTile(h3, TerrainType.Plain),
            });
            return (map, Vertex.Create(h1, h2, h3));
        }

        private static City PopulateCityFor(Civilization civ)
        {
            var (map, vertex) = BuildCityVertexMap();
            var city = new City(vertex) { CivilizationIndex = civ.Index };
            AutoExtendController.PopulateAggressiveCity(city, map, civ);
            return city;
        }

        [Fact]
        public void PopulateAggressiveCity_NeverGrantsBuildingLockedForThatCivilization()
        {
            var state = new WorldState(
                new IslandMap(new[] { new HexTile(new HexCoord(0, 0, IslandMap.SurfaceLayer), TerrainType.Plain) }),
                new List<Civilization> { new() { Index = 0 } },
                AtlasController.InvalidIslandId);
            var controller = new AutoExtendController();
            controller.Initialize(state, new GamePRNG(1), prestigeState: new PrestigeState()); // Tier == 1

            var npcCiv = new Civilization { Index = 1, IsNpc = true };
            npcCiv.SetNpcModifiers(new StaticModifierProvider(
                controller.BuildLayerCivModifiers(tierOffset: 1, fixedBonusMultiplier: 1)));

            var city = PopulateCityFor(npcCiv);

            // Aucun bâtiment posé ne doit être hors de portée des recherches/prestiges de la civ.
            var locked = city.Buildings
                .Where(b => npcCiv.GetBuildingMaxLevel(b) <= 0)
                .Select(b => b.Type)
                .ToList();
            Assert.Empty(locked);

            // Cas concret à l'origine de la règle : la Spire de Défense n'est ouverte que par un vertex
            // de prestige de la branche magie, très au-delà du rayon d'un PNJ de Tier 1.
            Assert.Equal(0, npcCiv.GetBuildingMaxLevel(new DefenseSpire()));
            Assert.DoesNotContain(city.Buildings, b => b.Type == BuildingType.DefenseSpire);
        }

        [Fact]
        public void PopulateAggressiveCity_GrantsLockedBuilding_AtTheLevelItsOwnModifiersUnlock()
        {
            var npcCiv = new Civilization { Index = 1, IsNpc = true };
            npcCiv.SetNpcModifiers(new StaticModifierProvider(new List<Modifier>
            {
                new(ECategory.BUILDING_MAX_LEVEL, "DefenseSpire", EType.ADDITIVE, 2),
            }));

            var city = PopulateCityFor(npcCiv);

            var spire = city.Buildings.SingleOrDefault(b => b.Type == BuildingType.DefenseSpire);
            Assert.NotNull(spire);
            Assert.Equal(2, spire!.Level);
        }

        [Fact]
        public void PopulateAggressiveCity_KeepsDefaultMaxLevel_ForBuildingsUnlockedByDefault()
        {
            var npcCiv = new Civilization { Index = 1, IsNpc = true };
            npcCiv.SetNpcModifiers(new StaticModifierProvider(new List<Modifier>
            {
                // Un bonus de niveau max sur un bâtiment déjà ouvert par défaut ne doit pas gonfler la
                // ville PNJ : seuls les bâtiments verrouillés changent de traitement.
                new(ECategory.BUILDING_MAX_LEVEL, "Sawmill", EType.ADDITIVE, 5),
            }));

            var city = PopulateCityFor(npcCiv);

            var sawmill = city.Buildings.Single(b => b.Type == BuildingType.Sawmill);
            Assert.Equal(new Sawmill().GetDefaultMaxLevel(), sawmill.Level);
        }
    }
}
