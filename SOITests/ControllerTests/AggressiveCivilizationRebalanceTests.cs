using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Island;
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
        public void SpawnAbyssIslandCivilization_UsesTierPlusTwoBaseline_AndDoubledFixedBonus()
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

            city.Buildings.Add(new SettlersOfIdlestan.Model.Buildings.Watchtower { Level = 1 });
            state.Visibility.RecalculateFor(civ.Index);

            var npcCiv = Assert.Single(state.Civilizations.Where(c => c.IsNpc));
            Assert.Single(npcCiv.Cities);
            Assert.Equal(NpcAggressivityLevel.Warlike, npcCiv.NpcParameters!.AggressivityLevel);
            Assert.Equal(NpcEvolutionLevel.Strong, npcCiv.NpcParameters!.EvolutionLevel);

            // La liste de modificateurs se termine exactement par le bonus fixe x2 (le reste de la
            // liste est la base économique/tech de Tier+2, qui peut légitimement contribuer aussi à
            // CITY_MAX_SOLDIERS_BONUS via une techno de bas tier, donc on ne teste pas la valeur
            // agrégée finale mais la présence exacte de la queue attendue).
            var expectedTail = Snapshot(AutoExtendController.BuildAggressiveModifiers(2));
            var actualTail = Snapshot(npcCiv.NpcParameters!.ExtraModifiers!.TakeLast(4));
            Assert.Equal(expectedTail, actualTail);
            Assert.True(npcCiv.CityMaxSoldiersBonus >= 170);
        }
    }
}
