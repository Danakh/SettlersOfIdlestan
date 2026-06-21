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

    /// <summary>Verrouillage générique : tant que le pouvoir divin Foi n'est pas débloqué
    /// (UNLOCK_DOMINION), ce hex s'affiche en "???" et son contenu reste caché (voir PrestigeMapRenderer).</summary>
    public bool RequiresDominionUnlock { get; }

    public PrestigeHex(
        HexCoord coord,
        string localizationKey,
        IReadOnlyList<Vertex> adjacentVertices,
        IReadOnlyList<Modifier> perVertexModifiers,
        int startingResourceBonusPerVertex = 0,
        PrestigeHexDomain domain = PrestigeHexDomain.None,
        bool requiresDominionUnlock = false)
    {
        Coord = coord;
        LocalizationKey = localizationKey;
        AdjacentVertices = adjacentVertices;
        PerVertexModifiers = perVertexModifiers;
        StartingResourceBonusPerVertex = startingResourceBonusPerVertex;
        Domain = domain;
        RequiresDominionUnlock = requiresDominionUnlock;
    }
}
