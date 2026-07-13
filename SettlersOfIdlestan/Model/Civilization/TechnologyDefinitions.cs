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
            cost: 7500,
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
            modifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.15) },
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
            cost: 1400,
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
            cost: 105000,
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
            cost: 20000,
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
            cost: 23000,
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
            cost: 22000,
            prerequisites: new[] { TechnologyId.MilitaryTactics, TechnologyId.RapidConstruction },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_AUTO_REINFORCEMENT, EType.ADDITIVE, 1) },
            tier: 4, line: 8),

        // === TIER 4 — Premiers croisements ===

        new(TechnologyId.AutomaticMarket,
            "tech_automatic_market_name", "tech_automatic_market_desc",
            cost: 20000,
            prerequisites: new[] { TechnologyId.EfficientTrading },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_AUTO_MARKET_TRADE, EType.ADDITIVE, 1) },
            tier: 4, line: 4),

        new(TechnologyId.ImprovedResearch,
            "tech_improved_research_name", "tech_improved_research_desc",
            cost: 22500,
            prerequisites: new[] { TechnologyId.ResearchMethods, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.2) },
            tier: 4, line: 6),

        // Prend la place d'AdvancedStrategy dans l'arbre (AdvancedStrategy est repoussée plus loin,
        // voir plus bas, avec MobileCampConstruction comme prérequis).
        new(TechnologyId.RailLogistics,
            "tech_rail_logistics_name", "tech_rail_logistics_desc",
            cost: 380000,
            prerequisites: new[] { TechnologyId.AdvancedTactics },
            modifiers: new Modifier[]
            {
                new(ECategory.REINFORCEMENT_SPEED, EType.ADDITIVE, 1.0),
                new(ECategory.REINFORCEMENT_RANGE, EType.ADDITIVE, 1),
            },
            tier: 6, line: 8),

        // Prend la place de Rempart de Fer dans l'arbre : dépend désormais de lui (dépendance inversée).
        new(TechnologyId.WatchtowerConstruction,
            "tech_watchtower_construction_name", "tech_watchtower_construction_desc",
            cost: 390000,
            prerequisites: new[] { TechnologyId.RempartsDeFer, TechnologyId.Surveillance },
            modifiers: new Modifier[] { new(ECategory.NEW_CITY_BUILDING, "Watchtower", EType.ADDITIVE, 1) },
            tier: 6, line: 7),

                // === TIER 5 — Grandes convergences ===
        

        // S'insère entre Recherche avancée et Maîtrise de la recherche : même tier que les Tours de
        // Guet, débloque la construction du Grand Phare (monument, Désert/Montagne).
        new(TechnologyId.GreatLighthouseConstruction,
            "tech_great_lighthouse_construction_name", "tech_great_lighthouse_construction_desc",
            cost: 390000,
            prerequisites: new[] { TechnologyId.ImprovedResearch, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_GREAT_LIGHTHOUSE, EType.ADDITIVE, 1) },
            tier: 6, line: 6),

        new(TechnologyId.MasterResearch,
            "tech_master_research_name", "tech_master_research_desc",
            cost: 1750000,
            prerequisites: new[] { TechnologyId.GreatLighthouseConstruction },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.3) },
            tier: 7, line: 6),

        // Débloquée par le vertex de prestige Guilde des Marchands
        new(TechnologyId.AdvancedTradingPosts,
            "tech_advanced_trading_posts_name", "tech_advanced_trading_posts_desc",
            cost: 480000,
            prerequisites: new[] { TechnologyId.AutomaticMarket, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[]
            {
                new(ECategory.UNLOCK_INTERMEDIATE_TRADE, EType.ADDITIVE, 1),
            },
            tier: 6, line: 5),

        // === Branche de l'Acier (débloquée par les vertex de prestige du nord-est) ===

        new(TechnologyId.Siderurgie,
            "tech_siderurgie_name", "tech_siderurgie_desc",
            cost: 80000,
            prerequisites: new[] { TechnologyId.SteelWeapons },
            modifiers: new Modifier[] { new(ECategory.SMELTER_ORE_INPUT, EType.ADDITIVE, -2) },
            tier: 5, line: 3),

        new(TechnologyId.SteelArmor,
            "tech_steel_armor_name", "tech_steel_armor_desc",
            cost: 2000000,
            prerequisites: new[] { TechnologyId.Siderurgie },
            modifiers: new Modifier[]
            {
                new(ECategory.UNLOCK_STEEL_ARMOR, EType.ADDITIVE, 1),
                new(ECategory.BUILDING_MAX_LEVEL, "ArmorSmith", EType.ADDITIVE, 2),
            },
            tier: 7, line: 4),

        new(TechnologyId.TemperedSteel,
            "tech_tempered_steel_name", "tech_tempered_steel_desc",
            cost: 410000,
            prerequisites: new[] { TechnologyId.Siderurgie },
            modifiers: new Modifier[] { new(ECategory.BUILDING_PRODUCTION, "Smelter", EType.ADDITIVE, 1) },
            tier: 6, line: 3),

        // Prend la place de RailLogistics dans l'arbre (voir plus haut). Débloque la construction du
        // Camp Mobile (voir MobileCampController) — l'accès est vérifié directement sur la recherche
        // complétée, comme ProspectionAvancee, plutôt que via un modificateur dédié.
        new(TechnologyId.MobileCampConstruction,
            "tech_mobile_camp_construction_name", "tech_mobile_camp_construction_desc",
            cost: 7200000,
            prerequisites: new[] { TechnologyId.WatchtowerConstruction, TechnologyId.RailLogistics },
            modifiers: Array.Empty<Modifier>(),
            tier: 8, line: 8),

        // Repoussée deux tiers au-dessus du Camp Mobile, qui devient son seul prérequis.
        // Prend la place d'AdvancedStrategy dans l'arbre (voir Technology.cs) : patrouille automatique
        // qui raide via le système de Raid les monstres qui s'approchent d'une ville, plutôt que
        // d'attaquer automatiquement les villes ennemies.
        new(TechnologyId.Patrol,
            "tech_patrol_name", "tech_patrol_desc",
            cost: 100000000,
            prerequisites: new[] { TechnologyId.MobileCampConstruction },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_PATROL, EType.ADDITIVE, 1) },
            tier: 10, line: 8),

        // Un tier au-dessus de Patrol, qui devient son seul prérequis. Raids automatiques sur une
        // civilisation : la cible se met à jour après un raid manuel du joueur ou une attaque subie.
        new(TechnologyId.Vendetta,
            "tech_vendetta_name", "tech_vendetta_desc",
            cost: 400000000,
            prerequisites: new[] { TechnologyId.Patrol },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_VENDETTA, EType.ADDITIVE, 1) },
            tier: 11, line: 8),

        // === Branche de l'Inframonde (débloquée par les vertex de prestige du nord-ouest) ===

        new(TechnologyId.Speleologie,
            "tech_speleologie_name", "tech_speleologie_desc",
            cost: 23000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, "Mine", EType.ADDITIVE, 0.25),
                new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 10),
            },
            tier: 4, line: 1),

        new(TechnologyId.CultureFongique,
            "tech_culture_fongique_name", "tech_culture_fongique_desc",
            cost: 95000,
            prerequisites: new[] { TechnologyId.Speleologie, TechnologyId.Agriculture },
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_SPEED, "MushroomFarm", EType.ADDITIVE, 0.25),
                new(ECategory.HARVEST_PRODUCTION_BONUS, "MushroomFarm", EType.ADDITIVE, 25),
            },
            tier: 5, line: 0),

        new(TechnologyId.CartographieSouterraine,
            "tech_cartographie_souterraine_name", "tech_cartographie_souterraine_desc",
            cost: 350000,
            prerequisites: new[] { TechnologyId.Speleologie },
            modifiers: new Modifier[]
            {
                new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 5),
            },
            tier: 6, line: 1),

        new(TechnologyId.OutilsEnMithril,
            "tech_outils_en_mithril_name", "tech_outils_en_mithril_desc",
            cost: 1600000,
            prerequisites: new[] { TechnologyId.Speleologie, TechnologyId.TemperedSteel },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.25) },
            tier: 7, line: 2),

        // 20% de chance qu'un hexagone Désert de l'Inframonde soit un Filon de Mithril, à la fois
        // pour les hexagones déjà révélés (conversion à la complétion) et pour les futurs
        // (voir ResearchController/AutoExtendController).
        new(TechnologyId.ProspectionAvancee,
            "tech_prospection_avancee_name", "tech_prospection_avancee_desc",
            cost: 5800000,
            prerequisites: new[] { TechnologyId.CartographieSouterraine, TechnologyId.OutilsEnMithril },
            modifiers: Array.Empty<Modifier>(),
            tier: 8, line: 1),

        // === Branche de la Magie (débloquée par le vertex de prestige Secret de la Magie) ===
        // Chaque recherche débloque un rituel à lancer depuis l'écran Rituels.

        new(TechnologyId.MagicInitiation,
            "tech_magic_initiation_name", "tech_magic_initiation_desc",
            cost: 80000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "Growth", EType.ADDITIVE, 1) },
            tier: 5, line: 10),

        new(TechnologyId.ArdentForgeRitual,
            "tech_ardent_forge_ritual_name", "tech_ardent_forge_ritual_desc",
            cost: 320000,
            prerequisites: new[] { TechnologyId.MagicInitiation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "ArdentForge", EType.ADDITIVE, 1) },
            tier: 6, line: 9),

        new(TechnologyId.ClairvoyanceRitual,
            "tech_clairvoyance_ritual_name", "tech_clairvoyance_ritual_desc",
            cost: 320000,
            prerequisites: new[] { TechnologyId.MagicInitiation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "Clairvoyance", EType.ADDITIVE, 1) },
            tier: 6, line: 10),

        new(TechnologyId.MartialBlessingRitual,
            "tech_martial_blessing_ritual_name", "tech_martial_blessing_ritual_desc",
            cost: 1400000,
            prerequisites: new[] { TechnologyId.ArdentForgeRitual },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "MartialBlessing", EType.ADDITIVE, 1) },
            tier: 7, line: 9),

        new(TechnologyId.ArcaneShieldRitual,
            "tech_arcane_shield_ritual_name", "tech_arcane_shield_ritual_desc",
            cost: 5200000,
            prerequisites: new[] { TechnologyId.ClairvoyanceRitual },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "ArcaneShield", EType.ADDITIVE, 1) },
            tier: 8, line: 10),

        new(TechnologyId.DeepLightRitual,
            "tech_deep_light_ritual_name", "tech_deep_light_ritual_desc",
            cost: 24000000,
            prerequisites: new[] { TechnologyId.MartialBlessingRitual, TechnologyId.Speleologie },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_RITUAL, "DeepLight", EType.ADDITIVE, 1) },
            tier: 9, line: 9),

        // === Branche des Sorts Instantanés (débloquée par le vertex de prestige Invocations) ===
        // Chaque recherche débloque un sort à lancer depuis l'écran Rituels.

        new(TechnologyId.Invocation,
            "tech_invocation_name", "tech_invocation_desc",
            cost: 80000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "Abundance", EType.ADDITIVE, 1) },
            tier: 5, line: 11),

        new(TechnologyId.TroopSummoning,
            "tech_troop_summoning_name", "tech_troop_summoning_desc",
            cost: 1300000,
            prerequisites: new[] { TechnologyId.Invocation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "SummonTroops", EType.ADDITIVE, 1) },
            tier: 7, line: 11),

        new(TechnologyId.ArcaneEdification,
            "tech_arcane_edification_name", "tech_arcane_edification_desc",
            cost: 6300000,
            prerequisites: new[] { TechnologyId.Invocation },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "ArcaneEdification", EType.ADDITIVE, 1) },
            tier: 8, line: 12),

        // === Amélioration de la Palissade (convergence Fortifications × métallurgie) ===

        // Prend la place de la Tour de Guet dans l'arbre : plus de prérequis Sidérurgie (Steelmaking).
        new(TechnologyId.RempartsDeFer,
            "tech_remparts_de_fer_name", "tech_remparts_de_fer_desc",
            cost: 115000,
            prerequisites: new[] { TechnologyId.RapidConstruction },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Palisade", EType.ADDITIVE, 1) },
            tier: 5, line: 7),

        new(TechnologyId.RempartsDeMithril,
            "tech_remparts_de_mithril_name", "tech_remparts_de_mithril_desc",
            cost: 6600000,
            prerequisites: new[] { TechnologyId.WatchtowerConstruction, TechnologyId.OutilsEnMithril },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Palisade", EType.ADDITIVE, 1) },
            tier: 8, line: 7),

        // === Recherches de bonus de prestige (capstones de branches existantes) ===

        // Bifurque une ligne plus bas que la Tour de Guet (Scouting).
        new(TechnologyId.ChroniquesDuGuet,
            "tech_chroniques_du_guet_name", "tech_chroniques_du_guet_desc",
            cost: 7800,
            prerequisites: new[] { TechnologyId.Scouting },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.15) },
            tier: 3, line: 10),

        // Un tier au-dessus des Chroniques du Guet, qui est son seul prérequis.
        new(TechnologyId.Diplomatie,
            "tech_diplomatie_name", "tech_diplomatie_desc",
            cost: 20000,
            prerequisites: new[] { TechnologyId.ChroniquesDuGuet },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_CONTESTED_HARVEST, EType.ADDITIVE, 1) },
            tier: 4, line: 10),

        new(TechnologyId.RenommeeCommerciale,
            "tech_renommee_commerciale_name", "tech_renommee_commerciale_desc",
            cost: 125000,
            prerequisites: new[] { TechnologyId.AutomaticMarket },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.2) },
            tier: 5, line: 4),

        new(TechnologyId.SagesseSouterraine,
            "tech_sagesse_souterraine_name", "tech_sagesse_souterraine_desc",
            cost: 6000000,
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
