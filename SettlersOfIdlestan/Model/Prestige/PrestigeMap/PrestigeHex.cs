using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeHex
{
    public HexCoord Coord { get; }
    public string LocalizationKey { get; }
    public IReadOnlyList<Vertex> AdjacentVertices { get; }
    public IReadOnlyList<Modifier> PerVertexModifiers { get; }
    public int StartingResourceBonusPerVertex { get; }

    public PrestigeHex(
        HexCoord coord,
        string localizationKey,
        IReadOnlyList<Vertex> adjacentVertices,
        IReadOnlyList<Modifier> perVertexModifiers,
        int startingResourceBonusPerVertex = 0)
    {
        Coord = coord;
        LocalizationKey = localizationKey;
        AdjacentVertices = adjacentVertices;
        PerVertexModifiers = perVertexModifiers;
        StartingResourceBonusPerVertex = startingResourceBonusPerVertex;
    }
}
