using Xunit;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates that completed technologies expose the correct modifiers through
/// TechnologyTree (which implements IModifierProvider) and that they apply
/// correctly via ModifierAggregator.
/// </summary>
public class TechnologyModifierTests
{
    private static ModifierAggregator BuildAggregator(params TechnologyId[] completed)
    {
        var tree = new TechnologyTree();
        foreach (var tech in completed)
            tree.CompleteResearch(tech);
        var aggregator = new ModifierAggregator();
        aggregator.Register(tree);
        return aggregator;
    }

    // ── HARVEST_SPEED — HarvestEfficiency (+0.1) and ImprovedHarvest (+0.15) ─

    [Fact]
    public void NoResearch_HarvestSpeed_IsUnchanged()
    {
        var aggregator = BuildAggregator();

        Assert.Equal(1.0, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0), 5);
    }

    [Fact]
    public void HarvestEfficiency_HarvestSpeed_IncreasedByPointOne()
    {
        var aggregator = BuildAggregator(TechnologyId.HarvestEfficiency);

        Assert.Equal(1.1, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0), 5);
    }

    [Fact]
    public void HarvestEfficiencyAndImprovedHarvest_HarvestSpeed_ModifiersStack()
    {
        // HarvestEfficiency: +0.1, ImprovedHarvest: +0.15 → total 1.25
        var aggregator = BuildAggregator(TechnologyId.HarvestEfficiency, TechnologyId.ImprovedHarvest);

        Assert.Equal(1.25, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 1.0), 5);
    }

    [Fact]
    public void HarvestEfficiency_DoesNotAffectOtherCategory()
    {
        var aggregator = BuildAggregator(TechnologyId.HarvestEfficiency);

        Assert.Equal(1.0, aggregator.ApplyModifiers(ECategory.RESEARCH_SPEED, "", 1.0), 5);
    }
}
