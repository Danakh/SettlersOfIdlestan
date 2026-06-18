using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Controller.Expand;
using SOITests.TestUtilities;
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

            var mithril = map.GetVertex(PrestigeMap.MithrilMineVertex);
            Assert.NotNull(mithril);
            Assert.Contains(mithril!.Modifiers, m =>
                m.Category == ECategory.UNLOCK_RESOURCE && m.SubCategory == "Mithril");
            Assert.Contains(mithril.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "MithrilMine");

            Assert.NotNull(map.GetVertex(PrestigeMap.AbyssRiftVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.DeepProspectorsVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.TreasureHuntersVertex));
        }

        [Fact]
        public void PrestigeMap_AbyssRiftVertex_IsTheMostExpensiveOfTheBranch()
        {
            var map = PrestigeMapFactory.CreateDefault();
            var mithrilCost = map.GetVertex(PrestigeMap.AbyssRiftVertex)!.Cost;

            var branchVertices = new[]
            {
                PrestigeMap.DeepestMineVertex,
                PrestigeMap.MushroomCultureVertex,
                PrestigeMap.MithrilMineVertex,
                PrestigeMap.DeepProspectorsVertex,
                PrestigeMap.TreasureHuntersVertex,
            };

            foreach (var coord in branchVertices)
                Assert.True(mithrilCost >= map.GetVertex(coord)!.Cost,
                    $"L'Abîme ({mithrilCost}) doit être au moins aussi cher que {coord} ({map.GetVertex(coord)!.Cost})");
        }

        // ── Déblocage complet de l'Abîme (Faille des Abysses + Porte Planaire + Rituel de l'Éclipse Noire) ──

        [Fact]
        public void AbyssUnlock_NotReachedWithOnlyTwoOfThreeVertices()
        {
            var island = IslandTestFactory.CreateSevenHexIslandState();
            var prestige = new PrestigeState();
            prestige.PurchasedVertices.Add(PrestigeMap.AbyssRiftVertex);
            prestige.PurchasedVertices.Add(PrestigeMap.PlanarGateVertex);
            island.PlayerCivilization.AddCustomAggregator(new PrestigeModifierProvider(prestige, PrestigeMapController.DefaultMap));

            var controller = new CorruptionSpireController();
            Assert.False(controller.HasCorruptionSpireUnlocked(island.PlayerCivilization));
        }

        [Fact]
        public void AbyssUnlock_ReachedWithAllThreeVertices()
        {
            var island = IslandTestFactory.CreateSevenHexIslandState();
            var prestige = new PrestigeState();
            prestige.PurchasedVertices.Add(PrestigeMap.AbyssRiftVertex);
            prestige.PurchasedVertices.Add(PrestigeMap.PlanarGateVertex);
            prestige.PurchasedVertices.Add(PrestigeMap.DarkEclipseRitualVertex);
            island.PlayerCivilization.AddCustomAggregator(new PrestigeModifierProvider(prestige, PrestigeMapController.DefaultMap));

            var controller = new CorruptionSpireController();
            Assert.True(controller.HasCorruptionSpireUnlocked(island.PlayerCivilization));
        }
    }
}
