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
    public PrestigeHexDomain Domain { get; }

    /// <summary>Verrouillage générique : tant que le joueur n'a jamais eu de God Point, ce hex
    /// s'affiche en "???" et son contenu reste caché (voir PrestigeMapRenderer).</summary>
    public bool RequiresGodPoint { get; }

    public PrestigeHex(
        HexCoord coord,
        string localizationKey,
        IReadOnlyList<Vertex> adjacentVertices,
        IReadOnlyList<Modifier> perVertexModifiers,
        int startingResourceBonusPerVertex = 0,
        PrestigeHexDomain domain = PrestigeHexDomain.None,
        bool requiresGodPoint = false)
    {
        Coord = coord;
        LocalizationKey = localizationKey;
        AdjacentVertices = adjacentVertices;
        PerVertexModifiers = perVertexModifiers;
        StartingResourceBonusPerVertex = startingResourceBonusPerVertex;
        Domain = domain;
        RequiresGodPoint = requiresGodPoint;
    }
}
