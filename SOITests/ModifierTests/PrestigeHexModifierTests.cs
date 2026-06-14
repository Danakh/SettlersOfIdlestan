using Xunit;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Controller.Expand;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates every PrestigeHex perVertexModifier defined in PrestigeMap.CreateDefault().
/// Strategy: each test purchases one vertex known to be adjacent to the hex under test,
/// then asserts the expected modifier value (perVertexModifier.Value × 1 adjacent vertex).
/// A scaling test (2 adjacent vertices) is included for one representative hex to cover
/// the multiplication mechanism.
///
/// Adjacency reference (vertex → 3 defining hex coords):
///   CentralVertex          (0,0),(1,0),(0,1)
///   BarracksVertex         (0,0),(1,0),(1,-1)
///   FortifiedOutpostVertex (0,0),(0,-1),(1,-1)
///   SeaportMarketVertex    (0,0),(0,1),(-1,1)
///   LaboratoryVertex       (1,0),(0,1),(1,1)
///   AppliedResearchVertex  (0,1),(1,1),(0,2)
///   AcademyVertex          (1,0),(2,0),(1,1)
///   HarvestGuildVertex     (-1,1),(-1,2),(0,1)
///   ArtisansGuildVertex    (-1,2),(0,1),(0,2)
///   MilitaryStrategyVertex (1,0),(2,0),(2,-1)
///   ConscriptionVertex     (1,0),(1,-1),(2,-1)
///   MilitaryAcademyVertex  (2,0),(2,-1),(3,-1)
///   KnowledgeMasteryVertex (1,1),(0,2),(1,2)
///   WatchtowerVertex       (0,0),(-1,0),(-1,1)
///   WarehouseNewCitiesVertex (0,0),(-1,0),(0,-1)
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

    // ── HarvestSpeedCoord (0,1) — HARVEST_SPEED "" +0.1 ─────────────────────
    // Adjacent vertices in map: Central, Laboratory, SeaportMarket, AppliedResearch,
    //                           HarvestGuild, ArtisansGuild

    [Fact]
    public void HarvestSpeedHex_NoAdjacentVertex_HarvestSpeedUnchanged()
    {
        // BarracksVertex touches (0,0),(1,0),(1,-1) — not adjacent to (0,1)
        var aggregator = BuildAggregator(PrestigeMap.BarracksVertex);
        Assert.Equal(0.0, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void HarvestSpeedHex_CentralVertex_HarvestSpeedPlus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.CentralVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    [Fact]
    public void HarvestSpeedHex_TwoAdjacentVertices_ScalesTo0Point2()
    {
        // CentralVertex + LaboratoryVertex both adjacent to (0,1) → modifier × 2
        var aggregator = BuildAggregator(PrestigeMap.CentralVertex, PrestigeMap.LaboratoryVertex);
        Assert.Equal(0.2, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    // ── ResearchSpeedCoord (1,0) — RESEARCH_SPEED "" +0.1 ───────────────────
    // Adjacent: Central, Barracks, Laboratory, Academy, MilitaryStrategy, Conscription

    [Fact]
    public void ResearchSpeedHex_CentralVertex_ResearchSpeedPlus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.CentralVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.RESEARCH_SPEED, "", 0.0), 5);
    }

    // ── UnitProductionSpeedCoord (1,-1) — UNIT_PRODUCTION_SPEED "" +0.1 ─────
    // Adjacent: Barracks, FortifiedOutpost, Conscription

    [Fact]
    public void UnitProductionSpeedHex_BarracksVertex_UnitProductionSpeedPlus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.BarracksVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.UNIT_PRODUCTION_SPEED, "", 0.0), 5);
    }

    // ── ResearchCostReductionCoord (1,1) — RESEARCH_COST_REDUCTION "" +0.1 ──
    // Adjacent: Laboratory, Academy, KnowledgeMastery, AppliedResearch

    [Fact]
    public void ResearchCostReductionHex_NoAdjacentVertex_IsUnchanged()
    {
        // BarracksVertex touches (0,0),(1,0),(1,-1) — not adjacent to (1,1)
        var aggregator = BuildAggregator(PrestigeMap.BarracksVertex);
        Assert.Equal(0.0, aggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0), 5);
    }

    [Fact]
    public void ResearchCostReductionHex_LaboratoryVertex_Plus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0), 5);
    }

    [Fact]
    public void ResearchCostReductionHex_TwoAdjacentVertices_ScalesTo0Point2()
    {
        // LaboratoryVertex (1,0),(0,1),(1,1) + AcademyVertex (1,0),(2,0),(1,1)
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex, PrestigeMap.AcademyVertex);
        Assert.Equal(0.2, aggregator.ApplyModifiers(ECategory.RESEARCH_COST_REDUCTION, "", 0.0), 5);
    }

    // ── StorageCapacityCoord (-1,1) — STORAGE_CAPACITY_BASIC "" +10
    //                                  STORAGE_CAPACITY_ADVANCED "" +5 ────────
    // Adjacent: SeaportMarket, Watchtower, MaritimeRoutes, HarvestGuild, TraderGuild

    [Fact]
    public void StorageCapacityHex_SeaportMarketVertex_BasicStoragePlus10()
    {
        var aggregator = BuildAggregator(PrestigeMap.SeaportMarketVertex);
        Assert.Equal(10, aggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_BASIC, "", 0));
    }

    [Fact]
    public void StorageCapacityHex_SeaportMarketVertex_AdvancedStoragePlus5()
    {
        var aggregator = BuildAggregator(PrestigeMap.SeaportMarketVertex);
        Assert.Equal(5, aggregator.ApplyModifiers(ECategory.STORAGE_CAPACITY_ADVANCED, "", 0));
    }

    // ── GoldTradeCoord (-1,2) — MARKET_GOLD_SPEED "" +0.1 ──────────────────
    // Adjacent: HarvestGuild, ArtisansGuild, TraderGuild

    [Fact]
    public void GoldTradeHex_HarvestGuildVertex_MarketGoldSpeedPlusTenPercent()
    {
        var aggregator = BuildAggregator(PrestigeMap.HarvestGuildVertex);
        Assert.Equal(1.1, aggregator.ApplyModifiers(ECategory.MARKET_GOLD_SPEED, "", 1.0), 5);
    }

    // ── ArtisansProductionCoord (0,2) — HARVEST_SPEED "Mine" +0.1
    //                                    HARVEST_SPEED "GlassWorks" +0.1 ─────
    // Adjacent: ArtisansGuild, AppliedResearch, KnowledgeMastery
    // Using KnowledgeMasteryVertex (1,1),(0,2),(1,2): adjacent to (0,2) but NOT to (0,1),
    // so no generic HARVEST_SPEED hex pollutes the result.

    [Fact]
    public void ArtisansProductionHex_KnowledgeMasteryVertex_MineHarvestSpeedPlus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.KnowledgeMasteryVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "Mine", 0.0), 5);
    }

    [Fact]
    public void ArtisansProductionHex_KnowledgeMasteryVertex_GlassWorksHarvestSpeedPlus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.KnowledgeMasteryVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "GlassWorks", 0.0), 5);
    }

    [Fact]
    public void ArtisansProductionHex_SubCategorySpecificModifiers_DoNotAffectGenericHarvestSpeed()
    {
        // SubCategory="Mine"/"GlassWorks" do not match a query with subCategory=""
        var aggregator = BuildAggregator(PrestigeMap.KnowledgeMasteryVertex);
        Assert.Equal(0.0, aggregator.ApplyModifiers(ECategory.HARVEST_SPEED, "", 0.0), 5);
    }

    // ── FortifiedOutpostCoord (0,-1) — CITY_DEFENSE "" +2 ───────────────────
    // Adjacent: FortifiedOutpost, WarehouseNewCities

    [Fact]
    public void FortifiedOutpostHex_FortifiedOutpostVertex_CityDefensePlus2()
    {
        var aggregator = BuildAggregator(PrestigeMap.FortifiedOutpostVertex);
        Assert.Equal(2, aggregator.ApplyModifiers(ECategory.CITY_DEFENSE, "", 0));
    }

    // ── ExperimentalScienceCoord (2,0) — BUILDING_PRODUCTION "Laboratory" +1 ─
    // Adjacent: Academy, MilitaryStrategy, MilitaryAcademy

    [Fact]
    public void ExperimentalScienceHex_AcademyVertex_LaboratoryProductionPlus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.AcademyVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_PRODUCTION, "Laboratory", 0));
    }

    // ── DefenseRegenCoord (2,-1) — CITY_DEFENSE_REGEN_SPEED "" +0.1 ─────────
    // Adjacent: MilitaryStrategy, Conscription, MilitaryAcademy

    [Fact]
    public void DefenseRegenHex_MilitaryStrategyVertex_CityDefenseRegenSpeedPlus0Point1()
    {
        var aggregator = BuildAggregator(PrestigeMap.MilitaryStrategyVertex);
        Assert.Equal(0.1, aggregator.ApplyModifiers(ECategory.CITY_DEFENSE_REGEN_SPEED, "", 0.0), 5);
    }

    // ── WarehouseMaxLevelCoord (-1,0) — BUILDING_MAX_LEVEL "Warehouse" +1 ────
    // Adjacent: Watchtower, MaritimeRoutes, WarehouseNewCities

    [Fact]
    public void WarehouseMaxLevelHex_WatchtowerVertex_WarehouseMaxLevelPlus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.WatchtowerVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Warehouse", 0));
    }
}
