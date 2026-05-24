using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeHex
{
    public PrestigeHexId Id { get; }
    public IReadOnlyList<PrestigeVertexId> AdjacentVertices { get; }
    // TechnologyTree modifiers applied once per adjacent purchased vertex (empty list if no TechTree effect)
    public IReadOnlyList<Modifier> PerVertexModifiers { get; }
    // Bonus to starting resources (Food/Wood/Brick/Stone) per adjacent purchased vertex
    public int StartingResourceBonusPerVertex { get; }

    public PrestigeHex(
        PrestigeHexId id,
        IReadOnlyList<PrestigeVertexId> adjacentVertices,
        IReadOnlyList<Modifier> perVertexModifiers,
        int startingResourceBonusPerVertex = 0)
    {
        Id = id;
        AdjacentVertices = adjacentVertices;
        PerVertexModifiers = perVertexModifiers;
        StartingResourceBonusPerVertex = startingResourceBonusPerVertex;
    }
}
