using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.PrestigeMap;

/// <summary>
/// Represents a prestige tile on the prestige map, which is a hexagon with a prestige type.
/// </summary>
public class PrestigeTile
{
    public PrestigeTile(HexCoord coord, PrestigeType prestigeType)
    {
        Coord = coord;
        PrestigeType = prestigeType;
    }

    public HexCoord Coord { get; }
    public PrestigeType PrestigeType { get; }

    public override string ToString()
    {
        return $"PrestigeTile({Coord}, {PrestigeType})";
    }
}