using System.Collections.Generic;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller
{
    public class AtlasController
    {
        public const int InvalidIslandId = -1;

        public IslandParameters GetIslandParameters(int islandId)
        {
            // Pour la première île, on retourne les paramètres standards d'une partie normale
            if (islandId == 1)
            {
                // Exemple de configuration standard (à adapter selon la logique du jeu)
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 5),
                    (TerrainType.Hill, 5),
                    (TerrainType.Plain, 5),
                    (TerrainType.Mountain, 5),
                    (TerrainType.Desert, 1),
                };
                int civilizationCount = 1;
                return new IslandParameters(islandId, tileData, civilizationCount);
            }
            // Pour d'autres îles, retourner une configuration différente si besoin
            if (islandId > 1)
            {
                var tileData = new List<(TerrainType terrainType, int tileCount)>
                {
                    (TerrainType.Forest, 6),
                    (TerrainType.Hill, 6),
                    (TerrainType.Plain, 6),
                    (TerrainType.Mountain, 6),
                    (TerrainType.Desert, 1),
                };
                return new IslandParameters(islandId, tileData, 1);
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
