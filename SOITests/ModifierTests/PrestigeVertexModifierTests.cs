using Xunit;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Controller.Expand;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates that vertex modifiers from PrestigeMap are correctly provided
/// by PrestigeModifierProvider and applied through ModifierAggregator.
/// One vertex is purchased per test to isolate the mechanism.
/// </summary>
public class PrestigeVertexModifierTests
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

    // ── BUILDING_MAX_LEVEL — CentralVertex (+3 Library) ──────────────────────

    [Fact]
    public void NoPurchase_BuildingMaxLevel_ReturnsBaseValue()
    {
        var aggregator = BuildAggregator();
        int baseLevel = new Library().GetDefaultMaxLevel();

        Assert.Equal(baseLevel, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", baseLevel));
    }

    [Fact]
    public void CentralVertex_LibraryMaxLevel_IncreasedByThree()
    {
        var aggregator = BuildAggregator(PrestigeMap.CentralVertex);
        int baseLevel = new Library().GetDefaultMaxLevel();

        Assert.Equal(baseLevel + 3, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", baseLevel));
    }

    [Fact]
    public void CentralVertex_ModifierIsIsolated_OtherBuildingUnaffected()
    {
        var aggregator = BuildAggregator(PrestigeMap.CentralVertex);
        int baseLevel = new Sawmill().GetDefaultMaxLevel();

        Assert.Equal(baseLevel, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Sawmill", baseLevel));
    }
}
