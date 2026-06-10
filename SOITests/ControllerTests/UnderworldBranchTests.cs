using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>Tests de la branche de l'Inframonde : vertex de prestige nord-ouest et hexes associés.</summary>
    public class UnderworldBranchTests
    {
        [Fact]
        public void PrestigeMap_UnderworldBranchVertices_AreDefined()
        {
            var map = PrestigeMapFactory.CreateDefault();

            var gate = map.GetVertex(PrestigeMap.DeepestMineVertex);
            Assert.NotNull(gate);
            Assert.Contains(gate!.Modifiers, m => m.Category == ECategory.UNLOCK_DEEPEST_MINE);
            Assert.Contains(gate.Modifiers, m =>
                m.Category == ECategory.UNLOCK_RESEARCH && m.SubCategory == "Speleologie");

            var mushroom = map.GetVertex(PrestigeMap.MushroomCultureVertex);
            Assert.NotNull(mushroom);
            Assert.Contains(mushroom!.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "MushroomFarm");

            var mithril = map.GetVertex(PrestigeMap.MithrilVertex);
            Assert.NotNull(mithril);
            Assert.Contains(mithril!.Modifiers, m =>
                m.Category == ECategory.UNLOCK_RESOURCE && m.SubCategory == "Mithril");
            Assert.Contains(mithril.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "MithrilMine");

            Assert.NotNull(map.GetVertex(PrestigeMap.UnderworldWatchVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.DeepProspectorsVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.TreasureHuntersVertex));
        }

        [Fact]
        public void PrestigeMap_DeepestMineVertex_CostsMoreThanSteelSecret()
        {
            var map = PrestigeMapFactory.CreateDefault();
            var gate = map.GetVertex(PrestigeMap.DeepestMineVertex)!;
            var steelSecret = map.GetVertex(PrestigeMap.SteelSecretVertex)!;

            // La Mine Profonde est verrouillée derrière un vertex plus cher
            // que celui qui débloque l'Acier (Secret de l'Acier).
            Assert.True(gate.Cost > steelSecret.Cost,
                $"La porte de l'Inframonde ({gate.Cost}) doit coûter plus que le Secret de l'Acier ({steelSecret.Cost})");
        }

        [Fact]
        public void PrestigeMap_MithrilVertex_IsTheMostExpensiveOfTheBranch()
        {
            var map = PrestigeMapFactory.CreateDefault();
            var mithrilCost = map.GetVertex(PrestigeMap.MithrilVertex)!.Cost;

            var branchVertices = new[]
            {
                PrestigeMap.DeepestMineVertex,
                PrestigeMap.MushroomCultureVertex,
                PrestigeMap.UnderworldWatchVertex,
                PrestigeMap.DeepProspectorsVertex,
                PrestigeMap.TreasureHuntersVertex,
            };

            foreach (var coord in branchVertices)
                Assert.True(mithrilCost >= map.GetVertex(coord)!.Cost,
                    $"Le Mithril ({mithrilCost}) doit être au moins aussi cher que {coord} ({map.GetVertex(coord)!.Cost})");
        }

        [Fact]
        public void PrestigeMap_UnderworldHexes_GrantPerVertexModifiers()
        {
            var map = PrestigeMapFactory.CreateDefault();

            var excavations = map.GetHex(PrestigeMap.ExcavationsCoord);
            Assert.NotNull(excavations);
            Assert.Contains(excavations!.PerVertexModifiers, m =>
                m.Category == ECategory.HARVEST_SPEED && m.SubCategory == "Mine");

            var underworld = map.GetHex(PrestigeMap.UnderworldCoord);
            Assert.NotNull(underworld);
            Assert.Contains(underworld!.PerVertexModifiers, m =>
                m.Category == ECategory.HARVEST_SPEED && m.SubCategory == "MushroomFarm");
            // 6 vertex adjacents : porte + 4 vertex thématiques + 1 placeholder
            Assert.Equal(6, underworld.AdjacentVertices.Count);
        }
    }
}
