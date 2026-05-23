using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.PrestigeMap;

public class PrestigeHex
{
    public PrestigeHexId Id { get; }
    public IReadOnlyList<PrestigeVertexId> AdjacentVertices { get; }
    // TechnologyTree modifier applied once per adjacent purchased vertex (null if no effect on TechTree)
    public Modifier? PerVertexModifier { get; }
    // Bonus to starting resources (Food/Wood/Brick/Stone) per adjacent purchased vertex
    public int StartingResourceBonusPerVertex { get; }

    public PrestigeHex(
        PrestigeHexId id,
        IReadOnlyList<PrestigeVertexId> adjacentVertices,
        Modifier? perVertexModifier,
        int startingResourceBonusPerVertex = 0)
    {
        Id = id;
        AdjacentVertices = adjacentVertices;
        PerVertexModifier = perVertexModifier;
        StartingResourceBonusPerVertex = startingResourceBonusPerVertex;
    }
}
