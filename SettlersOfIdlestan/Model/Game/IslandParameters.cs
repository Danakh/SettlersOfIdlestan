using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Model.Game
{
    public class IslandParameters
    {
        public int WorldId { get; set; }
        public IEnumerable<(TerrainType terrainType, int tileCount)> TileData { get; set; }
        public List<IslandFeatureParameters> Features { get; set; }
        public IslandShapeType ShapeType { get; set; }
        public List<NpcParameters> NpcCivilizations { get; set; } = new();
        public bool HasBonusIsland { get; set; }

        public IslandParameters(int worldId, IEnumerable<(TerrainType terrainType, int tileCount)> tileData, IEnumerable<IslandFeatureParameters>? features = null, IslandShapeType shapeType = IslandShapeType.Compact)
        {
            WorldId = worldId;
            TileData = tileData;
            Features = features?.ToList() ?? new List<IslandFeatureParameters>();
            ShapeType = shapeType;
        }
    }
}
