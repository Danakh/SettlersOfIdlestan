using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Buildings;

public interface IBuildingContext
{
    int Level { get; }
    Vertex Position { get; }
    IReadOnlyList<Building> Buildings { get; }
}
