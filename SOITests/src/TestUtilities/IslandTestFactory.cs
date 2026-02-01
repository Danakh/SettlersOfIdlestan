using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.City;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SOITests.TestUtilities;

/// <summary>
/// Small factory helpers used by unit tests to create simple island states.
/// </summary>
public static class IslandTestFactory
{
    /// <summary>
    /// Creates an IslandState containing seven hex tiles (center + 6 surrounding hexes)
    /// and a single civilization with one city placed on a vertex adjacent to three of the tiles.
    /// </summary>
    public static IslandState CreateSevenHexIslandState()
    {
        var center = new HexCoord(0, 0);
        var e = new HexCoord(1, 0);
        var w = new HexCoord(-1, 0);
        var ne = new HexCoord(0, 1);
        var sw = new HexCoord(0, -1);
        var nw = new HexCoord(-1, 1);
        var se = new HexCoord(1, -1);

        var tiles = new List<HexTile>
        {
            new HexTile(center, TerrainType.Field),
            new HexTile(e, TerrainType.Forest),
            new HexTile(w, TerrainType.Hill),
            new HexTile(ne, TerrainType.Pasture),
            new HexTile(sw, TerrainType.Mountain),
            new HexTile(nw, TerrainType.Forest),
            new HexTile(se, TerrainType.Pasture),
        };

        var map = new IslandMap(tiles);

        var civ = new Civilization { Index = 0 };

        // Place a city on the vertex shared by center, NE and E
        var cityVertex = Vertex.Create(center, ne, e);
        var city = new City(cityVertex) { CivilizationIndex = civ.Index };
        civ.Cities.Add(city);

        var state = new IslandState(map, new List<Civilization> { civ });
        return state;
    }
}
