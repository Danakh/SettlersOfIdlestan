using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public static class PrestigeMapFactory
{
    public static PrestigeMap CreateDefault()
    {
        int Cost(Vertex v) => PrestigeMap.DefaultCost(v.EdgeDistanceTo(PrestigeMap.CentralVertex));

        var vertices = new PrestigeVertex[]
        {
            new(
                PrestigeMap.CentralVertex,
                "prestige_vertex_central",
                cost: Cost(PrestigeMap.CentralVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 3),
                    new(ECategory.UNLOCK_RESEARCH_SYSTEM, EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.SeaportMarketVertex,
                "prestige_vertex_seaport_market",
                cost: Cost(PrestigeMap.SeaportMarketVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.STARTING_CITY_BUILDING, "Seaport", EType.ADDITIVE, 1),
                    new(ECategory.STARTING_CITY_BUILDING, "Market",  EType.ADDITIVE, 1),
                    new(ECategory.PRESTIGE_GAIN, EType.ADDITIVE, 0.1),
                }
            ),
            new(
                PrestigeMap.WatchtowerVertex,
                "prestige_vertex_watchtower",
                cost: Cost(PrestigeMap.WatchtowerVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Watchtower", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "Scouting", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.MaritimeRoutesVertex,
                "prestige_vertex_maritime_routes",
                cost: Cost(PrestigeMap.MaritimeRoutesVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_MARITIME_ROUTES, EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.TraderGuildVertex,
                "prestige_vertex_traders_guild",
                cost: Cost(PrestigeMap.TraderGuildVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "TraderGuild", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "AdvancedTradingPosts", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.WarehouseNewCitiesVertex,
                "prestige_vertex_warehouse_new_cities",
                cost: Cost(PrestigeMap.WarehouseNewCitiesVertex),
                modifiers: new Modifier[] { new(ECategory.NEW_CITY_BUILDING, "Warehouse", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.LaboratoryVertex,
                "prestige_vertex_laboratory",
                cost: Cost(PrestigeMap.LaboratoryVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "GlassWorks", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESOURCE, "Glass", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.BarracksVertex,
                "prestige_vertex_barracks",
                cost: Cost(PrestigeMap.BarracksVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2),
                }
            ),
            new(
                PrestigeMap.FortifiedOutpostVertex,
                "prestige_vertex_fortified_outpost",
                cost: Cost(PrestigeMap.FortifiedOutpostVertex),
                modifiers: new Modifier[] { new(ECategory.NEW_CITY_BUILDING, "Palisade", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.HarvestGuildVertex,
                "prestige_vertex_harvesters_guild",
                cost: Cost(PrestigeMap.HarvestGuildVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "HarvestersGuild", EType.ADDITIVE, 4) }
            ),
            new(
                PrestigeMap.ArtisansGuildVertex,
                "prestige_vertex_artisans_guild",
                cost: Cost(PrestigeMap.ArtisansGuildVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "ArtisansGuild", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.AppliedResearchVertex,
                "prestige_vertex_applied_research",
                cost: Cost(PrestigeMap.AppliedResearchVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_RESEARCH, "Artisanat", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.ConscriptionVertex,
                "prestige_vertex_conscription",
                cost: Cost(PrestigeMap.ConscriptionVertex),
                modifiers: new Modifier[] { new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 5) }
            ),
            new(
                PrestigeMap.MilitaryStrategyVertex,
                "prestige_vertex_military_strategy",
                cost: Cost(PrestigeMap.MilitaryStrategyVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RESEARCH, "MilitaryDiscipline", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.GarrisonVertex,
                "prestige_vertex_garrison",
                cost: Cost(PrestigeMap.GarrisonVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Garrison", EType.ADDITIVE, 4) }
            ),
            new(
                PrestigeMap.KnowledgeMasteryVertex,
                "prestige_vertex_knowledge_mastery",
                cost: Cost(PrestigeMap.KnowledgeMasteryVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RESEARCH, "Archivage", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH_QUEUE, EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.AcademyVertex,
                "prestige_vertex_academy",
                cost: Cost(PrestigeMap.AcademyVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Academy", EType.ADDITIVE, 4) }
            ),
            new(
                PrestigeMap.SteelSecretVertex,
                "prestige_vertex_steel_secret",
                cost: Cost(PrestigeMap.SteelSecretVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Smelter", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESOURCE, "Steel", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "SteelWeapons", EType.ADDITIVE, 1),
                }
            ),
            // ── Branche de l'Acier (nord-est) ─────────────────────────────────
            new(
                PrestigeMap.BlastFurnaceVertex,
                "prestige_vertex_blast_furnace",
                cost: Cost(PrestigeMap.BlastFurnaceVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Smelter", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "BlastFurnace", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "Siderurgie", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.MilitaryEngineeringVertex,
                "prestige_vertex_military_engineering",
                cost: Cost(PrestigeMap.MilitaryEngineeringVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Arsenal", EType.ADDITIVE, 3),
                    new(ECategory.UNLOCK_RESEARCH, "SteelArmor", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.SteelLegionVertex,
                "prestige_vertex_steel_legion",
                cost: Cost(PrestigeMap.SteelLegionVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 10),
                    new(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, 3),
                }
            ),
            new(
                PrestigeMap.ImperialRoadsVertex,
                "prestige_vertex_imperial_roads",
                cost: Cost(PrestigeMap.ImperialRoadsVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.CITY_ATTACK_RANGE, EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "RailLogistics", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.PlanarGateVertex,
                "prestige_vertex_planar_gate",
                cost: Cost(PrestigeMap.PlanarGateVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_ABYSS, EType.ADDITIVE, 1),
                }
            ),
            // ── Branche de l'Inframonde (nord-ouest) ──────────────────────────
            new(
                PrestigeMap.DeepestMineVertex,
                "prestige_vertex_deepest_mine",
                // Porte d'entrée de l'Inframonde — volontairement plus chère que toute la branche de l'Acier
                cost: Cost(PrestigeMap.DeepestMineVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_DEEPEST_MINE, EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "Speleologie", EType.ADDITIVE, 1),
                }
            ),
            // Coûts explicites : la formule par défaut explose à cette distance du centre.
            // Progression : porte 2500 → 10 000 → 25 000/50 000 → Mithril 250 000 (un des derniers).
            new(
                PrestigeMap.MushroomCultureVertex,
                "prestige_vertex_mushroom_culture",
                cost: Cost(PrestigeMap.MushroomCultureVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "MushroomFarm", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "CultureFongique", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.MithrilMineVertex,
                "prestige_vertex_mithril",
                cost: Cost(PrestigeMap.MithrilMineVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RESOURCE, "Mithril", EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "MithrilMine", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "OutilsEnMithril", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.DeepProspectorsVertex,
                "prestige_vertex_deep_prospectors",
                cost: Cost(PrestigeMap.DeepProspectorsVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10),
                    new(ECategory.BUILDING_MAX_LEVEL, "Mine", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.TreasureHuntersVertex,
                "prestige_vertex_treasure_hunters",
                cost: Cost(PrestigeMap.TreasureHuntersVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 5),
                }
            ),
            new(
                PrestigeMap.AbyssRiftVertex,
                "prestige_vertex_abyss_rift",
                cost: Cost(PrestigeMap.AbyssRiftVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_ABYSS, EType.ADDITIVE, 1),
                }
            ),
            // ── Branche de la Magie (sud) ──────────────────────────────────────
            // Une entrée bon marché donne accès aux cristaux en surface (Cercles de
            // Fées) avant ou en même temps que l'Inframonde ; l'autre (Achat Automatique)
            // est une commodité économique. La porte (Rituels) coûte autant que la porte
            // de l'Inframonde (2500).
            // Progression : 400/1000 → porte 2500 → 10 000 → 25 000/50 000 → Archimage 250 000.
            new(
                PrestigeMap.AlchimistHutVertex,
                "prestige_vertex_alchimisthut",
                cost: Cost(PrestigeMap.AlchimistHutVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.MAGIC_FEATURE_COUNT, "FairyCircle", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESOURCE, "Crystal", EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "AlchimistHut", EType.ADDITIVE, 3),
                    new(ECategory.UNLOCK_HEALING_POTION, EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.AutoBuyVertex,
                "prestige_vertex_autobuy",
                cost: Cost(PrestigeMap.AutoBuyVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_AUTO_BUY_TRADE, EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.RitualsVertex,
                "prestige_vertex_rituals",
                // Porte d'entrée de la Magie — au moins aussi chère que la porte de l'Inframonde
                cost: Cost(PrestigeMap.RitualsVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_MAGIC, EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "MageTower", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "MagicInitiation", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.InvocationsVertex,
                "prestige_vertex_invocations",
                cost: Cost(PrestigeMap.InvocationsVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_INVOCATIONS, EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "MageTower", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "Invocation", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.InvocationCircleVertex,
                "prestige_vertex_invocation_circle",
                cost: Cost(PrestigeMap.InvocationCircleVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.SPELL_COST_REDUCTION, EType.ADDITIVE, 0.25),
                }
            ),
            new(
                PrestigeMap.DarkEclipseRitualVertex,
                "prestige_vertex_dark_eclipse_ritual",
                cost: Cost(PrestigeMap.DarkEclipseRitualVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_ABYSS, EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.ArchmageVertex,
                "prestige_vertex_archmage",
                cost: Cost(PrestigeMap.ArchmageVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.RITUAL_MAX_COUNT, EType.ADDITIVE, 1),
                    new(ECategory.RITUAL_TOTAL_POWER, EType.ADDITIVE, 0.25),
                    new(ECategory.PASSIVE_RESOURCE_GENERATION, "Crystal", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.ReinforcedPalisadeVertex,
                "prestige_vertex_reinforced_palisade",
                cost: Cost(PrestigeMap.ReinforcedPalisadeVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Palisade", EType.ADDITIVE, 2) }
            ),
            new(
                PrestigeMap.RapidDeploymentVertex,
                "prestige_vertex_rapid_deployment",
                cost: Cost(PrestigeMap.RapidDeploymentVertex),
                modifiers: new Modifier[] { new(ECategory.ATTACK_SPEED, EType.ADDITIVE, 1.0) }
            ),
            new(
                PrestigeMap.BarbacaneVertex,
                "prestige_vertex_barbacane",
                cost: Cost(PrestigeMap.BarbacaneVertex),
                modifiers: new Modifier[] { new(ECategory.CITY_DEFENSE_PROTECTS_SOLDIERS, EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.SiegeTrainingVertex,
                "prestige_vertex_raid_action",
                cost: Cost(PrestigeMap.SiegeTrainingVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RAID, EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESEARCH, "WarHerald", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.OuterScienceVertex,
                "prestige_vertex_war_room",
                cost: Cost(PrestigeMap.OuterScienceVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "WarRoom", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.WarRoomVertex,
                "prestige_vertex_research_institute",
                cost: Cost(PrestigeMap.WarRoomVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "Academy", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.ImperialPortVertex,
                "prestige_vertex_imperial_port",
                cost: Cost(PrestigeMap.ImperialPortVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_SEAPORT_AUTOMATION, EType.ADDITIVE, 1),
                    new(ECategory.PRESTIGE_GAIN_PER_SEAPORT_LEVEL4, EType.ADDITIVE, 0.05),
                }
            ),
            new(
                PrestigeMap.MasterArtisansVertex,
                "prestige_vertex_master_artisans",
                cost: Cost(PrestigeMap.MasterArtisansVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.WONDER_COST_REDUCTION, EType.ADDITIVE, 0.1),
                    new(ECategory.INVESTMENT_SPEED_HIGH_STOCK_BONUS, EType.ADDITIVE, 1.0),
                }
            ),
            new(
                PrestigeMap.StrategicRationsVertex,
                "prestige_vertex_strategic_rations",
                cost: Cost(PrestigeMap.StrategicRationsVertex),
                modifiers: new Modifier[] { new(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, 10) }
            ),
            new(
                PrestigeMap.OuterHarborVertex,
                "prestige_vertex_relocation",
                cost: Cost(PrestigeMap.OuterHarborVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_RELOCATION, EType.ADDITIVE, 1) }
            ),
            // ── Branche des Abysses ──────────────────────────────────────────────
            // Chaque porte mêle 1-2 vertex clés (déblocage de recherche, pouvoir fort) et des
            // petits bonus thématiques.
            // Porte de l'Acier — autour de Foi Protectrice / PlanarRuins
            new(
                PrestigeMap.VoidBreachVertex,
                "prestige_vertex_void_breach",
                cost: Cost(PrestigeMap.VoidBreachVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_RESEARCH, "AcierAbyssal", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.DeepVoidVertex,
                "prestige_vertex_deep_void",
                cost: Cost(PrestigeMap.DeepVoidVertex),
                modifiers: new Modifier[] { new(ECategory.SMELTER_SPEED, EType.ADDITIVE, 0.15) }
            ),
            new(
                PrestigeMap.ShatteredRealmVertex,
                "prestige_vertex_shattered_realm",
                cost: Cost(PrestigeMap.ShatteredRealmVertex),
                modifiers: new Modifier[] { new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 5) }
            ),
            new(
                PrestigeMap.FractureLineVertex,
                "prestige_vertex_fracture_line",
                cost: Cost(PrestigeMap.FractureLineVertex),
                modifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 3) }
            ),
            new(
                PrestigeMap.OuterRuinsVertex,
                "prestige_vertex_outer_ruins",
                cost: Cost(PrestigeMap.OuterRuinsVertex),
                modifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.CollapsedWallVertex,
                "prestige_vertex_collapsed_wall",
                cost: Cost(PrestigeMap.CollapsedWallVertex),
                modifiers: new Modifier[] { new(ECategory.SOLDIER_FOOD_FREE_PER_CITY, EType.ADDITIVE, 5) }
            ),
            new(
                PrestigeMap.RuinedBastionVertex,
                "prestige_vertex_ruined_bastion",
                cost: Cost(PrestigeMap.RuinedBastionVertex),
                modifiers: new Modifier[] { new(ECategory.ATTACK_SPEED, EType.ADDITIVE, 0.5) }
            ),
            // Porte de l'Inframonde — autour de AbyssDepths / AbyssChasm. Les vertex adjacents à
            // l'hex Dominion (AbyssDepths) alimenteront aussi ses perVertexModifiers une fois le
            // pouvoir divin Foi débloqué.
            new(
                PrestigeMap.OuterDepthsVertex,
                "prestige_vertex_outer_depths",
                cost: Cost(PrestigeMap.OuterDepthsVertex),
                modifiers: new Modifier[] { new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE, 15) }
            ),
            new(
                PrestigeMap.HollowVeinVertex,
                "prestige_vertex_hollow_vein",
                cost: Cost(PrestigeMap.HollowVeinVertex),
                modifiers: new Modifier[] { new(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 5) }
            ),
            new(
                PrestigeMap.AbyssalBreachVertex,
                "prestige_vertex_abyssal_breach",
                cost: Cost(PrestigeMap.AbyssalBreachVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_RESEARCH, "EtudeDesAbysses", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.ForsakenTunnelVertex,
                "prestige_vertex_forsaken_tunnel",
                cost: Cost(PrestigeMap.ForsakenTunnelVertex),
                modifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, "Mine", EType.ADDITIVE, 0.15) }
            ),
            // Seul vertex doté d'un pouvoir : débloque la Guilde des Aventuriers (max niveau 0 → 1).
            new(
                PrestigeMap.AdventurersGuildVertex,
                "prestige_vertex_adventurers_guild",
                cost: Cost(PrestigeMap.AdventurersGuildVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "AdventurersGuild", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.SunkenPathVertex,
                "prestige_vertex_sunken_path",
                cost: Cost(PrestigeMap.SunkenPathVertex),
                modifiers: new Modifier[] { new(ECategory.UNDERWORLD_ROAD_BASE_REDUCTION, EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.BottomlessPitVertex,
                "prestige_vertex_bottomless_pit",
                cost: Cost(PrestigeMap.BottomlessPitVertex),
                modifiers: new Modifier[] { new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 5) }
            ),
            // Porte de la Magie — autour de Ossuary / AbyssVoid
            new(
                PrestigeMap.VoidEdgeVertex,
                "prestige_vertex_void_edge",
                cost: Cost(PrestigeMap.VoidEdgeVertex),
                modifiers: new Modifier[] { new(ECategory.RITUAL_TOTAL_POWER, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.FarShoreVertex,
                "prestige_vertex_far_shore",
                cost: Cost(PrestigeMap.FarShoreVertex),
                modifiers: new Modifier[] { new(ECategory.PASSIVE_RESOURCE_GENERATION, "Crystal", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.LostSanctumVertex,
                "prestige_vertex_lost_sanctum",
                cost: Cost(PrestigeMap.LostSanctumVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_RESEARCH, "MagieDuVide", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.WitheredGroveVertex,
                "prestige_vertex_withered_grove",
                cost: Cost(PrestigeMap.WitheredGroveVertex),
                modifiers: new Modifier[] { new(ECategory.MAGIC_FEATURE_COUNT, "FairyCircle", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.ForgottenAltarVertex,
                "prestige_vertex_forgotten_altar",
                cost: Cost(PrestigeMap.ForgottenAltarVertex),
                modifiers: new Modifier[] { new(ECategory.SPELL_COST_REDUCTION, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.PaleMistVertex,
                "prestige_vertex_pale_mist",
                cost: Cost(PrestigeMap.PaleMistVertex),
                modifiers: new Modifier[] { new(ECategory.RITUAL_UPKEEP_REDUCTION, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.SilentHollowVertex,
                "prestige_vertex_silent_hollow",
                cost: Cost(PrestigeMap.SilentHollowVertex),
                modifiers: new Modifier[] { new(ECategory.RITUAL_MAX_COUNT, EType.ADDITIVE, 1) }
            ),
        };

        IReadOnlyList<Vertex> Adjacent(HexCoord hex)
            => vertices.Select(v => v.Coord).Where(v => v.IsAdjacentTo(hex)).ToList();

        var hexes = new PrestigeHex[]
        {
            // ── Inner hexes (adjacent to Central) ────────────────────────────
            new(
                PrestigeMap.StartingResourcesCoord,
                "prestige_hex_starting_resources",
                adjacentVertices: Adjacent(PrestigeMap.StartingResourcesCoord),
                perVertexModifiers: Array.Empty<Modifier>(),
                startingResourceBonusPerVertex: 5,
                domain: PrestigeHexDomain.Exploit
            ),
            new(
                PrestigeMap.HarvestSpeedCoord,
                "prestige_hex_harvest_speed",
                adjacentVertices: Adjacent(PrestigeMap.HarvestSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Exploit
            ),
            new(
                PrestigeMap.ResearchSpeedCoord,
                "prestige_hex_research_speed",
                adjacentVertices: Adjacent(PrestigeMap.ResearchSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Explore
            ),
            // ── Outer hexes (each adjacent to one outer vertex only) ─────────
            new(
                PrestigeMap.UnitProductionSpeedCoord,
                "prestige_hex_unit_production_speed",
                adjacentVertices: Adjacent(PrestigeMap.UnitProductionSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Exterminate
            ),
            new(
                PrestigeMap.ResearchCostReductionCoord,
                "prestige_hex_research_cost_reduction",
                adjacentVertices: Adjacent(PrestigeMap.ResearchCostReductionCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Explore
            ),
            new(
                PrestigeMap.StorageCapacityCoord,
                "prestige_hex_storage_capacity",
                adjacentVertices: Adjacent(PrestigeMap.StorageCapacityCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.STORAGE_CAPACITY_BASIC,    EType.ADDITIVE, 10),
                    new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE,  5),
                },
                domain: PrestigeHexDomain.Expand
            ),
            new(
                PrestigeMap.GoldTradeCoord,
                "prestige_hex_gold_trade",
                adjacentVertices: Adjacent(PrestigeMap.GoldTradeCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.MARKET_GOLD_SPEED, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Expand
            ),
            new(
                PrestigeMap.ArtisansProductionCoord,
                "prestige_hex_artisans_production",
                adjacentVertices: Adjacent(PrestigeMap.ArtisansProductionCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.HARVEST_SPEED, "Mine",       EType.ADDITIVE, 0.1),
                    new(ECategory.HARVEST_SPEED, "GlassWorks", EType.ADDITIVE, 0.1),
                },
                domain: PrestigeHexDomain.Exploit
            ),
            new(
                PrestigeMap.FortifiedOutpostCoord,
                "prestige_hex_fortifications",
                adjacentVertices: Adjacent(PrestigeMap.FortifiedOutpostCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2) },
                domain: PrestigeHexDomain.Exterminate
            ),
            new(
                PrestigeMap.ExperimentalScienceCoord,
                "prestige_hex_experimental_science",
                adjacentVertices: Adjacent(PrestigeMap.ExperimentalScienceCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.BUILDING_PRODUCTION, "Laboratory", EType.ADDITIVE, 1) },
                domain: PrestigeHexDomain.Explore
            ),
            new(
                PrestigeMap.DefenseRegenCoord,
                "prestige_hex_defense_regen",
                adjacentVertices: Adjacent(PrestigeMap.DefenseRegenCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE_REGEN_SPEED, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Exterminate
            ),
            new(
                PrestigeMap.WarehouseMaxLevelCoord,
                "prestige_hex_warehouse_max_level",
                adjacentVertices: Adjacent(PrestigeMap.WarehouseMaxLevelCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Warehouse", EType.ADDITIVE, 1) },
                domain: PrestigeHexDomain.Expand
            ),
            // ── Hex Forges (branche de l'Acier) ───────────────────────────────
            new(
                PrestigeMap.SteelworksCoord,
                "prestige_hex_steelworks",
                adjacentVertices: Adjacent(PrestigeMap.SteelworksCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.SMELTER_SPEED, EType.ADDITIVE, 0.15) },
                domain: PrestigeHexDomain.Exterminate
            ),
            // ── Hexes de l'Inframonde (branche nord-ouest) ────────────────────
            new(
                PrestigeMap.UnderworldCoord,
                "prestige_hex_underworld",
                adjacentVertices: Adjacent(PrestigeMap.UnderworldCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.UNDERWORLD_ROAD_BASE_REDUCTION, EType.ADDITIVE, 1),
                },
                domain: PrestigeHexDomain.Explore
            ),
            // ── Hex Lignes Telluriques (branche de la Magie) ──────────────────
            new(
                PrestigeMap.LeyLinesCoord,
                "prestige_hex_ley_lines",
                adjacentVertices: Adjacent(PrestigeMap.LeyLinesCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RITUAL_TOTAL_POWER, EType.ADDITIVE, 0.05) },
                domain: PrestigeHexDomain.Explore
            ),
            // ── Hexes des Abysses — entourent les trois vertex de déblocage de la Faille des
            // Abysses (Porte Planaire / Faille des Abysses / Rituel de l'Éclipse Noire).
            // Foi Protectrice — verrouillée en "???" tant que le pouvoir divin Foi n'est pas
            // débloqué (UNLOCK_DOMINION), comme l'hex Dominion. Chaque vertex acheté accélère la
            // régénération de défense de 2% par point de Dominion sur les 3 hexs de la ville.
            new(
                PrestigeMap.ProtectiveFaithCoord,
                "prestige_hex_protective_faith",
                adjacentVertices: Adjacent(PrestigeMap.ProtectiveFaithCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.DOMINION_DEFENSE_REGEN_PER_LEVEL, EType.ADDITIVE, 0.02) },
                domain: PrestigeHexDomain.Exterminate,
                requiresDominionUnlock: true
            ),
            new(
                PrestigeMap.PlanarRuinsCoord,
                "prestige_hex_planar_ruins",
                adjacentVertices: Adjacent(PrestigeMap.PlanarRuinsCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2) },
                domain: PrestigeHexDomain.Exterminate
            ),
            // Dominion — à l'ouest de l'Inframonde. Caché derrière la Faille des Abysses (AbyssHexes)
            // ET verrouillé en "???" tant que le pouvoir divin Foi n'est pas débloqué (UNLOCK_DOMINION).
            new(
                PrestigeMap.AbyssDepthsCoord,
                "prestige_hex_dominion",
                adjacentVertices: Adjacent(PrestigeMap.AbyssDepthsCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.DOMINION_HARVEST_SPEED_PER_LEVEL, EType.ADDITIVE, 0.1) },
                domain: PrestigeHexDomain.Exploit,
                requiresDominionUnlock: true
            ),
            new(
                PrestigeMap.AbyssChasmCoord,
                "prestige_hex_abyss_chasm",
                adjacentVertices: Adjacent(PrestigeMap.AbyssChasmCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 2) },
                domain: PrestigeHexDomain.Explore
            ),
            // Ossuaire — hex d'ascension : accélère la boucle os → essence, donc volontairement
            // visible avant la première Ascension (pas de requiresDominionUnlock).
            new(
                PrestigeMap.OssuaryCoord,
                "prestige_hex_ossuary",
                adjacentVertices: Adjacent(PrestigeMap.OssuaryCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.DIVINE_BONES_COST_REDUCTION, EType.ADDITIVE, 0.05) },
                domain: PrestigeHexDomain.Explore
            ),
            new(
                PrestigeMap.AbyssVoidCoord,
                "prestige_hex_abyss_void",
                adjacentVertices: Adjacent(PrestigeMap.AbyssVoidCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.PASSIVE_RESOURCE_GENERATION, "Crystal", EType.ADDITIVE, 1) },
                domain: PrestigeHexDomain.Exploit
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
