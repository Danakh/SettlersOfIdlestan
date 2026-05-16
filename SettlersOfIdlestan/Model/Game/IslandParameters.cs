using System.Collections.Generic;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Game
{
    public class IslandParameters
    {
        public int IslandID { get; set; }
        public IEnumerable<(TerrainType terrainType, int tileCount)> TileData { get; set; }
        public int CivilizationCount { get; set; }

        public IslandParameters(int islandID, IEnumerable<(TerrainType terrainType, int tileCount)> tileData, int civilizationCount)
        {
            IslandID = islandID;
            TileData = tileData;
            CivilizationCount = civilizationCount;
        }
    }
}
