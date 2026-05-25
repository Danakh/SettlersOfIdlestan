using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Game
{
    public class IslandParameters
    {
        public int IslandID { get; set; }
        public IEnumerable<(TerrainType terrainType, int tileCount)> TileData { get; set; }
        public int CivilizationCount { get; set; }
        public List<IslandFeature> Features { get; set; }

        public IslandParameters(int islandID, IEnumerable<(TerrainType terrainType, int tileCount)> tileData, int civilizationCount, IEnumerable<IslandFeature>? features = null)
        {
            IslandID = islandID;
            TileData = tileData;
            CivilizationCount = civilizationCount;
            Features = features?.ToList() ?? new List<IslandFeature>();
        }
    }
}
