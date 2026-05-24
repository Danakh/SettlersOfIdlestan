using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeMap
{
    public IReadOnlyList<PrestigeVertex> Vertices { get; }
    public IReadOnlyList<PrestigeHex> Hexes { get; }

    public PrestigeMap(IEnumerable<PrestigeVertex> vertices, IEnumerable<PrestigeHex> hexes)
    {
        Vertices = vertices.ToList();
        Hexes = hexes.ToList();
    }

    public PrestigeVertex? GetVertex(PrestigeVertexId id) => Vertices.FirstOrDefault(v => v.Id == id);
    public PrestigeHex?    GetHex(PrestigeHexId id)       => Hexes.FirstOrDefault(h => h.Id == id);

    // Hex grid layout (R = 60, pointy-top, vertex Central at origin):
    //
    //   UnitProd(0,-120)   ResearchCost(104,60)   Storage(-104,60)
    //       |                     |                     |
    //   Barracks(0,-60)   Laboratory(52,30)   SeaportMarket(-52,30)
    //         \   [StartRes](-52,-30) [ResearchS](52,-30)   /
    //          \            |               |              /
    //                     Central(0,0)
    //                        |
    //                  [HarvestSpeed](0,60)
    //
    public static PrestigeMap CreateDefault()
    {
        var vertices = new PrestigeVertex[]
        {
            new(
                PrestigeVertexId.Central,
                cost: 3,
                prerequisites: Array.Empty<PrestigeVertexId>(),
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Library", EType.ADDITIVE, 3) },
                startingBuildings: Array.Empty<BuildingType>(),
                adjacentHexes: new[] { PrestigeHexId.StartingResources, PrestigeHexId.HarvestSpeed, PrestigeHexId.ResearchSpeed }
            ),
            new(
                PrestigeVertexId.SeaportMarket,
                cost: 5,
                prerequisites: new[] { PrestigeVertexId.Central },
                modifiers: Array.Empty<Modifier>(),
                startingBuildings: new[] { BuildingType.Seaport, BuildingType.Market },
                adjacentHexes: new[] { PrestigeHexId.StartingResources, PrestigeHexId.HarvestSpeed, PrestigeHexId.StorageCapacity }
            ),
            new(
                PrestigeVertexId.Laboratory,
                cost: 5,
                prerequisites: new[] { PrestigeVertexId.Central },
                modifiers: new Modifier[]
                {
                    new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 2),
                    new(ECategory.BUILDING_MAX_LEVEL, "GlassWorks", EType.ADDITIVE, 1),
                },
                startingBuildings: Array.Empty<BuildingType>(),
                adjacentHexes: new[] { PrestigeHexId.HarvestSpeed, PrestigeHexId.ResearchSpeed, PrestigeHexId.ResearchCostReduction }
            ),
            new(
                PrestigeVertexId.Barracks,
                cost: 5,
                prerequisites: new[] { PrestigeVertexId.Central },
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2) },
                startingBuildings: Array.Empty<BuildingType>(),
                adjacentHexes: new[] { PrestigeHexId.StartingResources, PrestigeHexId.ResearchSpeed, PrestigeHexId.UnitProductionSpeed }
            ),
        };

        var hexes = new PrestigeHex[]
        {
            // ── Inner hexes (adjacent to Central) ────────────────────────────
            new(
                PrestigeHexId.StartingResources,
                adjacentVertices: new[] { PrestigeVertexId.Central, PrestigeVertexId.SeaportMarket, PrestigeVertexId.Barracks },
                perVertexModifiers: Array.Empty<Modifier>(),
                startingResourceBonusPerVertex: 2
            ),
            new(
                PrestigeHexId.HarvestSpeed,
                adjacentVertices: new[] { PrestigeVertexId.Central, PrestigeVertexId.SeaportMarket, PrestigeVertexId.Laboratory },
                perVertexModifiers: new Modifier[] { new(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeHexId.ResearchSpeed,
                adjacentVertices: new[] { PrestigeVertexId.Central, PrestigeVertexId.Laboratory, PrestigeVertexId.Barracks },
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.1) }
            ),
            // ── Outer hexes (each adjacent to one outer vertex only) ─────────
            new(
                PrestigeHexId.UnitProductionSpeed,
                adjacentVertices: new[] { PrestigeVertexId.Barracks },
                perVertexModifiers: new Modifier[] { new(ECategory.UNIT_PRODUCTION_SPEED, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeHexId.ResearchCostReduction,
                adjacentVertices: new[] { PrestigeVertexId.Laboratory },
                perVertexModifiers: new Modifier[] { new(ECategory.RESEARCH_COST_REDUCTION, EType.ADDITIVE, 0.1) }
            ),
            new(
                PrestigeHexId.StorageCapacity,
                adjacentVertices: new[] { PrestigeVertexId.SeaportMarket },
                perVertexModifiers: new Modifier[]
                {
                    new(ECategory.STORAGE_CAPACITY_BASIC,    EType.ADDITIVE, 10),
                    new(ECategory.STORAGE_CAPACITY_ADVANCED, EType.ADDITIVE,  5),
                }
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
