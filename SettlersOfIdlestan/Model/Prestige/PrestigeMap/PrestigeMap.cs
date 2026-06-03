using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeMap
{
    // ── Hex coordinates ──────────────────────────────────────────────────────
    // Inner hexes (each adjacent to Central vertex)
    public static readonly HexCoord StartingResourcesCoord     = new(0,  0, 0);
    public static readonly HexCoord ResearchSpeedCoord         = new(1,  0, 0);
    public static readonly HexCoord HarvestSpeedCoord          = new(0,  1, 0);
    // Outer hexes (each adjacent to exactly one outer vertex)
    public static readonly HexCoord UnitProductionSpeedCoord   = new(1, -1, 0);
    public static readonly HexCoord FortifiedOutpostCoord      = new(0, -1, 0);
    public static readonly HexCoord StorageCapacityCoord       = new(-1,  1, 0);
    public static readonly HexCoord ResearchCostReductionCoord = new(1,  1, 0);
    public static readonly HexCoord GoldTradeCoord             = new(-1,  2, 0);
    public static readonly HexCoord ArtisansProductionCoord   = new(0,   2, 0);
    public static readonly HexCoord ExperimentalScienceCoord  = new(2,   0, 0);
    public static readonly HexCoord DefenseRegenCoord         = new(2,  -1, 0);
    public static readonly HexCoord WarehouseMaxLevelCoord    = new(-1,  0, 0);
    // Placeholder hexes (no bonuses)
    public static readonly HexCoord NorthEastPlaceholderCoord = new( 3, -1, 0);
    public static readonly HexCoord NorthWestPlaceholderCoord = new(-1, -1, 0);
    public static readonly HexCoord SouthPlaceholderCoord     = new(-1,  3, 0);

    // ── Prestige vertices (HexGrid Vertex objects) ────────────────────────────
    // Layout: pointy-top, R=60, Central vertex at screen center.

    public static readonly Vertex CentralVertex          = Vertex.Create(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0));
    public static readonly Vertex BarracksVertex              = Vertex.Create(new(0, 0, 0), new(1, 0, 0), new(1, -1, 0));
    public static readonly Vertex FortifiedOutpostVertex      = Vertex.Create(new(0, 0, 0), new(0, -1, 0), new(1, -1, 0));
    public static readonly Vertex SeaportMarketVertex    = Vertex.Create(new(0, 0, 0), new(0, 1, 0), new(-1, 1, 0));
    public static readonly Vertex LaboratoryVertex       = Vertex.Create(new(1, 0, 0), new(0, 1, 0), new(1, 1, 0));
    public static readonly Vertex AppliedResearchVertex  = Vertex.Create(new(0, 1, 0), new(1, 1, 0), new(0, 2, 0));
    public static readonly Vertex AcademyVertex          = Vertex.Create(new(1, 0, 0), new(2, 0, 0), new(1, 1, 0));
    public static readonly Vertex HarvestGuildVertex     = Vertex.Create(new(-1, 1, 0), new(-1, 2, 0), new(0, 1, 0));
    public static readonly Vertex ArtisansGuildVertex    = Vertex.Create(new(-1, 2, 0), new(0, 1, 0), new(0, 2, 0));
    public static readonly Vertex MilitaryStrategyVertex  = Vertex.Create(new(1, 0, 0), new(2,  0, 0), new(2, -1, 0));
    public static readonly Vertex ConscriptionVertex      = Vertex.Create(new(1, 0, 0), new(1, -1, 0), new(2, -1, 0));
    public static readonly Vertex MilitaryAcademyVertex   = Vertex.Create(new(2, 0, 0), new(2, -1, 0), new(3, -1, 0));
    public static readonly Vertex KnowledgeMasteryVertex  = Vertex.Create(new(1, 1, 0), new(0,  2, 0), new(1,  2, 0));
    public static readonly Vertex WatchtowerVertex           = Vertex.Create(new(0, 0, 0), new(-1, 0, 0), new(-1, 1, 0));
    public static readonly Vertex MaritimeRoutesVertex       = Vertex.Create(new(-1, 0, 0), new(-1, 1, 0), new(-2, 1, 0));
    public static readonly Vertex TraderGuildVertex          = Vertex.Create(new(-1, 1, 0), new(-1, 2, 0), new(-2, 2, 0));
    public static readonly Vertex WarehouseNewCitiesVertex   = Vertex.Create(new(0, 0, 0), new(-1, 0, 0), new(0, -1, 0));

    // ── Placeholder vertices — fill all open corners around mapped hexes ──────
    // Around FortifiedOutpost (0,-1) / UnitProductionSpeed (1,-1) north edge
    public static readonly Vertex PlaceholderA1Vertex = Vertex.Create(new(0, -1, 0), new(1, -2, 0), new(1, -1, 0));
    public static readonly Vertex PlaceholderA2Vertex = Vertex.Create(new(0, -1, 0), new(0, -2, 0), new(1, -2, 0));
    public static readonly Vertex PlaceholderA3Vertex = Vertex.Create(new(1, -1, 0), new(1, -2, 0), new(2, -2, 0));
    // Around DefenseRegen (2,-1) / UnitProductionSpeed (1,-1) outer east
    public static readonly Vertex PlaceholderB1Vertex = Vertex.Create(new(1, -1, 0), new(2, -2, 0), new(2, -1, 0));
    public static readonly Vertex PlaceholderB2Vertex = Vertex.Create(new(2, -1, 0), new(2, -2, 0), new(3, -2, 0));
    // Outer NE connecting DefenseRegen → new NE hex (3,-1) / ExperimentalScience (2,0)
    public static readonly Vertex PlaceholderC1Vertex = Vertex.Create(new(2, -1, 0), new(3, -2, 0), new(3, -1, 0));
    public static readonly Vertex PlaceholderC2Vertex = Vertex.Create(new(2,  0, 0), new(3, -1, 0), new(3,  0, 0));
    public static readonly Vertex PlaceholderC3Vertex = Vertex.Create(new(2,  0, 0), new(3,  0, 0), new(2,  1, 0));
    // Around ResearchCostReduction (1,1) / ExperimentalScience (2,0) outer east
    public static readonly Vertex PlaceholderD1Vertex = Vertex.Create(new(1,  1, 0), new(2,  0, 0), new(2,  1, 0));
    public static readonly Vertex PlaceholderD2Vertex = Vertex.Create(new(1,  1, 0), new(2,  1, 0), new(1,  2, 0));
    // Around WarehouseMaxLevel (-1,0) / FortifiedOutpost (0,-1) outer NW
    public static readonly Vertex PlaceholderE1Vertex = Vertex.Create(new(-1,  0, 0), new( 0, -1, 0), new(-1, -1, 0));
    public static readonly Vertex PlaceholderE2Vertex = Vertex.Create(new( 0, -1, 0), new(-1, -1, 0), new( 0, -2, 0));
    // Outer W connecting WarehouseMaxLevel → new NW hex (-2,0)
    public static readonly Vertex PlaceholderE3Vertex = Vertex.Create(new(-1,  0, 0), new(-2,  1, 0), new(-2,  0, 0));
    public static readonly Vertex PlaceholderE4Vertex = Vertex.Create(new(-1,  0, 0), new(-2,  0, 0), new(-1, -1, 0));
    // Around StorageCapacity (-1,1) outer west
    public static readonly Vertex PlaceholderF1Vertex = Vertex.Create(new(-1,  1, 0), new(-2,  2, 0), new(-2,  1, 0));
    // Around ArtisansProduction (0,2) / GoldTrade (-1,2) outer south connecting to new S hex (0,3)
    public static readonly Vertex PlaceholderG1Vertex = Vertex.Create(new(-1,  2, 0), new( 0,  2, 0), new(-1,  3, 0));
    public static readonly Vertex PlaceholderG2Vertex = Vertex.Create(new( 0,  2, 0), new( 1,  2, 0), new( 0,  3, 0));
    public static readonly Vertex PlaceholderG3Vertex = Vertex.Create(new( 0,  2, 0), new( 0,  3, 0), new(-1,  3, 0));
    // Around GoldTrade (-1,2) outer SW
    public static readonly Vertex PlaceholderH1Vertex = Vertex.Create(new(-1,  2, 0), new(-1,  3, 0), new(-2,  3, 0));
    public static readonly Vertex PlaceholderH2Vertex = Vertex.Create(new(-1,  2, 0), new(-2,  3, 0), new(-2,  2, 0));
    // Outer vertices of new NE placeholder hex (3,-1)
    public static readonly Vertex PlaceholderNE1Vertex = Vertex.Create(new(3, -1, 0), new(4, -1, 0), new(3,  0, 0));
    public static readonly Vertex PlaceholderNE2Vertex = Vertex.Create(new(3, -1, 0), new(3, -2, 0), new(4, -2, 0));
    public static readonly Vertex PlaceholderNE3Vertex = Vertex.Create(new(3, -1, 0), new(4, -2, 0), new(4, -1, 0));
    // Connecting vertices toward new NW placeholder hex (-2,-1) — involve (-2,0) which is not mapped
    public static readonly Vertex PlaceholderNW4Vertex = Vertex.Create(new(-2,  0, 0), new(-2, -1, 0), new(-1, -1, 0));
    // Outer vertices of new NW placeholder hex (-2,-1)
    public static readonly Vertex PlaceholderNWDVertex = Vertex.Create(new(-2, -1, 0), new(-1, -2, 0), new(-1, -1, 0));
    // North corner of NW placeholder hex (-1,-1) — between (-1,-2) and (0,-2), both unmapped
    public static readonly Vertex PlaceholderNWBVertex = Vertex.Create(new(-1, -1, 0), new(-1, -2, 0), new( 0, -2, 0));
    // Connecting vertex toward new S placeholder hex (-1,3) — involves (0,3) which is not mapped
    public static readonly Vertex PlaceholderS3Vertex  = Vertex.Create(new( 0,  3, 0), new(-1,  4, 0), new(-1,  3, 0));
    // Outer vertices of new S placeholder hex (-1,3)
    public static readonly Vertex PlaceholderSAVertex  = Vertex.Create(new(-1,  3, 0), new(-1,  4, 0), new(-2,  4, 0));
    public static readonly Vertex PlaceholderSBVertex  = Vertex.Create(new(-1,  3, 0), new(-2,  4, 0), new(-2,  3, 0));

    public IReadOnlyList<PrestigeVertex> Vertices { get; }
    public IReadOnlyList<PrestigeHex> Hexes { get; }

    public event Action<Vertex>? VertexPurchased;

    internal void RaiseVertexPurchased(Vertex vertex) => VertexPurchased?.Invoke(vertex);

    public PrestigeMap(IEnumerable<PrestigeVertex> vertices, IEnumerable<PrestigeHex> hexes)
    {
        Vertices = vertices.ToList();
        Hexes = hexes.ToList();
    }

    public PrestigeVertex? GetVertex(Vertex coord) => Vertices.FirstOrDefault(v => v.Coord.Equals(coord));
    public PrestigeHex?    GetHex(HexCoord coord)  => Hexes.FirstOrDefault(h => h.Coord.Equals(coord));

    public IReadOnlyList<PrestigeVertex> GetNeighbors(Vertex coord)
        => Vertices.Where(v => !v.Coord.Equals(coord) && coord.IsAdjacentTo(v.Coord)).ToList();

    // Default cost formula: central = 10, others = 10 + distance² × 5.
    public static int DefaultCost(int distanceFromCenter)
    {
        int[] costPerDistance = new int[] { 10, 25, 100, 400, 2000, 10000 };
        int len = costPerDistance.Length;
        return distanceFromCenter < len
            ? costPerDistance[distanceFromCenter]
            : costPerDistance[len - 1] * (int)Math.Pow(10, distanceFromCenter + 1 - len);
    }

    public static PrestigeMap CreateDefault()
    {
        int Cost(Vertex v) => DefaultCost(v.EdgeDistanceTo(CentralVertex));

        var vertices = new PrestigeVertex[]
        {
            new(
                CentralVertex,
                "prestige_vertex_central",
                cost: Cost(CentralVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 3) }
            ),
            new(
                SeaportMarketVertex,
                "prestige_vertex_seaport_market",
                cost: Cost(SeaportMarketVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.STARTING_CITY_BUILDING, "Seaport", EType.ADDITIVE, 1),
                    new(ECategory.STARTING_CITY_BUILDING, "Market",  EType.ADDITIVE, 1),
                }
            ),
            new(
                WatchtowerVertex,
                "prestige_vertex_watchtower",
                cost: Cost(WatchtowerVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Watchtower", EType.ADDITIVE, 1) }
            ),
            new(
                MaritimeRoutesVertex,
                "prestige_vertex_maritime_routes",
                cost: Cost(MaritimeRoutesVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_MARITIME_ROUTES, EType.ADDITIVE, 1) }
            ),
            new(
                TraderGuildVertex,
                "prestige_vertex_traders_guild",
                cost: Cost(TraderGuildVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "TraderGuild", EType.ADDITIVE, 1) }
            ),
            new(
                WarehouseNewCitiesVertex,
                "prestige_vertex_warehouse_new_cities",
                cost: Cost(WarehouseNewCitiesVertex),
                modifiers: new Modifier[] { new(ECategory.NEW_CITY_BUILDING, "Warehouse", EType.ADDITIVE, 1) }
            ),
            new(
                LaboratoryVertex,
                "prestige_vertex_laboratory",
                cost: Cost(LaboratoryVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "GlassWorks", EType.ADDITIVE, 1),
                    new(ECategory.UNLOCK_RESOURCE, "Glass", EType.ADDITIVE, 1),
                }
            ),
            new(
                BarracksVertex,
                "prestige_vertex_barracks",
                cost: Cost(BarracksVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2),
                    new(ECategory.UNLOCK_RESEARCH, "MilitaryBuildings", EType.ADDITIVE, 1),
                }
            ),
            new(
                FortifiedOutpostVertex,
                "prestige_vertex_fortified_outpost",
                cost: Cost(FortifiedOutpostVertex),
                modifiers: new Modifier[] { new(ECategory.NEW_CITY_BUILDING, "Palisade", EType.ADDITIVE, 1) }
            ),
            new(
                HarvestGuildVertex,
                "prestige_vertex_harvesters_guild",
                cost: Cost(HarvestGuildVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "HarvestersGuild", EType.ADDITIVE, 1) }
            ),
            new(
                ArtisansGuildVertex,
                "prestige_vertex_artisans_guild",
                cost: Cost(ArtisansGuildVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "ArtisansGuild", EType.ADDITIVE, 1) }
            ),
            new(
                AppliedResearchVertex,
                "prestige_vertex_applied_research",
                cost: Cost(AppliedResearchVertex),
                modifiers: new Modifier[] { new(ECategory.UNLOCK_RESEARCH, "Artisanat", EType.ADDITIVE, 1) }
            ),
            new(
                ConscriptionVertex,
                "prestige_vertex_conscription",
                cost: Cost(ConscriptionVertex),
                modifiers: new Modifier[] { new(ECategory.CITY_MAX_SOLDIERS_BONUS, EType.ADDITIVE, 5) }
            ),
            new(
                MilitaryStrategyVertex,
                "prestige_vertex_military_strategy",
                cost: Cost(MilitaryStrategyVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RESEARCH, "MilitaryDiscipline", EType.ADDITIVE, 1),
                }
            ),
            new(
                MilitaryAcademyVertex,
                "prestige_vertex_military_academy",
                cost: Cost(MilitaryAcademyVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "MilitaryAcademy", EType.ADDITIVE, 4) }
            ),
            new(
                KnowledgeMasteryVertex,
                "prestige_vertex_knowledge_mastery",
                cost: Cost(KnowledgeMasteryVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.UNLOCK_RESEARCH, "Erudition", EType.ADDITIVE, 1),
                }
            ),
            new(
                AcademyVertex,
                "prestige_vertex_academy",
                cost: Cost(AcademyVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Academy", EType.ADDITIVE, 1) }
            ),
            // ── Placeholder vertices (no bonuses) ────────────────────────────
            new(PlaceholderA1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderA1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderA2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderA2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderA3Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderA3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderB1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderB1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderB2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderB2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderC1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderC1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderC2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderC2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderC3Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderC3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderD1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderD1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderD2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderD2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderE1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderE1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderE2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderE2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderE3Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderE3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderE4Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderE4Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderF1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderF1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderG1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderG1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderG2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderG2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderG3Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderG3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderH1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderH1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderH2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderH2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderNE1Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderNE1Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderNE2Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderNE2Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderNE3Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderNE3Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderNW4Vertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderNW4Vertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderNWDVertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderNWDVertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderNWBVertex, "prestige_vertex_placeholder", cost: Cost(PlaceholderNWBVertex), modifiers: Array.Empty<Modifier>()),
            new(PlaceholderS3Vertex,  "prestige_vertex_placeholder", cost: Cost(PlaceholderS3Vertex),  modifiers: Array.Empty<Modifier>()),
            new(PlaceholderSAVertex,  "prestige_vertex_placeholder", cost: Cost(PlaceholderSAVertex),  modifiers: Array.Empty<Modifier>()),
            new(PlaceholderSBVertex,  "prestige_vertex_placeholder", cost: Cost(PlaceholderSBVertex),  modifiers: Array.Empty<Modifier>()),
        };

        // Adjacency computed from vertex definitions — no manual list needed.
        IReadOnlyList<Vertex> Adjacent(HexCoord hex)
            => vertices.Select(v => v.Coord).Where(v => v.IsAdjacentTo(hex)).ToList();

        var hexes = new PrestigeHex[]
        {
            // ── Inner hexes (adjacent to Central) ────────────────────────────
            new(
                StartingResourcesCoord,
                "prestige_hex_starting_resources",
                adjacentVertices: Adjacent(StartingResourcesCoord),
                perVertexModifiers: Array.Empty<Modifier>(),
                startingResourceBonusPerVertex: 2
            ),
            new(
                HarvestSpeedCoord,
                "prestige_hex_harvest_speed",
                adjacentVertices: Adjacent(HarvestSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                ResearchSpeedCoord,
                "prestige_hex_research_speed",
                adjacentVertices: Adjacent(ResearchSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.1) }
            ),
            // ── Outer hexes (each adjacent to one outer vertex only) ─────────
            new(
                UnitProductionSpeedCoord,
                "prestige_hex_unit_production_speed",
                adjacentVertices: Adjacent(UnitProductionSpeedCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                ResearchCostReductionCoord,
                "prestige_hex_research_cost_reduction",
                adjacentVertices: Adjacent(ResearchCostReductionCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) }
            ),
            new(
                StorageCapacityCoord,
                "prestige_hex_storage_capacity",
                adjacentVertices: Adjacent(StorageCapacityCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.STORAGE_CAPACITY_BASIC,    EType.ADDITIVE, 10),
                    new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE,  5),
                }
            ),
            new(
                GoldTradeCoord,
                "prestige_hex_gold_trade",
                adjacentVertices: Adjacent(GoldTradeCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.TRADE_GOLD_PACKAGES, EType.ADDITIVE, -0.5) }
            ),
            new(
                ArtisansProductionCoord,
                "prestige_hex_artisans_production",
                adjacentVertices: Adjacent(ArtisansProductionCoord),
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.HARVEST_SPEED, "Mine",       EType.ADDITIVE, 0.1),
                    new(ECategory.HARVEST_SPEED, "GlassWorks", EType.ADDITIVE, 0.1),
                }
            ),
            new(
                FortifiedOutpostCoord,
                "prestige_hex_fortifications",
                adjacentVertices: Adjacent(FortifiedOutpostCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2) }
            ),
            new(
                ExperimentalScienceCoord,
                "prestige_hex_experimental_science",
                adjacentVertices: Adjacent(ExperimentalScienceCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.BUILDING_PRODUCTION, "Laboratory", EType.ADDITIVE, 1) }
            ),
            new(
                DefenseRegenCoord,
                "prestige_hex_defense_regen",
                adjacentVertices: Adjacent(DefenseRegenCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE_REGEN_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                WarehouseMaxLevelCoord,
                "prestige_hex_warehouse_max_level",
                adjacentVertices: Adjacent(WarehouseMaxLevelCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Warehouse", EType.ADDITIVE, 1) }
            ),
            // ── Placeholder hexes ─────────────────────────────────────────────
            new(
                NorthEastPlaceholderCoord,
                "prestige_hex_placeholder",
                adjacentVertices: Adjacent(NorthEastPlaceholderCoord),
                perVertexModifiers: Array.Empty<Modifier>()
            ),
            new(
                NorthWestPlaceholderCoord,
                "prestige_hex_placeholder",
                adjacentVertices: Adjacent(NorthWestPlaceholderCoord),
                perVertexModifiers: Array.Empty<Modifier>()
            ),
            new(
                SouthPlaceholderCoord,
                "prestige_hex_placeholder",
                adjacentVertices: Adjacent(SouthPlaceholderCoord),
                perVertexModifiers: Array.Empty<Modifier>()
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
