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
            tier: 0, line: 2),

        new(TechnologyId.Artisanat,
            "tech_artisanat_name", "tech_artisanat_desc",
            cost: 120,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 5) },
            tier: 0, line: 3),

        new(TechnologyId.Architecture,
            "tech_architecture_name", "tech_architecture_desc",
            cost: 100,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_WONDERS, EType.ADDITIVE, 1) },
            tier: 0, line: 6),

        new(TechnologyId.Fortifications,
            "tech_fortifications_name", "tech_fortifications_desc",
            cost: 100,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Palisade", EType.ADDITIVE, 1) },
            tier: 0, line: 7),

        new(TechnologyId.MilitaryDiscipline,
            "tech_military_discipline_name", "tech_military_discipline_desc",
            cost: 1600,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) },
            tier: 2, line: 8),

        // === TIER 1 ===
        
        new(TechnologyId.Agriculture,
            "tech_agriculture_name", "tech_agriculture_desc",
            cost: 17800,
            prerequisites: new[] { TechnologyId.HarvestTools },
            modifiers: new Modifier[] { new(ECategory.HARVEST_PRODUCTION_BONUS, "Mill", EType.ADDITIVE, 50) },
            tier: 3, line: 0),

        new(TechnologyId.Orpaillage,
            "tech_orpaillage_name", "tech_orpaillage_desc",
            cost: 330,
            prerequisites: new[] { TechnologyId.Artisanat },
            modifiers: new Modifier[] { new(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10) },
            tier: 1, line: 3),

        new(TechnologyId.StorageOptimization,
            "tech_storage_optimization_name", "tech_storage_optimization_desc",
            cost: 300,
            prerequisites: new[] { TechnologyId.Architecture },
            modifiers: new Modifier[] { new(ECategory.STORAGE_CAPACITY_BASIC, EType.ADDITIVE, 20) },
            tier: 1, line: 5),

        new(TechnologyId.Archivage,
            "tech_archivage_name", "tech_archivage_desc",
            cost: 330,
            prerequisites: new[] { TechnologyId.Architecture },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.15) },
            tier: 1, line: 6),

        new(TechnologyId.MilitaryBuildings,
            "tech_military_buildings_name", "tech_military_buildings_desc",
            cost: 1600,
            prerequisites: new[] { TechnologyId.Fortifications, TechnologyId.Architecture },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2) },
            tier: 2, line: 7),

        new(TechnologyId.MilitaryTactics,
            "tech_military_tactics_name", "tech_military_tactics_desc",
            cost: 5500,
            prerequisites: new[] { TechnologyId.MilitaryDiscipline },
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.15) },
            tier: 3, line: 8),

        // Débloquée par le vertex de prestige Tour de Guet
        new(TechnologyId.Scouting,
            "tech_scouting_name", "tech_scouting_desc",
            cost: 950,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1) },
            tier: 2, line: 9),

        // === TIER 2 ===

        new(TechnologyId.RapidConstruction,
            "tech_rapid_construction_name", "tech_rapid_construction_desc",
            cost: 5500,
            prerequisites: new[] { TechnologyId.MilitaryBuildings },
            modifiers: new Modifier[] { new(ECategory.BUILDING_DEFENSE_ON_CONSTRUCT, EType.ADDITIVE, 1) },
            tier: 3, line: 7),

        new(TechnologyId.HarvestTools,
            "tech_harvest_tools_name", "tech_harvest_tools_desc",
            cost: 1665,
            prerequisites: new[] { TechnologyId.HarvestEfficiency },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Sawmill", EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Mill",    EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Quarry",     EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "Brickworks", EType.ADDITIVE, 1),
            },
            tier: 2, line: 2),

        new(TechnologyId.Metallurgy,
            "tech_metallurgy_name", "tech_metallurgy_desc",
            cost: 1665,
            prerequisites: new[] { TechnologyId.Orpaillage },
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 10) },
            tier: 2, line: 3),

        new(TechnologyId.SpecializedMarket,
            "tech_specialized_market_name", "tech_specialized_market_desc",
            cost: 1500,
            prerequisites: new[] { TechnologyId.StorageOptimization },
            modifiers: new Modifier[]
            {
                new(ECategory.BUILDING_MAX_LEVEL, "Market", EType.ADDITIVE, 3),
                new(ECategory.UNLOCK_MARKET_SPECIALIZATION, EType.ADDITIVE, 1),
            },
            tier: 2, line: 4),

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
            cost: 5350,
            prerequisites: new[] { TechnologyId.Archivage },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) },
            tier: 3, line: 6),

        // === TIER 3 ===

        new(TechnologyId.MasterHarvest,
            "tech_master_harvest_name", "tech_master_harvest_desc",
            cost: 46000,
            prerequisites: new[] { TechnologyId.HarvestTools },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.25) },
            tier: 5, line: 2),

        new(TechnologyId.MaitriseDesAlliages,
            "tech_maitrise_des_alliages_name", "tech_maitrise_des_alliages_desc",
            cost: 5560,
            prerequisites: new[] { TechnologyId.Metallurgy },
            modifiers: new Modifier[] { new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 15) },
            tier: 3, line: 3),

        new(TechnologyId.SteelWeapons,
            "tech_steel_weapons_name", "tech_steel_weapons_desc",
            cost: 15000,
            prerequisites: new[] { TechnologyId.MaitriseDesAlliages },
            modifiers: new Modifier[]
            {
                new(ECategory.UNLOCK_STEEL_WEAPONS, EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "WeaponSmith", EType.ADDITIVE, 2),
            },
            tier: 4, line: 3),

        new(TechnologyId.EfficientTrading,
            "tech_efficient_trading_name", "tech_efficient_trading_desc",
            cost: 5000,
            prerequisites: new[] { TechnologyId.SpecializedMarket },
            modifiers: new Modifier[] { new(ECategory.TRADE_BULK_GOLD_BONUS, EType.ADDITIVE, 1) },
            tier: 3, line: 4),

        // Continue la ligne de la Tour de Guet (Scouting) : permet d'attaquer les monstres à distance 2.
        new(TechnologyId.Surveillance,
            "tech_surveillance_name", "tech_surveillance_desc",
            cost: 18000,
            prerequisites: new[] { TechnologyId.Scouting },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RANGED_MONSTER_ATTACK, EType.ADDITIVE, 1) },
            tier: 4, line: 9),

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

        new(TechnologyId.AdvancedTactics,
            "tech_advanced_tactics_name", "tech_advanced_tactics_desc",
            cost: 17000,
            prerequisites: new[] { TechnologyId.MilitaryTactics, TechnologyId.RapidConstruction },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_AUTO_REINFORCEMENT, EType.ADDITIVE, 1) },
            tier: 4, line: 8),

        // === TIER 4 — Premiers croisements ===

        new(TechnologyId.AutomaticMarket,
            "tech_automatic_market_name", "tech_automatic_market_desc",
            cost: 15000,
            prerequisites: new[] { TechnologyId.EfficientTrading },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_AUTO_MARKET_TRADE, EType.ADDITIVE, 1) },
            tier: 4, line: 4),

        new(TechnologyId.ImprovedResearch,
            "tech_improved_research_name", "tech_improved_research_desc",
            cost: 17425,
            prerequisites: new[] { TechnologyId.ResearchMethods, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.2) },
            tier: 4, line: 6),

        new(TechnologyId.AdvancedStrategy,
            "tech_advanced_strategy_name", "tech_advanced_strategy_desc",
            cost: 51000,
            prerequisites: new[] { TechnologyId.AdvancedTactics },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_AUTO_ATTACK, EType.ADDITIVE, 1) },
            tier: 6, line: 8),

        // Prend la place de Rempart de Fer dans l'arbre : dépend désormais de lui (dépendance inversée).
        new(TechnologyId.WatchtowerConstruction,
            "tech_watchtower_construction_name", "tech_watchtower_construction_desc",
            cost: 52000,
            prerequisites: new[] { TechnologyId.RempartsDeFer, TechnologyId.Surveillance },
            modifiers: new Modifier[] { new(ECategory.NEW_CITY_BUILDING, "Watchtower", EType.ADDITIVE, 1) },
            tier: 6, line: 7),

                // === TIER 5 — Grandes convergences ===
        

        new(TechnologyId.MasterResearch,
            "tech_master_research_name", "tech_master_research_desc",
            cost: 140000,
            prerequisites: new[] { TechnologyId.ImprovedResearch },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.3) },
            tier: 7, line: 6),

        // Débloquée par le vertex de prestige Guilde des Marchands
        new(TechnologyId.AdvancedTradingPosts,
            "tech_advanced_trading_posts_name", "tech_advanced_trading_posts_desc",
            cost: 135000,
            prerequisites: new[] { TechnologyId.AutomaticMarket, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[]
            {
                new(ECategory.UNLOCK_INTERMEDIATE_TRADE, EType.ADDITIVE, 1),
            },
            tier: 6, line: 5),

        // === Branche de l'Acier (débloquée par les vertex de prestige du nord-est) ===

        new(TechnologyId.Siderurgie,
            "tech_siderurgie_name", "tech_siderurgie_desc",
            cost: 20000,
            prerequisites: new[] { TechnologyId.SteelWeapons },
            modifiers: new Modifier[] { new(ECategory.SMELTER_ORE_INPUT, EType.ADDITIVE, -2) },
            tier: 5, line: 3),

        new(TechnologyId.SteelArmor,
            "tech_steel_armor_name", "tech_steel_armor_desc",
            cost: 210000,
            prerequisites: new[] { TechnologyId.Siderurgie },
            modifiers: new Modifier[]
            {
                new(ECategory.UNLOCK_STEEL_ARMOR, EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "ArmorSmith", EType.ADDITIVE, 2),
            },
            tier: 7, line: 4),

        new(TechnologyId.TemperedSteel,
            "tech_tempered_steel_name", "tech_tempered_steel_desc",
            cost: 55000,
            prerequisites: new[] { TechnologyId.Siderurgie },
            modifiers: new Modifier[] { new(ECategory.BUILDING_PRODUCTION, "Smelter", EType.ADDITIVE, 1) },
            tier: 6, line: 3),

        new(TechnologyId.RailLogistics,
            "tech_rail_logistics_name", "tech_rail_logistics_desc",
            cost: 230000,
            prerequisites: new[] { TechnologyId.AdvancedStrategy },
            modifiers: new Modifier[]
            {
                new(ECategory.REINFORCEMENT_SPEED, EType.ADDITIVE, 1.0),
                new(ECategory.REINFORCEMENT_RANGE, EType.ADDITIVE, 1),
            },
            tier: 8, line: 8),

        // === Branche de l'Inframonde (débloquée par les vertex de prestige du nord-ouest) ===

        new(TechnologyId.Speleologie,
            "tech_speleologie_name", "tech_speleologie_desc",
            cost: 18000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, "Mine", EType.ADDITIVE, 0.25),
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 10),
            },
            tier: 4, line: 1),

        new(TechnologyId.CultureFongique,
            "tech_culture_fongique_name", "tech_culture_fongique_desc",
            cost: 35000,
            prerequisites: new[] { TechnologyId.Speleologie, TechnologyId.Agriculture },
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, "MushroomFarm", EType.ADDITIVE, 0.25),
                new(ECategory.HARVEST_PRODUCTION_BONUS, "MushroomFarm", EType.ADDITIVE, 25),
            },
            tier: 5, line: 0),

        new(TechnologyId.CartographieSouterraine,
            "tech_cartographie_souterraine_name", "tech_cartographie_souterraine_desc",
            cost: 40000,
            prerequisites: new[] { TechnologyId.Speleologie },
            modifiers: new Modifier[]
            {
                new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 5),
            },
            tier: 6, line: 1),

        new(TechnologyId.OutilsEnMithril,
            "tech_outils_en_mithril_name", "tech_outils_en_mithril_desc",
            cost: 120000,
            prerequisites: new[] { TechnologyId.Speleologie, TechnologyId.TemperedSteel },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.25) },
            tier: 7, line: 2),

        // 20% de chance qu'un hexagone Désert de l'Inframonde soit un Filon de Mithril, à la fois
        // pour les hexagones déjà révélés (conversion à la complétion) et pour les futurs
        // (voir ResearchController/AutoExtendController).
        new(TechnologyId.ProspectionAvancee,
            "tech_prospection_avancee_name", "tech_prospection_avancee_desc",
            cost: 200000,
            prerequisites: new[] { TechnologyId.CartographieSouterraine, TechnologyId.OutilsEnMithril },
            modifiers: Array.Empty<Modifier>(),
            tier: 8, line: 1),

        // === Branche de la Magie (débloquée par le vertex de prestige Secret de la Magie) ===
        // Chaque recherche débloque un rituel à lancer depuis l'écran Rituels.

        new(TechnologyId.MagicInitiation,
            "tech_magic_initiation_name", "tech_magic_initiation_desc",
            cost: 20000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "Growth", EType.ADDITIVE, 1) },
            tier: 5, line: 10),

        new(TechnologyId.ArdentForgeRitual,
            "tech_ardent_forge_ritual_name", "tech_ardent_forge_ritual_desc",
            cost: 35000,
            prerequisites: new[] { TechnologyId.MagicInitiation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "ArdentForge", EType.ADDITIVE, 1) },
            tier: 6, line: 9),

        new(TechnologyId.ClairvoyanceRitual,
            "tech_clairvoyance_ritual_name", "tech_clairvoyance_ritual_desc",
            cost: 35000,
            prerequisites: new[] { TechnologyId.MagicInitiation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "Clairvoyance", EType.ADDITIVE, 1) },
            tier: 6, line: 10),

        new(TechnologyId.MartialBlessingRitual,
            "tech_martial_blessing_ritual_name", "tech_martial_blessing_ritual_desc",
            cost: 70000,
            prerequisites: new[] { TechnologyId.ArdentForgeRitual },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "MartialBlessing", EType.ADDITIVE, 1) },
            tier: 7, line: 9),

        new(TechnologyId.ArcaneShieldRitual,
            "tech_arcane_shield_ritual_name", "tech_arcane_shield_ritual_desc",
            cost: 145000,
            prerequisites: new[] { TechnologyId.ClairvoyanceRitual },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "ArcaneShield", EType.ADDITIVE, 1) },
            tier: 8, line: 10),

        new(TechnologyId.DeepLightRitual,
            "tech_deep_light_ritual_name", "tech_deep_light_ritual_desc",
            cost: 150000,
            prerequisites: new[] { TechnologyId.MartialBlessingRitual, TechnologyId.Speleologie },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "DeepLight", EType.ADDITIVE, 1) },
            tier: 9, line: 9),

        // === Branche des Sorts Instantanés (débloquée par le vertex de prestige Invocations) ===
        // Chaque recherche débloque un sort à lancer depuis l'écran Rituels.

        new(TechnologyId.Invocation,
            "tech_invocation_name", "tech_invocation_desc",
            cost: 20000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "Abundance", EType.ADDITIVE, 1) },
            tier: 5, line: 11),

        new(TechnologyId.TroopSummoning,
            "tech_troop_summoning_name", "tech_troop_summoning_desc",
            cost: 60000,
            prerequisites: new[] { TechnologyId.Invocation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "SummonTroops", EType.ADDITIVE, 1) },
            tier: 7, line: 11),

        new(TechnologyId.ArcaneEdification,
            "tech_arcane_edification_name", "tech_arcane_edification_desc",
            cost: 215000,
            prerequisites: new[] { TechnologyId.Invocation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "ArcaneEdification", EType.ADDITIVE, 1) },
            tier: 8, line: 12),

        // === Amélioration de la Palissade (convergence Fortifications × métallurgie) ===

        // Prend la place de la Tour de Guet dans l'arbre : plus de prérequis Sidérurgie (Steelmaking).
        new(TechnologyId.RempartsDeFer,
            "tech_remparts_de_fer_name", "tech_remparts_de_fer_desc",
            cost: 60000,
            prerequisites: new[] { TechnologyId.RapidConstruction },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Palisade", EType.ADDITIVE, 1) },
            tier: 5, line: 7),

        new(TechnologyId.RempartsDeMithril,
            "tech_remparts_de_mithril_name", "tech_remparts_de_mithril_desc",
            cost: 220000,
            prerequisites: new[] { TechnologyId.WatchtowerConstruction, TechnologyId.OutilsEnMithril },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Palisade", EType.ADDITIVE, 1) },
            tier: 8, line: 7),

        // === Recherches de bonus de prestige (capstones de branches existantes) ===

        // Bifurque une ligne plus bas que la Tour de Guet (Scouting).
        new(TechnologyId.ChroniquesDuGuet,
            "tech_chroniques_du_guet_name", "tech_chroniques_du_guet_desc",
            cost: 18500,
            prerequisites: new[] { TechnologyId.Scouting },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.15) },
            tier: 3, line: 10),

        new(TechnologyId.RenommeeCommerciale,
            "tech_renommee_commerciale_name", "tech_renommee_commerciale_desc",
            cost: 130000,
            prerequisites: new[] { TechnologyId.AutomaticMarket },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.2) },
            tier: 5, line: 4),

        new(TechnologyId.SagesseSouterraine,
            "tech_sagesse_souterraine_name", "tech_sagesse_souterraine_desc",
            cost: 205000,
            prerequisites: new[] { TechnologyId.CultureFongique },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.25) },
            tier: 8, line: 0),

    };

    public static Technology? Get(TechnologyId id) => All.FirstOrDefault(t => t.Id == id);

    public static int GetDepth(TechnologyId id)
    {
        var tech = Get(id);
        if (tech == null || tech.Prerequisites.Count == 0) return 0;
        return tech.Prerequisites.Max(p => GetDepth(p)) + 1;
    }
}
