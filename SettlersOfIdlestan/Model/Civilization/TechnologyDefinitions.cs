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
            modifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.05) },
            tier: 1, line: 6,
            repeatable: true),

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
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.05) },
            tier: 5, line: 2,
            repeatable: true),

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
            prerequisites: new[] { TechnologyId.ResearchMethods },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.2) },
            tier: 4, line: 6),

        // Prend la place d'AdvancedStrategy dans l'arbre (AdvancedStrategy est repoussée plus loin,
        // voir plus bas). Prérequis désormais Mobile Camp (au lieu d'Advanced Tactics), Mobile Camp
        // ayant été rapproché de la racine de l'arbre.
        new(TechnologyId.RailLogistics,
            "tech_rail_logistics_name", "tech_rail_logistics_desc",
            cost: 380000,
            prerequisites: new[] { TechnologyId.MobileCampConstruction },
            modifiers: new Modifier[]
            {
                new(ECategory.REINFORCEMENT_SPEED, EType.ADDITIVE, 1.0),
                new(ECategory.REINFORCEMENT_RANGE, EType.ADDITIVE, 1),
            },
            tier: 6, line: 8),

        // Prend la place de Rempart de Fer dans l'arbre : dépend désormais de lui (dépendance inversée).
        // Prérequis Surveillance remplacé par Mobile Camp, rapproché de la racine de l'arbre.
        new(TechnologyId.WatchtowerConstruction,
            "tech_watchtower_construction_name", "tech_watchtower_construction_desc",
            cost: 390000,
            prerequisites: new[] { TechnologyId.RempartsDeFer, TechnologyId.MobileCampConstruction },
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
            cost: 120000,
            prerequisites: new[] { TechnologyId.AutomaticMarket, TechnologyId.GrandArchitecture },
            modifiers: new Modifier[]
            {
                new(ECategory.UNLOCK_INTERMEDIATE_TRADE, EType.ADDITIVE, 1),
            },
            tier: 5, line: 5),

        // Un tier au-dessus des Comptoirs Avancés (seul prérequis).
        new(TechnologyId.AdvancedGuilds,
            "tech_advanced_guilds_name", "tech_advanced_guilds_desc",
            cost: 425000,
            prerequisites: new[] { TechnologyId.AdvancedTradingPosts },
            modifiers: new Modifier[]
            {
                new(ECategory.GUILD_AUTOMATION_SPEED_PER_CITY, EType.ADDITIVE, 0.1),
            },
            tier: 6, line: 5),

        // Deux tiers au-dessus des Guildes Avancées (seul prérequis). Débloque le Grand Temple
        // (bâtiment unique, niveau max 1) : automatise la construction des Temples et ajoute
        // PRESTIGE_GAIN_PER_TEMPLE, cumulable avec PRESTIGE_GAIN.
        new(TechnologyId.GrandTempleConstruction,
            "tech_grand_temple_construction_name", "tech_grand_temple_construction_desc",
            cost: 2000000,
            prerequisites: new[] { TechnologyId.AdvancedGuilds },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "GrandTemple", EType.ADDITIVE, 1) },
            tier: 7, line: 5),

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

        // === Branche de la Volcanologie (convergence Sidérurgie × risque volcanique) ===

        new(TechnologyId.Volcanologie,
            "tech_volcanologie_name", "tech_volcanologie_desc",
            cost: 1600000,
            prerequisites: new[] { TechnologyId.CartographieSouterraine },
            modifiers: new Modifier[]
            {
                new(ECategory.HARVEST_PRODUCTION_BONUS, "Mine", EType.ADDITIVE, 15),
                new(ECategory.VOLCANO_DAMAGE_REDUCTION, EType.ADDITIVE, 0.5),
            },
            tier: 7, line: 2),

        // Débloque la Forge Volcanique (bâtiment unique, niveau max 3), constructible uniquement à
        // côté d'un volcan découvert (voir VolcanicForge.HasBuildPrerequisites).
        new(TechnologyId.VolcanicMetallurgy,
            "tech_volcanic_metallurgy_name", "tech_volcanic_metallurgy_desc",
            cost: 25000000,
            prerequisites: new[] { TechnologyId.Volcanologie },
            modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "VolcanicForge", EType.ADDITIVE, 3) },
            tier: 9, line: 2),

        // Rapprochée de la racine de l'arbre (tier -2, coût /16) pour la rendre accessible plus tôt :
        // Watchtower et Rail Logistics dépendent désormais d'elle plutôt que l'inverse. Débloque la
        // construction du Camp Mobile (voir MobileCampController) — l'accès est vérifié directement
        // sur la recherche complétée, plutôt que via un modificateur dédié.
        new(TechnologyId.MobileCampConstruction,
            "tech_mobile_camp_construction_name", "tech_mobile_camp_construction_desc",
            cost: 112500,
            prerequisites: new[] { TechnologyId.AdvancedTactics, TechnologyId.Surveillance },
            modifiers: Array.Empty<Modifier>(),
            tier: 5, line: 8),

        // Baissée d'un tier (coût / 4) ; prérequis désormais Rail Logistics (au lieu du Camp Mobile,
        // rapproché de la racine de l'arbre). Répétable à l'infini (comme MasterHarvest) : +5%
        // UNIT_PRODUCTION_SPEED par complétion, coût doublé à chaque relance.
        new(TechnologyId.EntrainementIntensif,
            "tech_entrainement_intensif_name", "tech_entrainement_intensif_desc",
            cost: 1650000,
            prerequisites: new[] { TechnologyId.RailLogistics },
            modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.05) },
            tier: 7, line: 8,
            repeatable: true),

        // Même tier que WarHerald, une ligne au-dessus. Prérequis : EntrainementIntensif.
        new(TechnologyId.DeploiementRapide,
            "tech_deploiement_rapide_name", "tech_deploiement_rapide_desc",
            cost: 30000000,
            prerequisites: new[] { TechnologyId.EntrainementIntensif },
            modifiers: new Modifier[] { new(ECategory.ATTACK_SPEED, EType.ADDITIVE, 0.25) },
            tier: 9, line: 7),

        // Un tier au-dessus d'EntrainementIntensif, qui devient son seul prérequis — également
        // débloquée par le vertex de prestige Raids (voir PrestigeMapFactory, SiegeTrainingVertex).
        // Raid gratuit sur une ville alliée : redirige tous les flux de renfort vers la cible, sauf
        // les emplacements ayant un flux d'attaque actif (voir RaidEngine.StartWarHeraldRaid).
        new(TechnologyId.WarHerald,
            "tech_war_herald_name", "tech_war_herald_desc",
            cost: 30000000,
            prerequisites: new[] { TechnologyId.EntrainementIntensif },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_WAR_HERALD, EType.ADDITIVE, 1) },
            tier: 9, line: 8),

        // Même ligne que WarHerald (voir Technology.cs), son seul prérequis désormais que Patrol a
        // été supprimée de l'arbre. Raids automatiques sur une civilisation : la cible se met à jour
        // après un raid manuel du joueur ou une attaque subie.
        new(TechnologyId.Vendetta,
            "tech_vendetta_name", "tech_vendetta_desc",
            cost: 100000000,
            prerequisites: new[] { TechnologyId.WarHerald },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_VENDETTA, EType.ADDITIVE, 1) },
            tier: 10, line: 8),

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

        // Suite de Culture Fongique : débloque la récolte de Bois par les Scieries sur les Cavernes
        // aux Champignons adjacentes, à moitié vitesse par rapport aux forêts
        // (voir Sawmill.GetAutomaticHarvestTerrainSpeedMultiplier).
        new(TechnologyId.BoisDeChampignon,
            "tech_bois_de_champignon_name", "tech_bois_de_champignon_desc",
            cost: 380000,
            prerequisites: new[] { TechnologyId.CultureFongique },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SAWMILL_MUSHROOM_HARVEST, EType.ADDITIVE, 1) },
            tier: 6, line: 0),

        new(TechnologyId.CartographieSouterraine,
            "tech_cartographie_souterraine_name", "tech_cartographie_souterraine_desc",
            cost: 350000,
            prerequisites: new[] { TechnologyId.Speleologie },
            modifiers: new Modifier[]
            {
                new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 3),
                new(ECategory.UNLOCK_BUILDERS_GUILD_UNDERWORLD, EType.ADDITIVE, 1),
            },
            tier: 6, line: 1),

        // Les Tours de Guet étant interdites dans l'Outremonde (voir Watchtower.IsAvailableInLayer),
        // cette recherche transpose l'idée de "ralentir les monstres" en effet civ-wide sans bâtiment
        // dédié : allonge l'intervalle entre deux tentatives d'apparition de monstre de bordure
        // (voir AutoExtendController.TrySpawnBorderMonsters).
        new(TechnologyId.VeilleSouterraine,
            "tech_veille_souterraine_name", "tech_veille_souterraine_desc",
            cost: 1600000,
            prerequisites: new[] { TechnologyId.CartographieSouterraine },
            modifiers: new Modifier[] { new(ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL, EType.ADDITIVE, 0.75) },
            tier: 7, line: 0),

        new(TechnologyId.OutilsEnMithril,
            "tech_outils_en_mithril_name", "tech_outils_en_mithril_desc",
            cost: 1600000,
            prerequisites: new[] { TechnologyId.Speleologie, TechnologyId.TemperedSteel },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.25) },
            tier: 7, line: 3),

        // +20% de vitesse de régénération de la défense pour les villes de l'Inframonde
        // (voir MilitaryController.GetDefenseRegenSpeed).
        new(TechnologyId.ProspectionAvancee,
            "tech_prospection_avancee_name", "tech_prospection_avancee_desc",
            cost: 5800000,
            prerequisites: new[] { TechnologyId.CartographieSouterraine, TechnologyId.OutilsEnMithril },
            modifiers: new Modifier[] { new(ECategory.UNDERWORLD_CITY_DEFENSE_REGEN_SPEED, EType.ADDITIVE, 0.2) },
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
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.05) },
            tier: 3, line: 10,
            repeatable: true),

        // Un tier au-dessus des Chroniques du Guet, qui est son seul prérequis.
        new(TechnologyId.Diplomatie,
            "tech_diplomatie_name", "tech_diplomatie_desc",
            cost: 20000,
            prerequisites: new[] { TechnologyId.ChroniquesDuGuet },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_CONTESTED_HARVEST, EType.ADDITIVE, 1) },
            tier: 4, line: 10),

        // Baissée d'un tier (coût / 4) pour la rendre accessible plus tôt.
        new(TechnologyId.SagesseSouterraine,
            "tech_sagesse_souterraine_name", "tech_sagesse_souterraine_desc",
            cost: 25000000,
            prerequisites: new[] { TechnologyId.VeilleSouterraine },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.10) },
            tier: 9, line: 0,
            repeatable: true),

        // Racine de la branche des Abysses : débloquée directement par le vertex de prestige
        // Brèche Abyssale, sans prérequis.
        new(TechnologyId.VoidWalking,
            "tech_void_walking_name", "tech_void_walking_desc",
            cost: 6500000,
            prerequisites: Array.Empty<TechnologyId>(),
            modifiers: new Modifier[] { new(ECategory.UNLOCK_VOID_ROUTES, EType.ADDITIVE, 1) },
            tier: 8, line: 5),

        // === Branche des Abysses (débloquée par le vertex de prestige Brèche Abyssale, via VoidWalking) ===

        // Reprend la place de l'ancienne Étude des Abysses (supprimée) : tier -1 et coût / 4 par
        // rapport à son ancienne position (tier 11, 420M), prérequis désormais Void Walking. Baissée
        // d'un tier supplémentaire (coût / 4) pour la rendre accessible plus tôt.
        // Permet de rechercher les Os Divins qui apparaissent sur les îles des Abysses générées
        // après la première (voir DivineBones/AbyssIslandGenerator).
        new(TechnologyId.VoidCompass,
            "tech_void_compass_name", "tech_void_compass_desc",
            cost: 26250000,
            prerequisites: new[] { TechnologyId.VoidWalking },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_DIVINE_BONES, EType.ADDITIVE, 1) },
            tier: 9, line: 4),

        new(TechnologyId.Demonologie,
            "tech_demonologie_name", "tech_demonologie_desc",
            cost: 26000000,
            prerequisites: new[] { TechnologyId.VoidWalking },
            modifiers: new Modifier[] { new(ECategory.UNDERWORLD_MONSTER_SPAWN_INTERVAL, EType.ADDITIVE, 0.5) },
            tier: 9, line: 5),

        // La corruption compte 1 niveau de moins pour le malus de récolte (plancher : niveau 1,
        // voir Corruption.GetHarvestTimeMultiplier — seul le Dominion peut réellement la contrer).
        new(TechnologyId.ResistanceALaCorruption,
            "tech_resistance_a_la_corruption_name", "tech_resistance_a_la_corruption_desc",
            cost: 105000000,
            prerequisites: new[] { TechnologyId.Demonologie },
            modifiers: new Modifier[] { new(ECategory.CORRUPTION_LEVEL_REDUCTION, EType.ADDITIVE, 1) },
            tier: 10, line: 5),

        // Baissée d'un tier (coût / 4).
        new(TechnologyId.PacteAbyssal,
            "tech_pacte_abyssal_name", "tech_pacte_abyssal_desc",
            cost: 105000000,
            prerequisites: new[] { TechnologyId.Demonologie },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.25) },
            tier: 10, line: 6),

        // Baissée de 2 tiers (coût / 16). Ne dépend plus que de PacteAbyssal, Secrets of the Rift
        // (SecretsDeLaFaille) ayant été supprimée.
        new(TechnologyId.TheologieDeLAscension,
            "tech_theologie_de_l_ascension_name", "tech_theologie_de_l_ascension_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.PacteAbyssal },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.5) },
            tier: 11, line: 6),

        // === Capstones des branches existantes (tiers 10-11 depuis la baisse de 2 tiers) ===

        // Débloquée par le vertex de prestige Brèche du Vide (porte de l'Acier des Abysses).
        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.AcierAbyssal,
            "tech_acier_abyssal_name", "tech_acier_abyssal_desc",
            cost: 106250000,
            prerequisites: new[] { TechnologyId.SteelArmor, TechnologyId.VolcanicMetallurgy },
            modifiers: new Modifier[]
            {
                new(ECategory.FORGE_DOUBLE_HARVEST_BONUS, EType.ADDITIVE, 25),
                new(ECategory.BUILDING_PRODUCTION, "Smelter", EType.ADDITIVE, 1),
            },
            tier: 10, line: 3),

        // Débloquée par le vertex de prestige Sanctuaire Perdu (porte de la Magie des Abysses).
        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.MagieDuVide,
            "tech_magie_du_vide_name", "tech_magie_du_vide_desc",
            cost: 106250000,
            prerequisites: new[] { TechnologyId.MartialBlessingRitual, TechnologyId.ArcaneShieldRitual },
            modifiers: new Modifier[]
            {
                new(ECategory.RITUAL_TOTAL_POWER, EType.ADDITIVE, 0.25),
                new(ECategory.SPELL_COST_REDUCTION, EType.ADDITIVE, 0.1),
            },
            tier: 10, line: 10),

        // Suite directe de la Magie du Vide : débloque le sort Pont du Vide, qui bâtit d'un coup les
        // trois routes autour d'un vertex bordé de Vide. Contrairement aux routes du Vide classiques,
        // il ne consomme pas de points de recherche — le prix est en cristaux, et il double à chaque
        // lancement du run (voir SpellDefinition.CostDoublesPerCast).
        new(TechnologyId.PontDuVide,
            "tech_pont_du_vide_name", "tech_pont_du_vide_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.MagieDuVide },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_SPELL, "VoidBridge", EType.ADDITIVE, 1) },
            tier: 11, line: 10),

        // Seconde suite de la Magie du Vide : débloque l'Observatoire, monument bâti sur une Montagne
        // de la surface. Chaque niveau abaisse le multiplicateur exponentiel du coût en points de
        // recherche des routes du Vide, de ×4 à ×3 une fois l'Observatoire complet (voir Observatory).
        new(TechnologyId.CartesDesEtoiles,
            "tech_cartes_des_etoiles_name", "tech_cartes_des_etoiles_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.MagieDuVide },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_OBSERVATORY, EType.ADDITIVE, 1) },
            tier: 11, line: 11),

        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.CoeurDeLaTerre,
            "tech_coeur_de_la_terre_name", "tech_coeur_de_la_terre_desc",
            cost: 106250000,
            prerequisites: new[] { TechnologyId.ProspectionAvancee },
            modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, "Mine", EType.ADDITIVE, 0.5) },
            tier: 10, line: 1),

        // Baissée d'un tier (coût / 4).
        new(TechnologyId.Omniscience,
            "tech_omniscience_name", "tech_omniscience_desc",
            cost: 26562500,
            prerequisites: new[] { TechnologyId.MasterResearch },
            modifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.5) },
            tier: 9, line: 6),

        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.LegionEternelle,
            "tech_legion_eternelle_name", "tech_legion_eternelle_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.Vendetta },
            modifiers: new Modifier[]
            {
                new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.25),
                new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 10),
            },
            tier: 11, line: 8),

        // === Suite de la ligne du Vide (VoidWalking → VoidCompass, tiers 10-11 depuis la baisse de 2 tiers) ===
        // Accélère la boucle d'Ascension : Purification des Os Divins moins chère, puis routes du
        // Vide moins coûteuses pour atteindre les îles suivantes des Abysses.

        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.ReliquaireSacre,
            "tech_reliquaire_sacre_name", "tech_reliquaire_sacre_desc",
            cost: 106250000,
            prerequisites: new[] { TechnologyId.VoidCompass },
            modifiers: new Modifier[]
            {
                new(ECategory.DIVINE_BONES_COST_REDUCTION, EType.ADDITIVE, 0.15),
                new(ECategory.DIVINE_ESSENCE_KEPT_ON_PRESTIGE, EType.ADDITIVE, 1),
            },
            tier: 10, line: 4),

        // Un tier au-dessus de Reliquaire Sacré, dont elle dépend (avec Acier Abyssal) : conserve une
        // seconde essence divine lors du prestige.
        new(TechnologyId.ReliquaireRenforce,
            "tech_reliquaire_renforce_name", "tech_reliquaire_renforce_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.ReliquaireSacre, TechnologyId.AcierAbyssal },
            modifiers: new Modifier[] { new(ECategory.DIVINE_ESSENCE_KEPT_ON_PRESTIGE, EType.ADDITIVE, 1) },
            tier: 11, line: 3),

        // Suite du Reliquaire Sacré : débloque la Nécropole, bâtie sur des Os Divins non purifiés
        // qu'elle consomme (l'essence divine qu'ils auraient donnée est sacrifiée). Chaque niveau
        // augmente de 15% les points divins gagnés à l'Ascension (voir Necropolis).
        new(TechnologyId.NecropoleDivine,
            "tech_necropole_divine_name", "tech_necropole_divine_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.ReliquaireSacre },
            modifiers: new Modifier[] { new(ECategory.UNLOCK_NECROPOLIS, EType.ADDITIVE, 1) },
            tier: 11, line: 5),

        // Les routes du Vide déjà bâties ne comptent que pour moitié dans le coût exponentiel de la
        // suivante : 1M × 4^n devient 1M × 4^(n/2) (voir RoadController.GetVoidRouteResearchCost).
        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.CartographieDuVide,
            "tech_cartographie_du_vide_name", "tech_cartographie_du_vide_desc",
            cost: 418750000,
            prerequisites: new[] { TechnologyId.ReliquaireSacre },
            modifiers: new Modifier[] { new(ECategory.VOID_ROUTE_COST_REDUCTION, EType.ADDITIVE, 1) },
            tier: 11, line: 4),

        // === Branche de la Théocratie (tiers 12-13 depuis la baisse de 2 tiers) ===
        // Recherches du Dominion : cachées tant que le pouvoir divin Foi n'est pas débloqué
        // (requiresDominionUnlock), donc accessibles uniquement après la première Ascension.

        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.DogmeDeLEmprise,
            "tech_dogme_de_l_emprise_name", "tech_dogme_de_l_emprise_desc",
            cost: 1687500000,
            prerequisites: new[] { TechnologyId.TheologieDeLAscension },
            modifiers: new Modifier[] { new(ECategory.TEMPLE_DOMINION_CAP, EType.ADDITIVE, 1) },
            tier: 12, line: 5,
            requiresDominionUnlock: true),

        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.CommunionAbyssale,
            "tech_communion_abyssale_name", "tech_communion_abyssale_desc",
            cost: 1687500000,
            prerequisites: new[] { TechnologyId.TheologieDeLAscension },
            modifiers: new Modifier[] { new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 1.0) },
            tier: 12, line: 6),

        // Le Dominion déborde plus vite que la Corruption : +5 points de % de chance par niveau
        // (10%/niveau → 15%/niveau, voir CorruptionController.ProcessSpread).
        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.Evangelisation,
            "tech_evangelisation_name", "tech_evangelisation_desc",
            cost: 6687500000,
            prerequisites: new[] { TechnologyId.DogmeDeLEmprise },
            modifiers: new Modifier[] { new(ECategory.DOMINION_SPREAD_CHANCE, EType.ADDITIVE, 5) },
            tier: 13, line: 5,
            requiresDominionUnlock: true),

        // 50% de chance que le Dominion sur les hexs d'une ville avec Temple ne perde pas de niveau
        // face à la Corruption (l'annulation reste totale pour la Corruption).
        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.TerreConsacree,
            "tech_terre_consacree_name", "tech_terre_consacree_desc",
            cost: 6687500000,
            prerequisites: new[] { TechnologyId.DogmeDeLEmprise },
            modifiers: new Modifier[] { new(ECategory.TEMPLE_DOMINION_PROTECTION_CHANCE, EType.ADDITIVE, 0.5) },
            tier: 13, line: 6,
            requiresDominionUnlock: true),

        // Chaque Temple ajoute un bonus fixe de défense à sa ville selon son niveau (+1/3/6/10,
        // voir Temple.GetDefenseBonusForLevel et MilitaryController.GetDefenseScore).
        // Baissée de 2 tiers (coût / 16).
        new(TechnologyId.BastionConsacre,
            "tech_bastion_consacre_name", "tech_bastion_consacre_desc",
            cost: 1687500000,
            prerequisites: new[] { TechnologyId.LegionEternelle },
            modifiers: new Modifier[] { new(ECategory.TEMPLE_DEFENSE_BONUS, EType.ADDITIVE, 1) },
            tier: 12, line: 8,
            requiresDominionUnlock: true),

    };

    public static Technology? Get(TechnologyId id) => All.FirstOrDefault(t => t.Id == id);

    public static int GetDepth(TechnologyId id)
    {
        var tech = Get(id);
        if (tech == null || tech.Prerequisites.Count == 0) return 0;
        return tech.Prerequisites.Max(p => GetDepth(p)) + 1;
    }
}
