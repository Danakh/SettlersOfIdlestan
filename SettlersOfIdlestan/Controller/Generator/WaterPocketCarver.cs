using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Helper partagé par IslandShapeGeneratorLake et IslandShapeGeneratorInlandSea : construit une
/// chaîne connexe de hexs (au sein d'un ensemble de terre déjà généré) qui ne forme jamais de
/// triangle (3 hexs mutuellement adjacents = un Vertex), pour obtenir une poche d'eau allongée
/// plutôt qu'un blob compact.
/// </summary>
internal static class WaterPocketCarver
{
    /// <summary>
    /// Construit une chaîne de `size` hexs en restant à l'intérieur de `landSet`, en partant de `seed`.
    /// Retourne null si la marche aléatoire reste bloquée avant d'atteindre la taille demandée.
    /// </summary>
    public static List<HexCoord>? CarveChain(GamePRNG prng, HexCoord seed, int size, HashSet<HexCoord> landSet)
    {
        if (!landSet.Contains(seed)) return null;

        var chain = new List<HexCoord> { seed };
        var chainSet = new HashSet<HexCoord> { seed };

        while (chain.Count < size)
        {
            int idx = prng.Next(chain.Count);
            var from = chain[idx];
            var dirs = HexDirectionUtils.AllHexDirections.ToList();
            prng.Shuffle(dirs);

            bool added = false;
            foreach (var dir in dirs)
            {
                var cand = from.Neighbor(dir);
                if (!landSet.Contains(cand) || chainSet.Contains(cand)) continue;
                if (FormsTriangle(cand, chainSet)) continue;

                chain.Add(cand);
                chainSet.Add(cand);
                added = true;
                break;
            }

            if (!added) return null;
        }

        return chain;
    }

    // Vrai si `candidate` est adjacent à deux hexs de `existing` qui sont eux-mêmes mutuellement
    // adjacents (les 3 hexs formeraient alors un Vertex/triangle).
    private static bool FormsTriangle(HexCoord candidate, HashSet<HexCoord> existing)
    {
        var touching = HexDirectionUtils.AllHexDirections
            .Select(candidate.Neighbor)
            .Where(existing.Contains)
            .ToList();

        for (int i = 0; i < touching.Count; i++)
            for (int j = i + 1; j < touching.Count; j++)
                if (HexDirectionUtils.AllHexDirections.Any(d => touching[i].Neighbor(d).Equals(touching[j])))
                    return true;

        return false;
    }
}
