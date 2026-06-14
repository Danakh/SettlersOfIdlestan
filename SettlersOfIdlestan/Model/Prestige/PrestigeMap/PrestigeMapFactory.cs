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
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Watchtower", EType.ADDITIVE, 1) }
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
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "TraderGuild", EType.ADDITIVE, 1) }
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
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "HarvestersGuild", EType.ADDITIVE, 1) }
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
                PrestigeMap.MilitaryAcademyVertex,
                "prestige_vertex_military_academy",
                cost: Cost(PrestigeMap.MilitaryAcademyVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "MilitaryAcademy", EType.ADDITIVE, 4) }
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
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Academy", EType.ADDITIVE, 1) }
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
                    new(ECategory.STEEL_WEAPONS_SOLDIER_COUNT, EType.ADDITIVE, 3),
                    new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 10),
                }
            ),
            new(
                PrestigeMap.ImperialRoadsVertex,
                "prestige_vertex_imperial_roads",
                cost: Cost(PrestigeMap.ImperialRoadsVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.REINFORCEMENT_RANGE, EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "RailLogistics", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.MasterSmithsVertex,
                "prestige_vertex_master_smiths",
                cost: Cost(PrestigeMap.MasterSmithsVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.PASSIVE_RESOURCE_GENERATION, "Steel", EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "Arsenal", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "Smelter", EType.ADDITIVE, 1),
                }
            ),
            // ── Branche de l'Inframonde (nord-ouest) ──────────────────────────
            new(
                PrestigeMap.DeepestMineVertex,
                "prestige_vertex_deepest_mine",
                // Porte d'entrée de l'Inframonde — volontairement plus chère que toute la branche de l'Acier
                cost: 2500,
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
                cost: 10000,
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "MushroomFarm", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "CultureFongique", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.UnderworldWatchVertex,
                "prestige_vertex_underworld_watch",
                cost: 10000,
                modifiers: new Modifier[]
                {
                    new(ECategory.NEW_CITY_BUILDING, "Watchtower", EType.ADDITIVE, 1),
                    new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2),
                }
            ),
            new(
                PrestigeMap.DeepProspectorsVertex,
                "prestige_vertex_deep_prospectors",
                cost: 25000,
                modifiers: new Modifier[]
                {
                    new(ECategory.MINE_GOLD_CHANCE_PERCENT, EType.ADDITIVE, 10),
                    new(ECategory.BUILDING_MAX_LEVEL, "Mine", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.TreasureHuntersVertex,
                "prestige_vertex_treasure_hunters",
                cost: 50000,
                modifiers: new Modifier[]
                {
                    new(ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT, EType.ADDITIVE, 5),
                    new(ECategory.UNLOCK_RESEARCH, "CartographieSouterraine", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.MithrilVertex,
                "prestige_vertex_mithril",
                cost: 250000,
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RESOURCE, "Mithril", EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "MithrilMine", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "OutilsEnMithril", EType.ADDITIVE, 1),
                }
            ),
            // ── Branche de la Magie (sud) ──────────────────────────────────────
            // Deux entrées bon marché donnent accès aux cristaux en surface (Cercles de
            // Fées, Dolmens) avant ou en même temps que l'Inframonde. La porte (Secret de
            // la Magie) coûte autant que la porte de l'Inframonde (2500).
            // Progression : 400/1000 → porte 2500 → 10 000 → 25 000/50 000 → Archimage 250 000.
            new(
                PrestigeMap.FairyCirclesVertex,
                "prestige_vertex_fairy_circles",
                cost: Cost(PrestigeMap.FairyCirclesVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.MAGIC_FEATURE_COUNT, "FairyCircle", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESOURCE, "Crystal", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.DolmensVertex,
                "prestige_vertex_dolmens",
                cost: 1000,
                modifiers: new Modifier[]
                {
                    new(ECategory.MAGIC_FEATURE_COUNT, "Dolmen", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESOURCE, "Crystal", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.MagicSecretVertex,
                "prestige_vertex_magic_secret",
                // Porte d'entrée de la Magie — au moins aussi chère que la porte de l'Inframonde
                cost: 2500,
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_MAGIC, EType.ADDITIVE, 1),
                    new(ECategory.BUILDING_MAX_LEVEL, "MageTower", EType.ADDITIVE, 3),
                    new(ECategory.UNLOCK_RESEARCH, "MagicInitiation", EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.FocalizationVertex,
                "prestige_vertex_focalization",
                cost: 10000,
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "MageTower", EType.ADDITIVE, 2),
                }
            ),
            new(
                PrestigeMap.InnerCircleVertex,
                "prestige_vertex_inner_circle",
                cost: 25000,
                modifiers: new Modifier[]
                {
                    new(ECategory.RITUAL_MAX_COUNT, EType.ADDITIVE, 1),
                }
            ),
            new(
                PrestigeMap.CrystalomancyVertex,
                "prestige_vertex_crystalomancy",
                cost: 50000,
                modifiers: new Modifier[]
                {
                    new(ECategory.RITUAL_UPKEEP_REDUCTION, EType.ADDITIVE, 0.2),
                    new(ECategory.HARVEST_SPEED, "MageTower", EType.ADDITIVE, 0.5),
                }
            ),
            new(
                PrestigeMap.ArchmageVertex,
                "prestige_vertex_archmage",
                cost: 250000,
                modifiers: new Modifier[]
                {
                    new(ECategory.RITUAL_MAX_COUNT, EType.ADDITIVE, 1),
                    new(ECategory.RITUAL_TOTAL_POWER, EType.ADDITIVE, 0.25),
                    new(ECategory.PASSIVE_RESOURCE_GENERATION, "Crystal", EType.ADDITIVE, 1),
                }
            ),
            // ── Placeholder vertices (no bonuses) ────────────────────────────
            new(PrestigeMap.PlaceholderA1Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderA1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderA2Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderA2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderA3Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderA3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderB1Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderB1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderB2Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderB2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderC3Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderC3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderD2Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderD2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderE3Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderE3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderF1Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderF1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PrestigeMap.PlaceholderG2Vertex, "prestige_vertex_placeholder", cost: Cost(PrestigeMap.PlaceholderG2Vertex), modifiers: Array.Empty<Modifier>()),
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
                startingResourceBonusPerVertex: 2
            ),
            new(
                PrestigeMap.HarvestSpeedCoord,
                "prestige_hex_harvest_speed",
                adjacentVertices: Adjacent(PrestigeMap.HarvestSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.ResearchSpeedCoord,
                "prestige_hex_research_speed",
                adjacentVertices: Adjacent(PrestigeMap.ResearchSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.1) }
            ),
            // ── Outer hexes (each adjacent to one outer vertex only) ─────────
            new(
                PrestigeMap.UnitProductionSpeedCoord,
                "prestige_hex_unit_production_speed",
                adjacentVertices: Adjacent(PrestigeMap.UnitProductionSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.ResearchCostReductionCoord,
                "prestige_hex_research_cost_reduction",
                adjacentVertices: Adjacent(PrestigeMap.ResearchCostReductionCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.StorageCapacityCoord,
                "prestige_hex_storage_capacity",
                adjacentVertices: Adjacent(PrestigeMap.StorageCapacityCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.STORAGE_CAPACITY_BASIC,    EType.ADDITIVE, 10),
                    new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE,  5),
                }
            ),
            new(
                PrestigeMap.GoldTradeCoord,
                "prestige_hex_gold_trade",
                adjacentVertices: Adjacent(PrestigeMap.GoldTradeCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.MARKET_GOLD_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.ArtisansProductionCoord,
                "prestige_hex_artisans_production",
                adjacentVertices: Adjacent(PrestigeMap.ArtisansProductionCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.HARVEST_SPEED, "Mine",       EType.ADDITIVE, 0.1),
                    new(ECategory.HARVEST_SPEED, "GlassWorks", EType.ADDITIVE, 0.1),
                }
            ),
            new(
                PrestigeMap.FortifiedOutpostCoord,
                "prestige_hex_fortifications",
                adjacentVertices: Adjacent(PrestigeMap.FortifiedOutpostCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2) }
            ),
            new(
                PrestigeMap.ExperimentalScienceCoord,
                "prestige_hex_experimental_science",
                adjacentVertices: Adjacent(PrestigeMap.ExperimentalScienceCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.BUILDING_PRODUCTION, "Laboratory", EType.ADDITIVE, 1) }
            ),
            new(
                PrestigeMap.DefenseRegenCoord,
                "prestige_hex_defense_regen",
                adjacentVertices: Adjacent(PrestigeMap.DefenseRegenCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE_REGEN_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeMap.WarehouseMaxLevelCoord,
                "prestige_hex_warehouse_max_level",
                adjacentVertices: Adjacent(PrestigeMap.WarehouseMaxLevelCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Warehouse", EType.ADDITIVE, 1) }
            ),
            // ── Hex Forges (branche de l'Acier) ───────────────────────────────
            new(
                PrestigeMap.SteelworksCoord,
                "prestige_hex_steelworks",
                adjacentVertices: Adjacent(PrestigeMap.SteelworksCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.SMELTER_SPEED, EType.ADDITIVE, 0.15) }
            ),
            // ── Hexes de l'Inframonde (branche nord-ouest) ────────────────────
            new(
                PrestigeMap.UnderworldCoord,
                "prestige_hex_underworld",
                adjacentVertices: Adjacent(PrestigeMap.UnderworldCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.HARVEST_SPEED, "MushroomFarm", EType.ADDITIVE, 0.15),
                    new(ECategory.HARVEST_SPEED, "MithrilMine",  EType.ADDITIVE, 0.15),
                }
            ),
            // ── Hex Lignes Telluriques (branche de la Magie) ──────────────────
            new(
                PrestigeMap.LeyLinesCoord,
                "prestige_hex_ley_lines",
                adjacentVertices: Adjacent(PrestigeMap.LeyLinesCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RITUAL_TOTAL_POWER, EType.ADDITIVE, 0.05) }
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
