using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island
{
    public class AtlasController
    {
        public const int InvalidIslandId = -1;

        public IslandParameters GetIslandParameters(int islandId)
        {
            // Pour la première île, on retourne les paramètres standards d'une partie normale
            if (islandId == 1)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 5),
                    (TerrainType.Hill, 5),
                    (TerrainType.Plain, 4),
                    (TerrainType.Mountain, 4),
                    (TerrainType.Desert, 1),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Bandit,       IslandFeaturePlacement.FarFromPlayer),
                };
                return new IslandParameters(islandId, tileData, 1, features, IslandShapeType.Compact);
            }
            // Île 2 : forme allongée avec repaire de bandits
            if (islandId == 2)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 7),
                    (TerrainType.Hill, 7),
                    (TerrainType.Plain, 7),
                    (TerrainType.Mountain, 7),
                    (TerrainType.Desert, 3),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                    new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromPlayer),
                };
                return new IslandParameters(islandId, tileData, 1, features, IslandShapeType.Elongated);
            }
            // Île 3 : archipel + 1 civilisation NPC Low/Pacifiste
            if (islandId == 3)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 9),
                    (TerrainType.Hill, 9),
                    (TerrainType.Plain, 9),
                    (TerrainType.Mountain, 9),
                    (TerrainType.Desert, 3),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                    new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromPlayer),
                };
                return new IslandParameters(islandId, tileData, 1, features, IslandShapeType.Crescent)
                {
                    NpcCivilizations =
                    [
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Low, AggressivityLevel = NpcAggressivityLevel.Pacifist },
                    ]
                };
            }
            // Île 4+ : archipel + 2 civilisations NPC Medium/Pacifiste
            if (islandId > 3)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 9),
                    (TerrainType.Hill, 9),
                    (TerrainType.Plain, 9),
                    (TerrainType.Mountain, 9),
                    (TerrainType.Desert, 3),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                    new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromPlayer),
                };
                return new IslandParameters(islandId, tileData, 1, features, IslandShapeType.Archipelago)
                {
                    NpcCivilizations =
                    [
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Medium, AggressivityLevel = NpcAggressivityLevel.Pacifist },
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Medium, AggressivityLevel = NpcAggressivityLevel.Pacifist },
                    ]
                };
            }

            return new IslandParameters(InvalidIslandId, new List<(TerrainType terrainType, int tileCount)>(), 0);
        }

        public int GetFirstIslandID()
        {
            return 1;
        }

        public int GetNextIslandID(MainGameState gameState)
        {
            IslandState? state = gameState.CurrentIslandState;
            return state?.IslandID + 1 ?? GetFirstIslandID();
        }
    }
}
