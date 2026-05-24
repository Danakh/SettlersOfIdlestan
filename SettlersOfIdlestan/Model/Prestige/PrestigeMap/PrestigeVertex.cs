using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeVertex
{
    public PrestigeVertexId Id { get; }
    public int Cost { get; }
    public IReadOnlyList<PrestigeVertexId> Prerequisites { get; }
    public IReadOnlyList<Modifier> Modifiers { get; }
    public IReadOnlyList<BuildingType> StartingBuildings { get; }
    public IReadOnlyList<PrestigeHexId> AdjacentHexes { get; }

    public PrestigeVertex(
        PrestigeVertexId id,
        int cost,
        IReadOnlyList<PrestigeVertexId> prerequisites,
        IReadOnlyList<Modifier> modifiers,
        IReadOnlyList<BuildingType> startingBuildings,
        IReadOnlyList<PrestigeHexId> adjacentHexes)
    {
        Id = id;
        Cost = cost;
        Prerequisites = prerequisites;
        Modifiers = modifiers;
        StartingBuildings = startingBuildings;
        AdjacentHexes = adjacentHexes;
    }
}
