using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeMap
{
    // ── Hex coordinates ──────────────────────────────────────────────────────
    // Inner hexes (each adjacent to Central vertex)
    public static readonly HexCoord StartingResourcesCoord     = new(0,  0);
    public static readonly HexCoord ResearchSpeedCoord         = new(1,  0);
    public static readonly HexCoord HarvestSpeedCoord          = new(0,  1);
    // Outer hexes (each adjacent to exactly one outer vertex)
    public static readonly HexCoord UnitProductionSpeedCoord   = new(1, -1);
    public static readonly HexCoord FortifiedOutpostCoord      = new(0, -1);
    public static readonly HexCoord StorageCapacityCoord       = new(-1,  1);
    public static readonly HexCoord ResearchCostReductionCoord = new(1,  1);
    public static readonly HexCoord GoldTradeCoord             = new(-1,  2);
    public static readonly HexCoord ArtisansProductionCoord   = new(0,   2);

    // ── Prestige vertices (HexGrid Vertex objects) ────────────────────────────
    // Layout: pointy-top, R=60, Central vertex at screen center.

    public static readonly Vertex CentralVertex          = Vertex.Create(new(0, 0), new(1, 0), new(0, 1));
    public static readonly Vertex BarracksVertex              = Vertex.Create(new(0, 0), new(1, 0), new(1, -1));
    public static readonly Vertex FortifiedOutpostVertex      = Vertex.Create(new(0, 0), new(0, -1), new(1, -1));
    public static readonly Vertex SeaportMarketVertex    = Vertex.Create(new(0, 0), new(0, 1), new(-1, 1));
    public static readonly Vertex LaboratoryVertex       = Vertex.Create(new(1, 0), new(0, 1), new(1, 1));
    public static readonly Vertex AppliedResearchVertex  = Vertex.Create(new(0, 1), new(1, 1), new(0, 2));
    public static readonly Vertex HarvestGuildVertex     = Vertex.Create(new(-1, 1), new(-1, 2), new(0, 1));
    public static readonly Vertex ArtisansGuildVertex    = Vertex.Create(new(-1, 2), new(0, 1), new(0, 2));
    public static readonly Vertex MilitaryStrategyVertex = Vertex.Create(new(1, 0), new(1, -1), new(2, -1));

    public IReadOnlyList<PrestigeVertex> Vertices { get; }
    public IReadOnlyList<PrestigeHex> Hexes { get; }

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
        => distanceFromCenter == 0 ? 10 : 10 + distanceFromCenter * distanceFromCenter * 5;

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
                LaboratoryVertex,
                "prestige_vertex_laboratory",
                cost: Cost(LaboratoryVertex),
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "GlassWorks", EType.ADDITIVE, 1),
                }
            ),
            new(
                BarracksVertex,
                "prestige_vertex_barracks",
                cost: Cost(BarracksVertex),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2) }
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
                modifiers: Array.Empty<Modifier>()
            ),
            new(
                MilitaryStrategyVertex,
                "prestige_vertex_military_strategy",
                cost: Cost(MilitaryStrategyVertex),
                modifiers: Array.Empty<Modifier>()
            ),
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
                "prestige_hex_fortified_outpost",
                adjacentVertices: Adjacent(FortifiedOutpostCoord),
                perVertexModifiers: new Modifier[] { new(ECategory.CITY_DEFENSE, EType.ADDITIVE, 2) }
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
