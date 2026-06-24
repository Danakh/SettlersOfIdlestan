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

        private GamePRNG? _prng;

        public void Initialize(GamePRNG prng)
        {
            _prng = prng;
        }

        public IslandParameters GetIslandParameters(int WorldId)
        {
            // Pour la première île, on retourne les paramètres standards d'une partie normale
            if (WorldId <= 1)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 6),
                    (TerrainType.Hill, 5),
                    (TerrainType.Plain, 4),
                    (TerrainType.Mountain, 4),
                    (TerrainType.Desert, 1),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Rats, IslandFeaturePlacement.FarFromPlayer),
                };
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Compact);
            }
            // Île 2 : forme allongée avec repaire de bandits
            if (WorldId == 2)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 8),
                    (TerrainType.Hill, 8),
                    (TerrainType.Plain, 8),
                    (TerrainType.Mountain, 8),
                    (TerrainType.Desert, 3),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Rats,          IslandFeaturePlacement.CloseToPlayer),
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
                    (TerrainType.Forest, 10),
                    (TerrainType.Hill, 10),
                    (TerrainType.Plain, 10),
                    (TerrainType.Mountain, 10),
                    (TerrainType.Desert, 3),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Rats,          IslandFeaturePlacement.CloseToPlayer),
                    new IslandFeatureParameters(IslandFeatureType.Rats,          IslandFeaturePlacement.Random),
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                    new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromAllCivilization),
                };
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Crescent)
                {
                    NpcCivilizations =
                    [
                        new NpcParameters { EvolutionLevel = NpcEvolutionLevel.Medium, AggressivityLevel = NpcAggressivityLevel.Pacifist },
                    ]
                };
            }
            // Île 4 : compact avec lac + 2 civilisations NPC Medium/Cautious
            if (WorldId == 4)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 13),
                    (TerrainType.Hill, 13),
                    (TerrainType.Plain, 13),
                    (TerrainType.Mountain, 13),
                    (TerrainType.Desert, 4),
                };
                var features = new List<IslandFeatureParameters>
                {
                    new IslandFeatureParameters(IslandFeatureType.Rats,          IslandFeaturePlacement.CloseToPlayer),
                    new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                    new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                    new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromAllCivilization),
                };
                // 50 % de chance qu'un volcan soit présent à partir de l'île 4
                if (_prng!.Next(100) < 50)
                    features.Add(new IslandFeatureParameters(IslandFeatureType.Volcano, IslandFeaturePlacement.FarFromPlayer));
                return new IslandParameters(WorldId, tileData, features, IslandShapeType.Lake)
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

        private IslandParameters BuildHighEndIsland(int WorldId)
        {
            var shapes = Enum.GetValues<IslandShapeType>();
            var shape = shapes[_prng!.Next(shapes.Length)];

            var tileData = new List<(TerrainType terrainType, int tileCount)>
            {
                (TerrainType.Forest, 15),
                (TerrainType.Hill, 15),
                (TerrainType.Plain, 15),
                (TerrainType.Mountain, 15),
                (TerrainType.Desert, 5),
            };

            var features = new List<IslandFeatureParameters>
            {
                new IslandFeatureParameters(IslandFeatureType.Rats,          IslandFeaturePlacement.CloseToPlayer),
                new IslandFeatureParameters(IslandFeatureType.Rats,          IslandFeaturePlacement.Random),
                new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                new IslandFeatureParameters(IslandFeatureType.Bandit,        IslandFeaturePlacement.FarFromPlayer),
                new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                new IslandFeatureParameters(IslandFeatureType.TreasureTrove, IslandFeaturePlacement.Random),
                new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromAllCivilization),
                new IslandFeatureParameters(IslandFeatureType.BanditHideout, IslandFeaturePlacement.FarFromAllCivilization),
                new IslandFeatureParameters(IslandFeatureType.Dragon,        IslandFeaturePlacement.FarFromPlayer),
            };
            // 50 % de chance qu'un volcan soit présent à partir de l'île 4
            if (_prng.Next(100) < 50)
                features.Add(new IslandFeatureParameters(IslandFeatureType.Volcano, IslandFeaturePlacement.FarFromPlayer));

            int civCount = _prng.Next(4, 7); // 4-6 civilisations NPC
            var npcCivs = new List<NpcParameters>(civCount);
            for (int i = 0; i < civCount; i++)
            {
                // Plus d'expansion => moins agressif (et vice-versa), avec léger bruit.
                int expansionScore    = _prng.Next(0, 4);
                int aggressivityScore = Math.Clamp(3 - expansionScore + _prng.Next(-1, 2), 0, 3);
                npcCivs.Add(new NpcParameters
                {
                    EvolutionLevel    = (NpcEvolutionLevel)expansionScore,
                    AggressivityLevel = (NpcAggressivityLevel)aggressivityScore,
                });
            }

            return new IslandParameters(WorldId, tileData, features, shape)
            {
                NpcCivilizations = npcCivs,
                HasBonusIsland = _prng.Next(100) < 50,
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
