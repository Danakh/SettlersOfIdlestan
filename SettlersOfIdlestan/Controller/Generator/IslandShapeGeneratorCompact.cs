using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

public class IslandShapeGeneratorCompact : IslandShapeGenerator
{
    private readonly GamePRNG _prng;

    public IslandShapeGeneratorCompact(GamePRNG prng)
    {
        _prng = prng;
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer)
    {
        if (count <= 0) return [];

        var origin = new HexCoord(0, 0, layer);
        var island = new List<HexCoord>(count) { origin };
        var allLand = new HashSet<HexCoord> { origin };

        GrowIsland(island, count, allLand);
        return island;
    }

    private void GrowIsland(List<HexCoord> island, int targetSize, HashSet<HexCoord> allLand)
    {
        int stuckLimit = (targetSize + 1) * 6;
        int stuckCount = 0;

        while (island.Count < targetSize && stuckCount < stuckLimit)
        {
            int idx = _prng.Next(island.Count);
            var hex = island[idx];
            bool addedAny = false;

            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                if (island.Count >= targetSize) break;
                var nb = hex.Neighbor(dir);
                if (allLand.Contains(nb)) continue;

                island.Add(nb);
                allLand.Add(nb);
                addedAny = true;
            }

            stuckCount = addedAny ? 0 : stuckCount + 1;
        }

        FixTips(island, allLand);
    }

    // Remplace les tuiles "pointe" (< 2 voisins terrestres) par des tuiles mieux connectées.
    // Le seul cas habituel : la dernière tuile ajoutée, dont la sœur dans la rafale n'a pas été
    // insérée parce que targetSize était atteint juste avant.
    private static void FixTips(List<HexCoord> island, HashSet<HexCoord> allLand)
    {
        const int maxPasses = 20;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            int tipIdx = FindTipIndex(island, allLand);
            if (tipIdx < 0) return;

            var tip = island[tipIdx];
            allLand.Remove(tip);

            HexCoord? replacement = null;
            foreach (var hex in island)
            {
                if (hex.Equals(tip)) continue;
                foreach (var dir in HexDirectionUtils.AllHexDirections)
                {
                    var nb = hex.Neighbor(dir);
                    if (allLand.Contains(nb)) continue;
                    int nbCount = HexDirectionUtils.AllHexDirections.Count(d => allLand.Contains(nb.Neighbor(d)));
                    if (nbCount >= 2) { replacement = nb; break; }
                }
                if (replacement != null) break;
            }

            if (replacement != null)
            {
                island[tipIdx] = replacement;
                allLand.Add(replacement);
            }
            else
            {
                allLand.Add(tip); // Pas de remplacement trouvé, on laisse tel quel
                return;
            }
        }
    }

    private static int FindTipIndex(List<HexCoord> island, HashSet<HexCoord> allLand)
    {
        for (int i = 0; i < island.Count; i++)
        {
            int count = HexDirectionUtils.AllHexDirections.Count(d => allLand.Contains(island[i].Neighbor(d)));
            if (count < 2) return i;
        }
        return -1;
    }
}
