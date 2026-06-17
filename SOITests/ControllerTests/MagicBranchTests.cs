using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using System.Linq;
using Xunit;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ControllerTests
{
    /// <summary>Tests de la branche de la Magie : vertex de prestige sud et hex Lignes Telluriques.</summary>
    public class MagicBranchTests
    {
        [Fact]
        public void PrestigeMap_MagicBranchVertices_AreDefined()
        {
            var map = PrestigeMapFactory.CreateDefault();

            var gate = map.GetVertex(PrestigeMap.MagicSecretVertex);
            Assert.NotNull(gate);
            Assert.Contains(gate!.Modifiers, m => m.Category == ECategory.UNLOCK_MAGIC);
            Assert.Contains(gate.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "MageTower");
            Assert.Contains(gate.Modifiers, m =>
                m.Category == ECategory.UNLOCK_RESEARCH && m.SubCategory == "MagicInitiation");

            var alchimistHut = map.GetVertex(PrestigeMap.AlchimistHutVertex);
            Assert.NotNull(alchimistHut);
            Assert.Contains(alchimistHut!.Modifiers, m =>
                m.Category == ECategory.MAGIC_FEATURE_COUNT && m.SubCategory == "FairyCircle");
            Assert.Contains(alchimistHut.Modifiers, m =>
                m.Category == ECategory.UNLOCK_RESOURCE && m.SubCategory == "Crystal");
            Assert.Contains(alchimistHut.Modifiers, m =>
                m.Category == ECategory.BUILDING_MAX_LEVEL && m.SubCategory == "AlchimistHut");
            Assert.Contains(alchimistHut.Modifiers, m =>
                m.Category == ECategory.UNLOCK_HEALING_POTION);

            var dolmens = map.GetVertex(PrestigeMap.DolmensVertex);
            Assert.NotNull(dolmens);
            Assert.Contains(dolmens!.Modifiers, m =>
                m.Category == ECategory.MAGIC_FEATURE_COUNT && m.SubCategory == "Dolmen");

            Assert.NotNull(map.GetVertex(PrestigeMap.FocalizationVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.InnerCircleVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.CrystalomancyVertex));
            Assert.NotNull(map.GetVertex(PrestigeMap.ArchmageVertex));
        }

        [Fact]
        public void PrestigeMap_MagicGate_CostsAtLeastAsMuchAsUnderworldGate()
        {
            var map = PrestigeMapFactory.CreateDefault();
            var magicGate = map.GetVertex(PrestigeMap.MagicSecretVertex)!;
            var underworldGate = map.GetVertex(PrestigeMap.DeepestMineVertex)!;

            // La magie n'est disponible que dans l'Inframonde : sa porte doit coûter
            // au moins autant que la porte de l'Inframonde.
            Assert.True(magicGate.Cost >= underworldGate.Cost,
                $"La porte de la Magie ({magicGate.Cost}) doit coûter au moins autant que la porte de l'Inframonde ({underworldGate.Cost})");
        }

        [Fact]
        public void PrestigeMap_FairyCirclesAndDolmens_AreCheaperThanMagicGate()
        {
            var map = PrestigeMapFactory.CreateDefault();
            int gateCost = map.GetVertex(PrestigeMap.MagicSecretVertex)!.Cost;

            // Les vertex précurseurs (source de cristaux en surface) sont accessibles
            // avant la porte, pour pouvoir démarrer la magie sans l'Inframonde.
            Assert.True(map.GetVertex(PrestigeMap.AlchimistHutVertex)!.Cost < gateCost);
            Assert.True(map.GetVertex(PrestigeMap.DolmensVertex)!.Cost < gateCost);
        }

        [Fact]
        public void PrestigeMap_ArchmageVertex_IsTheMostExpensiveOfTheBranch()
        {
            var map = PrestigeMapFactory.CreateDefault();
            var archmageCost = map.GetVertex(PrestigeMap.ArchmageVertex)!.Cost;

            var branchVertices = new[]
            {
                PrestigeMap.AlchimistHutVertex,
                PrestigeMap.DolmensVertex,
                PrestigeMap.MagicSecretVertex,
                PrestigeMap.FocalizationVertex,
                PrestigeMap.InnerCircleVertex,
                PrestigeMap.CrystalomancyVertex,
            };

            foreach (var coord in branchVertices)
                Assert.True(archmageCost >= map.GetVertex(coord)!.Cost,
                    $"L'Archimage ({archmageCost}) doit être au moins aussi cher que {coord} ({map.GetVertex(coord)!.Cost})");
        }

        [Fact]
        public void PrestigeMap_LeyLinesHex_GrantsRitualPowerPerVertex()
        {
            var map = PrestigeMapFactory.CreateDefault();

            var leyLines = map.GetHex(PrestigeMap.LeyLinesCoord);
            Assert.NotNull(leyLines);
            Assert.Contains(leyLines!.PerVertexModifiers, m =>
                m.Category == ECategory.RITUAL_TOTAL_POWER);
            // 6 vertex adjacents : 2 entrées + porte + 3 vertex profonds
            Assert.Equal(6, leyLines.AdjacentVertices.Count);
        }
    }
}
