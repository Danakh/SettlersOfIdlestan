using Xunit;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Controller.Expand;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates that PrestigeHex perVertexModifiers scale correctly with the number
/// of adjacent purchased vertices.
/// Hex under test: ResearchCostReductionCoord (1,1) — RESEARCH_COST_REDUCTION +0.1 per vertex.
/// Adjacent vertices in the default map: LaboratoryVertex, AcademyVertex,
/// KnowledgeMasteryVertex, AppliedResearchVertex.
/// </summary>
public class PrestigeHexModifierTests
{
    private static ModifierAggregator BuildAggregator(params Vertex[] purchased)
    {
        var prestige = new PrestigeState();
        foreach (var v in purchased)
            prestige.PurchasedVertices.Add(v);
        var provider = new PrestigeModifierProvider(prestige, PrestigeMapController.DefaultMap);
        var aggregator = new ModifierAggregator();
        aggregator.Register(provider);
        return aggregator;
    }

    // ── RESEARCH_COST_REDUCTION — ResearchCostReductionCoord ─────────────────

    [Fact]
    public void NoAdjacentVertex_ResearchCostReduction_IsUnchanged()
    {
        // BarracksVertex touches (0,0),(1,0),(1,-1) — not adjacent to (1,1)
        var aggregator = BuildAggregator(PrestigeMap.BarracksVertex);

        double result = aggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0);

        Assert.Equal(0.0, result, 5);
    }

    [Fact]
    public void OneAdjacentVertex_ResearchCostReduction_IncreasedByOneStep()
    {
        // LaboratoryVertex touches (1,0),(0,1),(1,1) — adjacent to ResearchCostReductionCoord
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex);

        double result = aggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0);

        Assert.Equal(0.1, result, 5);
    }

    [Fact]
    public void TwoAdjacentVertices_ResearchCostReduction_ScalesWithCount()
    {
        // LaboratoryVertex (1,0),(0,1),(1,1) and AcademyVertex (1,0),(2,0),(1,1)
        // both adjacent to ResearchCostReductionCoord → modifier value = 0.1 × 2
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex, PrestigeMap.AcademyVertex);

        double result = aggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0);

        Assert.Equal(0.2, result, 5);
    }
}
