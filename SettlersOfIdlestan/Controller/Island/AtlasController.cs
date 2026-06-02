using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island
{
    public class AtlasController
    {
        public const int InvalidIslandId = -1;

        public IslandParameters GetIslandParameters(int WorldId)
        {
            // Pour la première île, on retourne les paramètres standards d'une partie normale
            if (WorldId == 1)
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
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Compact);
            }
            // Île 2 : forme allongée avec repaire de bandits
            if (WorldId == 2)
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
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Elongated);
            }
            // Île 3 : cresent + 2 civilisation NPC Low/Pacifiste
            if (WorldId == 3)
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
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Crescent)
                {
                    NpcCivilizations =
                    [
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Low, AggressivityLevel = NpcAggressivityLevel.Pacifist },
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Low, AggressivityLevel = NpcAggressivityLevel.Pacifist },
                    ]
                };
            }
            // Île 4 : archipel + 2 civilisations NPC Medium/Cautious
            if (WorldId == 4)
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
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Archipelago)
                {
                    NpcCivilizations =
                    [
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Medium, AggressivityLevel = NpcAggressivityLevel.Cautious },
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Medium, AggressivityLevel = NpcAggressivityLevel.Cautious },
                    ]
                };
            }
            // Îles 5+ : forme et civilisations aléatoires, expansion et agressivité compensées
            if (WorldId >= 5)
            {
                return BuildHighEndIsland(WorldId);
            }

            return new IslandParameters(InvalidIslandId, new List<(TerrainType terrainType, int tileCount)>());
        }

        private static IslandParameters BuildHighEndIsland(int WorldId)
        {
            var prng = new GamePRNG(WorldId);

            var shapes = Enum.GetValues<IslandShapeType>();
            var shape = shapes[prng.Next(shapes.Length)];

            var tileData = new List<(TerrainType terrainType, int tileCount)>
            {
                (TerrainType.Forest, 12),
                (TerrainType.Hill, 12),
                (TerrainType.Plain, 12),
                (TerrainType.Mountain, 12),
                (TerrainType.Desert, 4),
            };

            var features = new List<IslandFeatureParameters>
            {
                new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromPlayer),
                new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromPlayer),
                new IslandFeatureParameters(IslandFeatureType.Dragon,        IslandFeaturePlacement.FarFromPlayer),
            };

            int civCount = prng.Next(4, 7); // 4-6 civilisations NPC
            var npcCivs = new List<NpcParameters>(civCount);
            for (int i = 0; i < civCount; i++)
            {
                // Plus d'expansion => moins agressif (et vice-versa), avec léger bruit.
                int expansionScore    = prng.Next(0, 4);
                int aggressivityScore = Math.Clamp(3 - expansionScore + prng.Next(-1, 2), 0, 3);
                npcCivs.Add(new NpcParameters
                {
                    EvolutionLevel    = (NpcEvolutionLevel)expansionScore,
                    AggressivityLevel = (NpcAggressivityLevel)aggressivityScore,
                });
            }

            return new IslandParameters(WorldId, tileData, features, shape)
            {
                NpcCivilizations = npcCivs,
            };
        }

        public int GetFirstWorldId()
        {
            return 1;
        }

        public int GetNextWorldId(MainGameState gameState)
        {
            WorldState? state = gameState.CurrentWorldState;
            return state?.WorldId + 1 ?? GetFirstWorldId();
        }
    }
}
