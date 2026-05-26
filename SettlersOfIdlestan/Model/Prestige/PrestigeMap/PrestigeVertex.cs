using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Prestige.PrestigeMap;

public class PrestigeVertex
{
    public Vertex Coord { get; }
    public string LocalizationKey { get; }
    public int Cost { get; }
    public IReadOnlyList<Vertex> Prerequisites { get; }
    public IReadOnlyList<Modifier> Modifiers { get; }
    public IReadOnlyList<BuildingType> StartingBuildings { get; }

    public PrestigeVertex(
        Vertex coord,
        string localizationKey,
        int cost,
        IReadOnlyList<Vertex> prerequisites,
        IReadOnlyList<Modifier> modifiers,
        IReadOnlyList<BuildingType> startingBuildings)
    {
        Coord = coord;
        LocalizationKey = localizationKey;
        Cost = cost;
        Prerequisites = prerequisites;
        Modifiers = modifiers;
        StartingBuildings = startingBuildings;
    }
}
