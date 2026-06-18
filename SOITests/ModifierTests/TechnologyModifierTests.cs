using Xunit;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates every technology modifier defined in TechnologyDefinitions.All.
/// Each test completes exactly one technology in isolation and asserts the expected
/// modifier value via ModifierAggregator, confirming the definition is correct.
/// Base value is always 0 / 0.0 so the result directly equals the modifier's value.
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

    // ── HARVEST_SPEED ─────────────────────────────────────────────────────────

    [Fact]
    public void NoResearch_HarvestSpeed_IsUnchanged()
    {
        Assert.Equal(0.0, BuildAggregator().ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void HarvestEfficiency_HarvestSpeed_Plus0Point1()
    {
        Assert.Equal(0.1, BuildAggregator(TechnologyId.HarvestEfficiency).ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void ImprovedHarvest_HarvestSpeed_Plus0Point15()
    {
        Assert.Equal(0.15, BuildAggregator(TechnologyId.ImprovedHarvest).ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MasterHarvest_HarvestSpeed_Plus0Point25()
    {
        Assert.Equal(0.25, BuildAggregator(TechnologyId.MasterHarvest).ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void EpicHarvest_HarvestSpeed_Plus0Point35()
    {
        Assert.Equal(0.35, BuildAggregator(TechnologyId.EpicHarvest).ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    // ── FORGE_DOUBLE_HARVEST_BONUS ────────────────────────────────────────────

    [Fact]
    public void Artisanat_ForgeDoubleHarvestBonus_Plus5()
    {
        Assert.Equal(5, BuildAggregator(TechnologyId.Artisanat).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    [Fact]
    public void Metallurgy_ForgeDoubleHarvestBonus_Plus10()
    {
        Assert.Equal(10, BuildAggregator(TechnologyId.Metallurgy).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    [Fact]
    public void MaitriseDesAlliages_ForgeDoubleHarvestBonus_Plus15()
    {
        Assert.Equal(15, BuildAggregator(TechnologyId.MaitriseDesAlliages).ApplyModifiers(ECategory.FORGE_DOUBLE_HARVEST_BONUS, "", 0));
    }

    // ── UNLOCK_WONDERS ────────────────────────────────────────────────────────

    [Fact]
    public void Architecture_UnlocksWonders()
    {
        Assert.True(BuildAggregator(TechnologyId.Architecture).HasModifier(ECategory.UNLOCK_WONDERS));
    }

    [Fact]
    public void NoResearch_WondersNotUnlocked()
    {
        Assert.False(BuildAggregator().HasModifier(ECategory.UNLOCK_WONDERS));
    }

    // ── UNIT_PRODUCTION_SPEED ─────────────────────────────────────────────────

    [Fact]
    public void MilitaryDiscipline_UnitProductionSpeed_Plus0Point1()
    {
        Assert.Equal(0.1, BuildAggregator(TechnologyId.MilitaryDiscipline).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MilitaryTactics_UnitProductionSpeed_Plus0Point15()
    {
        Assert.Equal(0.15, BuildAggregator(TechnologyId.MilitaryTactics).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MilitaryMastery_UnitProductionSpeed_Plus0Point25()
    {
        Assert.Equal(0.25, BuildAggregator(TechnologyId.MilitaryMastery).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void AdvancedTactics_NoUnitProductionSpeedModifier()
    {
        Assert.Equal(0.0, BuildAggregator(TechnologyId.AdvancedTactics).ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    // ── CITY_ATTACK_RANGE ─────────────────────────────────────────────────────

    [Fact]
    public void MilitaryTactics_CityAttackRange_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.MilitaryTactics).ApplyModifiers(ECategory.CITY_ATTACK_RANGE, "", 0));
    }

    // ── HARVEST_PRODUCTION_BONUS ──────────────────────────────────────────────

    [Fact]
    public void Agriculture_HarvestProductionBonus_Mill_Plus50()
    {
        Assert.Equal(50, BuildAggregator(TechnologyId.Agriculture).ApplyModifiers(ECategory.HARVEST_PRODUCTION_BONUS, "Mill", 0));
    }

    // ── STORAGE_CAPACITY_BASIC ────────────────────────────────────────────────

    [Fact]
    public void StorageOptimization_StorageCapacityBasic_Plus20()
    {
        Assert.Equal(20, BuildAggregator(TechnologyId.StorageOptimization).ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0));
    }

    // ── RESEARCH_SPEED ────────────────────────────────────────────────────────

    [Fact]
    public void Archivage_ResearchSpeed_Plus0Point15()
    {
        Assert.Equal(0.15, BuildAggregator(TechnologyId.Archivage).ApplyModifiers(ECategory.RESEARCH_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void Scholarship_ResearchSpeed_Plus0Point2()
    {
        Assert.Equal(0.2, BuildAggregator(TechnologyId.Scholarship).ApplyModifiers(ECategory.RESEARCH_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void ImprovedResearch_ResearchSpeed_Plus0Point2()
    {
        Assert.Equal(0.2, BuildAggregator(TechnologyId.ImprovedResearch).ApplyModifiers(ECategory.RESEARCH_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void MasterResearch_ResearchSpeed_Plus0Point3()
    {
        Assert.Equal(0.3, BuildAggregator(TechnologyId.MasterResearch).ApplyModifiers(ECategory.RESEARCH_SPEED, "", 0.0), 5);
    }

    // ── MINE_GOLD_CHANCE_PERCENT ──────────────────────────────────────────────

    [Fact]
    public void Orpaillage_MineGoldChancePercent_Plus10()
    {
        Assert.Equal(10, BuildAggregator(TechnologyId.Orpaillage).ApplyModifiers(ECategory.MINE_GOLD_CHANCE_PERCENT, "", 0));
    }

    // ── RESEARCH_COST_REDUCTION ───────────────────────────────────────────────

    [Fact]
    public void ResearchMethods_ResearchCostReduction_Plus0Point1()
    {
        Assert.Equal(0.1, BuildAggregator(TechnologyId.ResearchMethods).ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0), 5);
    }


    // ── BUILDING_MAX_LEVEL ────────────────────────────────────────────────────

    [Fact]
    public void MilitaryBuildings_BarracksMaxLevel_Plus2()
    {
        Assert.Equal(2, BuildAggregator(TechnologyId.MilitaryBuildings).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", 0));
    }

    [Fact]
    public void HarvestTools_SawmillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.HarvestTools).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Sawmill", 0));
    }

    [Fact]
    public void HarvestTools_QuarryMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.HarvestTools).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Mill", 0));
    }

    [Fact]
    public void AdvancedArchitecture_SawmillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Sawmill", 0));
    }

    [Fact]
    public void AdvancedArchitecture_BrickworksMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Brickworks", 0));
    }

    [Fact]
    public void AdvancedArchitecture_QuarryMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Quarry", 0));
    }

    [Fact]
    public void AdvancedArchitecture_MillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.AdvancedArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Mill", 0));
    }

    [Fact]
    public void GrandArchitecture_MarketMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.GrandArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Market", 0));
    }

    [Fact]
    public void GrandArchitecture_LibraryMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.GrandArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", 0));
    }

    [Fact]
    public void GrandArchitecture_BarracksMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.GrandArchitecture).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", 0));
    }

    // ── TRADE_GOLD_PACKAGES ───────────────────────────────────────────────────

    [Fact]
    public void TradeRoutes_TradeGoldPackages_Plus3()
    {
        Assert.Equal(3.0, BuildAggregator(TechnologyId.TradeRoutes).ApplyModifiers(ECategory.TRADE_GOLD_PACKAGES, "", 0.0), 5);
    }


    // ── CITY_DEFENSE ──────────────────────────────────────────────────────────

    [Fact]
    public void MilitaryMastery_CityDefense_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.MilitaryMastery).ApplyModifiers(ECategory.CITY_DEFENSE, "", 0));
    }

    [Fact]
    public void AdvancedStrategy_NoCityDefenseModifier()
    {
        Assert.Equal(0, BuildAggregator(TechnologyId.AdvancedStrategy).ApplyModifiers(ECategory.CITY_DEFENSE, "", 0));
    }

    // ── BUILDING_MAX_LEVEL (Compagnonage) ─────────────────────────────────────

    [Fact]
    public void Compagnonage_MillMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.Compagnonage).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Quarry", 0));
    }

    [Fact]
    public void Compagnonage_BrickworksMaxLevel_Plus1()
    {
        Assert.Equal(1, BuildAggregator(TechnologyId.Compagnonage).ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Brickworks", 0));
    }
}
