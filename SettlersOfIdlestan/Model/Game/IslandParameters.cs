using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Game
{
    public class IslandParameters
    {
        public int IslandID { get; set; }
        public IEnumerable<(TerrainType terrainType, int tileCount)> TileData { get; set; }
        public int CivilizationCount { get; set; }
        public List<IslandFeatureParameters> Features { get; set; }
        public IslandShapeType ShapeType { get; set; }
        public List<NpcParameters> NpcCivilizations { get; set; } = new();

        public IslandParameters(int islandID, IEnumerable<(TerrainType terrainType, int tileCount)> tileData, int civilizationCount, IEnumerable<IslandFeatureParameters>? features = null, IslandShapeType shapeType = IslandShapeType.Compact)
        {
            IslandID = islandID;
            TileData = tileData;
            CivilizationCount = civilizationCount;
            Features = features?.ToList() ?? new List<IslandFeatureParameters>();
            ShapeType = shapeType;
        }
    }
}
