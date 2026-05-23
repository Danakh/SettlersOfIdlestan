using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;

namespace SettlersOfIdlestan.Model.PrestigeMap;

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
    public PrestigeHex? GetHex(PrestigeHexId id) => Hexes.FirstOrDefault(h => h.Id == id);

    // Adjacency layout:
    //   Hex StartingResources: adjacent to Central, SeaportMarket, Barracks
    //   Hex HarvestSpeed:      adjacent to Central, SeaportMarket, Laboratory
    //   Hex ResearchSpeed:     adjacent to Central, Laboratory, Barracks
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
                adjacentHexes: new[] { PrestigeHexId.StartingResources, PrestigeHexId.HarvestSpeed }
            ),
            new(
                PrestigeVertexId.Laboratory,
                cost: 5,
                prerequisites: new[] { PrestigeVertexId.Central },
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Laboratory", EType.ADDITIVE, 2) },
                startingBuildings: Array.Empty<BuildingType>(),
                adjacentHexes: new[] { PrestigeHexId.HarvestSpeed, PrestigeHexId.ResearchSpeed }
            ),
            new(
                PrestigeVertexId.Barracks,
                cost: 5,
                prerequisites: new[] { PrestigeVertexId.Central },
                modifiers: new Modifier[] { new(ECategory.BUILDING_MAX_LEVEL, "Barracks", EType.ADDITIVE, 2) },
                startingBuildings: Array.Empty<BuildingType>(),
                adjacentHexes: new[] { PrestigeHexId.StartingResources, PrestigeHexId.ResearchSpeed }
            ),
        };

        var hexes = new PrestigeHex[]
        {
            new(
                PrestigeHexId.StartingResources,
                adjacentVertices: new[] { PrestigeVertexId.Central, PrestigeVertexId.SeaportMarket, PrestigeVertexId.Barracks },
                perVertexModifier: null,
                startingResourceBonusPerVertex: 2
            ),
            new(
                PrestigeHexId.HarvestSpeed,
                adjacentVertices: new[] { PrestigeVertexId.Central, PrestigeVertexId.SeaportMarket, PrestigeVertexId.Laboratory },
                perVertexModifier: new Modifier(ECategory.HARVEST_SPEED, EType.ADDITIVE, 0.1),
                startingResourceBonusPerVertex: 0
            ),
            new(
                PrestigeHexId.ResearchSpeed,
                adjacentVertices: new[] { PrestigeVertexId.Central, PrestigeVertexId.Laboratory, PrestigeVertexId.Barracks },
                perVertexModifier: new Modifier(ECategory.RESEARCH_SPEED, EType.ADDITIVE, 0.1),
                startingResourceBonusPerVertex: 0
            ),
        };

        return new PrestigeMap(vertices, hexes);
    }
}
