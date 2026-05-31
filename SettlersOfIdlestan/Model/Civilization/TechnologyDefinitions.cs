using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Civilization;

public static class TechnologyDefinitions
{
    public static IReadOnlyList<Technology> All { get; } = new Technology[]
    {
        // === TIER 0 — Racines (pas de prérequis) ===

        new(TechnologyId.HarvestEfficiency,
            "tech_harvest_efficiency_name", "tech_harvest_efficiency_desc",
            cost: 100,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) },
            tier: 0, line: 1),

        new(TechnologyId.Artisanat,
            "tech_artisanat_name", "tech_artisanat_desc",
            cost: 120,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 5) },
            tier: 0, line: 2),

        new(TechnologyId.Architecture,
            "tech_architecture_name", "tech_architecture_desc",
            cost: 100,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_WONDERS, EType.ADDITIVE, 1) },
            tier: 0, line: 5),

        new(TechnologyId.MilitaryDiscipline,
            "tech_military_discipline_name", "tech_military_discipline_desc",
            cost: 120,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) },
            tier: 0, line: 7),

        // === TIER 1 ===
        
        new(TechnologyId.Agriculture,
            "tech_agriculture_name", "tech_agriculture_desc",
            cost: 600,
            prerequisites: new[] { TechnologyId.HarvestEfficiency },
            modifiers: new Modifier[] { new(ECategory.HARVEST_PRODUCTION_BONUS, "Mill", EType.ADDITIVE, 50) },
            tier: 1, line: 0),

        new(TechnologyId.ImprovedHarvest,
            "tech_improved_harvest_name", "tech_improved_harvest_desc",
            cost: 400,
            prerequisites: new[] { TechnologyId.HarvestEfficiency },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.15) },
            tier: 1, line: 1),

        new(TechnologyId.StorageOptimization,
            "tech_storage_optimization_name", "tech_storage_optimization_desc",
            cost: 300,
            prerequisites: new[] { TechnologyId.Architecture },
            modifiers: new Modifier[] { new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 20) },
            tier: 1, line: 5),

        new(TechnologyId.Erudition,
            "tech_erudition_name", "tech_erudition_desc",
            cost: 330,
            prerequisites: new[] { TechnologyId.Architecture },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.15) },
            tier: 1, line: 4),

        new(TechnologyId.Orpaillage,
            "tech_orpaillage_name", "tech_orpaillage_desc",
            cost: 330,
            prerequisites: new[] { TechnologyId.Artisanat },
            modifiers: new Modifier[] { new(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10) },
            tier: 1, line: 2),

        new(TechnologyId.MilitaryTactics,
            "tech_military_tactics_name", "tech_military_tactics_desc",
            cost: 500,
            prerequisites: new[] { TechnologyId.MilitaryDiscipline },
            modifiers: new Modifier[]
            {
                new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.15),
                new(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1),
            },
            tier: 1, line: 7),

        new(TechnologyId.MilitaryBuildings,
            "tech_military_buildings_name", "tech_military_buildings_desc",
            cost: 400,
            prerequisites: new[] { TechnologyId.Architecture, TechnologyId.MilitaryDiscipline },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2) },
            tier: 1, line: 6),

        // === TIER 2 ===

        new(TechnologyId.HarvestTools,
            "tech_harvest_tools_name", "tech_harvest_tools_desc",
            cost: 1665,
            prerequisites: new[] { TechnologyId.ImprovedHarvest },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Sawmill", EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Quarry",  EType.ADDITIVE, 1),
            },
            tier: 2, line: 1),

        new(TechnologyId.AdvancedArchitecture,
            "tech_advanced_architecture_name", "tech_advanced_architecture_desc",
            cost: 1665,
            prerequisites: new[] { TechnologyId.StorageOptimization },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Sawmill",    EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Brickworks", EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Quarry",     EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Mill",       EType.ADDITIVE, 1),
            },
            tier: 2, line: 5),

        new(TechnologyId.ResearchMethods,
            "tech_research_methods_name", "tech_research_methods_desc",
            cost: 1425,
            prerequisites: new[] { TechnologyId.Erudition },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) },
            tier: 2, line: 4),

        new(TechnologyId.Metallurgy,
            "tech_metallurgy_name", "tech_metallurgy_desc",
            cost: 1665,
            prerequisites: new[] { TechnologyId.Orpaillage },
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 10) },
            tier: 2, line: 2),

        new(TechnologyId.MilitaryMastery,
            "tech_military_mastery_name", "tech_military_mastery_desc",
            cost: 2085,
            prerequisites: new[] { TechnologyId.MilitaryTactics },
            modifiers: new Modifier[]
            {
                new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.25),
                new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 1),
            },
            tier: 2, line: 7),

        new(TechnologyId.SpecializedMarket,
            "tech_specialized_market_name", "tech_specialized_market_desc",
            cost: 1500,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Market", EType.ADDITIVE, 1) },
            tier: 2, line: 3),

        // === TIER 3 ===

        new(TechnologyId.MasterHarvest,
            "tech_master_harvest_name", "tech_master_harvest_desc",
            cost: 5560,
            prerequisites: new[] { TechnologyId.HarvestTools },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.25) },
            tier: 3, line: 1),

        new(TechnologyId.GrandArchitecture,
            "tech_grand_architecture_name", "tech_grand_architecture_desc",
            cost: 5560,
            prerequisites: new[] { TechnologyId.AdvancedArchitecture },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Market",  EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 1),
            },
            tier: 3, line: 5),

        new(TechnologyId.Scholarship,
            "tech_scholarship_name", "tech_scholarship_desc",
            cost: 5200,
            prerequisites: new[] { TechnologyId.ResearchMethods },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.2) },
            tier: 3, line: 4),

        new(TechnologyId.Masterwork,
            "tech_masterwork_name", "tech_masterwork_desc",
            cost: 5560,
            prerequisites: new[] { TechnologyId.Metallurgy },
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 15) },
            tier: 3, line: 2),

        new(TechnologyId.WarAcademy,
            "tech_war_academy_name", "tech_war_academy_desc",
            cost: 6500,
            prerequisites: new[] { TechnologyId.MilitaryMastery, TechnologyId.MilitaryBuildings },
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.3) },
            tier: 3, line: 7),

        new(TechnologyId.EfficientTrading,
            "tech_efficient_trading_name", "tech_efficient_trading_desc",
            cost: 5000,
            prerequisites: new[] { TechnologyId.SpecializedMarket },
            modifiers: new Modifier[] { new(ECategory.TRADE_BULK_GOLD_BONUS, EType.ADDITIVE, 1) },
            tier: 3, line: 3),

        // === TIER 4 — Premiers croisements ===

        new(TechnologyId.EpicHarvest,
            "tech_epic_harvest_name", "tech_epic_harvest_desc",
            cost: 19350,
            prerequisites: new[] { TechnologyId.MasterHarvest, TechnologyId.Masterwork },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.35) },
            tier: 4, line: 1),

        new(TechnologyId.TradeRoutes,
            "tech_trade_routes_name", "tech_trade_routes_desc",
            cost: 15500,
            prerequisites: new[] { TechnologyId.GrandArchitecture },
            modifiers: new Modifier[] { new(ECategory.TRADE_GOLD_PACKAGES, EType.ADDITIVE, 3) },
            tier: 4, line: 5),

        new(TechnologyId.ImprovedResearch,
            "tech_improved_research_name", "tech_improved_research_desc",
            cost: 17425,
            prerequisites: new[] { TechnologyId.Scholarship, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.2) },
            tier: 4, line: 4),

        new(TechnologyId.MilitarySupremacy,
            "tech_military_supremacy_name", "tech_military_supremacy_desc",
            cost: 19350,
            prerequisites: new[] { TechnologyId.WarAcademy, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 1) },
            tier: 4, line: 7),

        new(TechnologyId.AutomaticMarket,
            "tech_automatic_market_name", "tech_automatic_market_desc",
            cost: 15000,
            prerequisites: new[] { TechnologyId.EfficientTrading },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Market", EType.ADDITIVE, 1) },
            tier: 4, line: 3),

        // === TIER 5 — Grandes convergences ===

        new(TechnologyId.MerchantGuild,
            "tech_merchant_guild_name", "tech_merchant_guild_desc",
            cost: 50370,
            prerequisites: new[] { TechnologyId.EpicHarvest, TechnologyId.TradeRoutes },
            modifiers: new Modifier[] { new(ECategory.TRADE_GOLD_PACKAGES, EType.ADDITIVE, 5) },
            tier: 5, line: 5),

        new(TechnologyId.MasterResearch,
            "tech_master_research_name", "tech_master_research_desc",
            cost: 50370,
            prerequisites: new[] { TechnologyId.ImprovedResearch, TechnologyId.TradeRoutes },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.3) },
            tier: 5, line: 4),

        new(TechnologyId.GloriousEmpire,
            "tech_glorious_empire_name", "tech_glorious_empire_desc",
            cost: 54240,
            prerequisites: new[] { TechnologyId.MilitarySupremacy, TechnologyId.TradeRoutes },
            modifiers: new Modifier[]
            {
                new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 1),
                new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.3),
            },
            tier: 5, line: 7),

        // === TIER 6 ===

        new(TechnologyId.IndustrialAge,
            "tech_industrial_age_name", "tech_industrial_age_desc",
            cost: 135730,
            prerequisites: new[] { TechnologyId.MerchantGuild, TechnologyId.GloriousEmpire },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Sawmill",    EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Brickworks", EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Quarry",     EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Mill",       EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Mine",       EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Forge",      EType.ADDITIVE, 1),
            },
            tier: 6, line: 6),

        new(TechnologyId.Enlightenment,
            "tech_enlightenment_name", "tech_enlightenment_desc",
            cost: 135730,
            prerequisites: new[] { TechnologyId.MasterResearch },
            modifiers: new Modifier[]
            {
                new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.4),
                new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.15),
            },
            tier: 6, line: 4),

        // === TIER 7 ===

        new(TechnologyId.RenaissanceAge,
            "tech_renaissance_age_name", "tech_renaissance_age_desc",
            cost: 345040,
            prerequisites: new[] { TechnologyId.IndustrialAge, TechnologyId.Enlightenment },
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.3),
                new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.2),
            },
            tier: 7, line: 5),

        // === TIER 8 ===

        new(TechnologyId.GoldenEra,
            "tech_golden_era_name", "tech_golden_era_desc",
            cost: 863280,
            prerequisites: new[] { TechnologyId.RenaissanceAge },
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.5),
                new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.5),
                new(ECategory.TRADE_GOLD_PACKAGES, EType.ADDITIVE, 5),
            },
            tier: 8, line: 5),

        // === TIER 9 — Ultime ===

        new(TechnologyId.Utopia,
            "tech_utopia_name", "tech_utopia_desc",
            cost: 2500000,
            prerequisites: new[] { TechnologyId.GoldenEra },
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.5),
                new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.2),
                new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2),
            },
            tier: 9, line: 5),
    };

    public static Technology? Get(TechnologyId id) => All.FirstOrDefault(t => t.Id == id);

    public static int GetDepth(TechnologyId id)
    {
        var tech = Get(id);
        if (tech == null || tech.Prerequisites.Count == 0) return 0;
        return tech.Prerequisites.Max(p => GetDepth(p)) + 1;
    }
}
