using Xunit;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Controller.Expand;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SOITests.ModifierTests;

/// <summary>
/// Validates every vertex modifier defined in PrestigeMap.CreateDefault().
/// Each test purchases exactly one vertex and asserts the expected modifier value,
/// ensuring the PrestigeModifierProvider correctly exposes vertex modifiers.
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

    // ── BUILDING_MAX_LEVEL ────────────────────────────────────────────────────

    [Fact]
    public void NoPurchase_BuildingMaxLevel_ReturnsBaseValue()
    {
        var aggregator = BuildAggregator();
        Assert.Equal(0, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", 0));
    }

    [Fact]
    public void CentralVertex_LibraryMaxLevel_Plus3()
    {
        var aggregator = BuildAggregator(PrestigeMap.CentralVertex);
        Assert.Equal(3, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Library", 0));
    }

    [Fact]
    public void WatchtowerVertex_WatchtowerMaxLevel_Plus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.WatchtowerVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Watchtower", 0));
    }

    [Fact]
    public void TraderGuildVertex_TraderGuildMaxLevel_Plus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.TraderGuildVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "TraderGuild", 0));
    }

    [Fact]
    public void LaboratoryVertex_LaboratoryMaxLevel_Plus2()
    {
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex);
        Assert.Equal(2, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Laboratory", 0));
    }

    [Fact]
    public void LaboratoryVertex_GlassWorksMaxLevel_Plus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "GlassWorks", 0));
    }

    [Fact]
    public void BarracksVertex_BarracksMaxLevel_Plus2()
    {
        var aggregator = BuildAggregator(PrestigeMap.BarracksVertex);
        Assert.Equal(2, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Barracks", 0));
    }

    [Fact]
    public void HarvestGuildVertex_HarvestersGuildMaxLevel_Plus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.HarvestGuildVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "HarvestersGuild", 0));
    }

    [Fact]
    public void ArtisansGuildVertex_ArtisansGuildMaxLevel_Plus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.ArtisansGuildVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "ArtisansGuild", 0));
    }

    [Fact]
    public void MilitaryAcademyVertex_MilitaryAcademyMaxLevel_Plus4()
    {
        var aggregator = BuildAggregator(PrestigeMap.MilitaryAcademyVertex);
        Assert.Equal(4, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "MilitaryAcademy", 0));
    }

    [Fact]
    public void AcademyVertex_AcademyMaxLevel_Plus1()
    {
        var aggregator = BuildAggregator(PrestigeMap.AcademyVertex);
        Assert.Equal(1, aggregator.ApplyModifiers(ECategory.BUILDING_MAX_LEVEL, "Academy", 0));
    }

    // ── STARTING_CITY_BUILDING ────────────────────────────────────────────────

    [Fact]
    public void SeaportMarketVertex_HasStartingCityBuilding_Seaport()
    {
        var aggregator = BuildAggregator(PrestigeMap.SeaportMarketVertex);
        Assert.True(aggregator.HasModifier(ECategory.STARTING_CITY_BUILDING, "Seaport"));
    }

    [Fact]
    public void SeaportMarketVertex_HasStartingCityBuilding_Market()
    {
        var aggregator = BuildAggregator(PrestigeMap.SeaportMarketVertex);
        Assert.True(aggregator.HasModifier(ECategory.STARTING_CITY_BUILDING, "Market"));
    }

    // ── NEW_CITY_BUILDING ─────────────────────────────────────────────────────

    [Fact]
    public void WarehouseNewCitiesVertex_HasNewCityBuilding_Warehouse()
    {
        var aggregator = BuildAggregator(PrestigeMap.WarehouseNewCitiesVertex);
        Assert.True(aggregator.HasModifier(ECategory.NEW_CITY_BUILDING, "Warehouse"));
    }

    [Fact]
    public void FortifiedOutpostVertex_HasNewCityBuilding_Palisade()
    {
        var aggregator = BuildAggregator(PrestigeMap.FortifiedOutpostVertex);
        Assert.True(aggregator.HasModifier(ECategory.NEW_CITY_BUILDING, "Palisade"));
    }

    // ── UNLOCK_MARITIME_ROUTES ────────────────────────────────────────────────

    [Fact]
    public void MaritimeRoutesVertex_UnlocksMaritimeRoutes()
    {
        var aggregator = BuildAggregator(PrestigeMap.MaritimeRoutesVertex);
        Assert.True(aggregator.HasModifier(ECategory.UNLOCK_MARITIME_ROUTES));
    }

    [Fact]
    public void NoPurchase_MaritimeRoutes_NotPresent()
    {
        var aggregator = BuildAggregator();
        Assert.False(aggregator.HasModifier(ECategory.UNLOCK_MARITIME_ROUTES));
    }

    // ── UNLOCK_RESOURCE ───────────────────────────────────────────────────────

    [Fact]
    public void LaboratoryVertex_UnlocksGlassResource()
    {
        var aggregator = BuildAggregator(PrestigeMap.LaboratoryVertex);
        Assert.True(aggregator.HasModifier(ECategory.UNLOCK_RESOURCE, "Glass"));
    }

    // ── UNLOCK_RESEARCH ───────────────────────────────────────────────────────

    [Fact]
    public void AppliedResearchVertex_UnlocksResearch_Artisanat()
    {
        var aggregator = BuildAggregator(PrestigeMap.AppliedResearchVertex);
        Assert.True(aggregator.HasModifier(ECategory.UNLOCK_RESEARCH, "Artisanat"));
    }

    [Fact]
    public void MilitaryStrategyVertex_UnlocksResearch_MilitaryDiscipline()
    {
        var aggregator = BuildAggregator(PrestigeMap.MilitaryStrategyVertex);
        Assert.True(aggregator.HasModifier(ECategory.UNLOCK_RESEARCH, "MilitaryDiscipline"));
    }

    [Fact]
    public void KnowledgeMasteryVertex_UnlocksResearch_Archivage()
    {
        var aggregator = BuildAggregator(PrestigeMap.KnowledgeMasteryVertex);
        Assert.True(aggregator.HasModifier(ECategory.UNLOCK_RESEARCH, "Archivage"));
    }

    [Fact]
    public void WatchtowerVertex_UnlocksResearch_Scouting()
    {
        var aggregator = BuildAggregator(PrestigeMap.WatchtowerVertex);
        Assert.True(aggregator.HasModifier(ECategory.UNLOCK_RESEARCH, "Scouting"));
    }

    // ── CITY_MAX_SOLDIERS_BONUS ───────────────────────────────────────────────

    [Fact]
    public void ConscriptionVertex_CityMaxSoldiersBonus_Plus5()
    {
        var aggregator = BuildAggregator(PrestigeMap.ConscriptionVertex);
        Assert.Equal(5, aggregator.ApplyModifiers(ECategory.CITY_MAX_SOLDIERS_BONUS, "", 0));
    }
}
