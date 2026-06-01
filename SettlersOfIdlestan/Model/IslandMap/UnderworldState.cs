using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// State of the Underworld — a second map accessible after building the DeepestMine.
/// Contains a small 3-hex cavern map and the outpost planted by the player.
/// </summary>
public class UnderworldState
{
    public IslandMap Map { get; set; }

    /// <summary>
    /// Cities in the Underworld. Stored separately from surface civilizations.
    /// </summary>
    public List<City> Cities { get; set; }

    [System.Text.Json.Serialization.JsonConstructor]
    public UnderworldState()
    {
        Map = new IslandMap(System.Array.Empty<HexTile>(), HexCoord.UnderworldZ);
        Cities = new List<City>();
    }

    /// <summary>
    /// Creates the default 3-hex underworld map with an outpost at the shared vertex.
    /// Hexes (0,0), (1,0), (0,1) form a triangle sharing one vertex.
    /// </summary>
    public static UnderworldState CreateDefault(int playerCivIndex)
    {
        var tiles = new[]
        {
            new HexTile(new HexCoord(0, 0, HexCoord.UnderworldZ), TerrainType.Mountain),
            new HexTile(new HexCoord(1, 0, HexCoord.UnderworldZ), TerrainType.Mountain),
            new HexTile(new HexCoord(0, 1, HexCoord.UnderworldZ), TerrainType.Mountain),
        };

        var map = new IslandMap(tiles);

        var outpostVertex = Vertex.Create(
            new HexCoord(0, 0, HexCoord.UnderworldZ),
            new HexCoord(1, 0, HexCoord.UnderworldZ),
            new HexCoord(0, 1, HexCoord.UnderworldZ));

        var outpost = new City(outpostVertex) { CivilizationIndex = playerCivIndex };

        return new UnderworldState
        {
            Map = map,
            Cities = new List<City> { outpost },
        };
    }
}
