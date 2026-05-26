using SettlersOfIdlestan.Model.Buildings;
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
    public static readonly HexCoord StorageCapacityCoord       = new(-1,  1);
    public static readonly HexCoord ResearchCostReductionCoord = new(1,  1);
    public static readonly HexCoord GoldTradeCoord             = new(-1,  2);

    // ── Prestige vertices (HexGrid Vertex objects) ────────────────────────────
    // Layout: pointy-top, R=60, Central vertex at screen center.
    //
    //   Barracks(0,-60)  SeaportMkt(-52,30)      Lab(52,30)
    //    [StartRes](-52,-30) [ResearchS](52,-30)
    //     [UnitProd](0,-120)       Central(0,0)
    //               [HarvestSpeed](0,60)   [ResearchCost](104,60)  [Storage](-104,60)
    //                          HarvestGuild(-52,90)
    //                             [GoldTrade](-52,150)
    //
    public static readonly Vertex CentralVertex        = Vertex.Create(new(0, 0), new(1, 0), new(0, 1));
    public static readonly Vertex BarracksVertex       = Vertex.Create(new(0, 0), new(1, 0), new(1, -1));
    public static readonly Vertex SeaportMarketVertex  = Vertex.Create(new(0, 0), new(0, 1), new(-1, 1));
    public static readonly Vertex LaboratoryVertex     = Vertex.Create(new(1, 0), new(0, 1), new(1, 1));
    public static readonly Vertex HarvestGuildVertex   = Vertex.Create(new(-1, 1), new(-1, 2), new(0, 1));

    public IReadOnlyList<PrestigeVertex> Vertices { get; }
    public IReadOnlyList<PrestigeHex> Hexes { get; }

    public PrestigeMap(IEnumerable<PrestigeVertex> vertices, IEnumerable<PrestigeHex> hexes)
    {
        Vertices = vertices.ToList();
        Hexes = hexes.ToList();
    }

    public PrestigeVertex? GetVertex(Vertex coord) => Vertices.FirstOrDefault(v => v.Coord.Equals(coord));
    public PrestigeHex?    GetHex(HexCoord coord)  => Hexes.FirstOrDefault(h => h.Coord.Equals(coord));

    public static PrestigeMap CreateDefault()
    {
        var vertices = new PrestigeVertex[]
        {
            new(
                CentralVertex,
                "prestige_vertex_central",
                cost: 3,
                prerequisites: Array.Empty<Vertex>(),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 3) },
                startingBuildings: Array.Empty<BuildingType>()
            ),
            new(
                SeaportMarketVertex,
                "prestige_vertex_seaport_market",
                cost: 5,
                prerequisites: new[] { CentralVertex },
                modifiers: Array.Empty<Modifier>(),
                startingBuildings: new[] { BuildingType.Seaport, BuildingType.Market }
            ),
            new(
                LaboratoryVertex,
                "prestige_vertex_laboratory",
                cost: 5,
                prerequisites: new[] { CentralVertex },
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "GlassWorks", EType.ADDITIVE, 1),
                },
                startingBuildings: Array.Empty<BuildingType>()
            ),
            new(
                BarracksVertex,
                "prestige_vertex_barracks",
                cost: 5,
                prerequisites: new[] { CentralVertex },
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2) },
                startingBuildings: Array.Empty<BuildingType>()
            ),
            new(
                HarvestGuildVertex,
                "prestige_vertex_harvesters_guild",
                cost: 5,
                prerequisites: new[] { SeaportMarketVertex },
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "HarvestersGuild", EType.ADDITIVE, 1) },
                startingBuildings: Array.Empty<BuildingType>()
            ),
        };

        var hexes = new PrestigeHex[]
        {
            // ── Inner hexes (adjacent to Central) ────────────────────────────
            new(
                StartingResourcesCoord,
                "prestige_hex_starting_resources",
                adjacentVertices: new[] { CentralVertex, SeaportMarketVertex, BarracksVertex },
                perVertexModifiers: Array.Empty<Modifier>(),
                startingResourceBonusPerVertex: 2
            ),
            new(
                HarvestSpeedCoord,
                "prestige_hex_harvest_speed",
                adjacentVertices: new[] { CentralVertex, SeaportMarketVertex, LaboratoryVertex, HarvestGuildVertex },
                perVertexModifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                ResearchSpeedCoord,
                "prestige_hex_research_speed",
                adjacentVertices: new[] { CentralVertex, LaboratoryVertex, BarracksVertex },
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.1) }
            ),
            // ── Outer hexes (each adjacent to one outer vertex only) ─────────
            new(
                UnitProductionSpeedCoord,
                "prestige_hex_unit_production_speed",
                adjacentVertices: new[] { BarracksVertex },
                perVertexModifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                ResearchCostReductionCoord,
                "prestige_hex_research_cost_reduction",
                adjacentVertices: new[] { LaboratoryVertex },
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) }
            ),
            new(
                StorageCapacityCoord,
                "prestige_hex_storage_capacity",
                adjacentVertices: new[] { SeaportMarketVertex, HarvestGuildVertex },
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.STORAGE_CAPACITY_BASIC,    EType.ADDITIVE, 10),
                    new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE,  5),
                }
            ),
            new(
                GoldTradeCoord,
                "prestige_hex_gold_trade",
                adjacentVertices: new[] { HarvestGuildVertex },
                perVertexModifiers: new Modifier[] { new(ECategory.TRADE_GOLD_PACKAGES, EType.ADDITIVE, -0.5) }
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
