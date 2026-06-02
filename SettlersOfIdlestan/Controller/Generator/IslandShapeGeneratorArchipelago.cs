using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

public class IslandShapeGeneratorArchipelago : IslandShapeGenerator
{
    private readonly GamePRNG _prng;
    private const int TargetIslandSize = 15;

    public IslandShapeGeneratorArchipelago(GamePRNG prng)
    {
        _prng = prng;
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer)
    {
        if (count < 4)
            return new IslandShapeGeneratorCompact(_prng).GenerateCoords(count, layer);

        int numIslands = Math.Max(2, count / TargetIslandSize);
        int[] islandSizes = DistributeSizes(count, numIslands);

        var allLand = new HashSet<HexCoord>();
        var usedWater = new HashSet<HexCoord>();
        var result = new List<HexCoord>(count);

        var origin = new HexCoord(0, 0, layer);
        var currentIsland = new List<HexCoord> { origin };
        allLand.Add(origin);
        GrowIsland(currentIsland, islandSizes[0], allLand, usedWater, null);
        result.AddRange(currentIsland);

        for (int i = 1; i < numIslands; i++)
        {
            var prevLand = new HashSet<HexCoord>(allLand);

            if (!TryFindPassage(currentIsland, allLand, usedWater,
                    out var w1, out var w2, out var w3, out var m1, out var m2))
                break;

            usedWater.Add(w1);
            usedWater.Add(w2);
            usedWater.Add(w3);

            var newIsland = new List<HexCoord> { m1, m2 };
            allLand.Add(m1);
            allLand.Add(m2);

            GrowIsland(newIsland, islandSizes[i], allLand, usedWater, prevLand);
            result.AddRange(newIsland);
            currentIsland = newIsland;
        }

        return result;
    }

    private static int[] DistributeSizes(int total, int n)
    {
        var sizes = new int[n];
        int baseSize = total / n;
        int remainder = total % n;
        for (int i = 0; i < n; i++)
            sizes[i] = baseSize + (i < remainder ? 1 : 0);
        return sizes;
    }

    // Croissance itérative : hex aléatoire → tous ses voisins null, jusqu'au quota.
    private void GrowIsland(List<HexCoord> island, int targetSize,
        HashSet<HexCoord> allLand, HashSet<HexCoord> usedWater, HashSet<HexCoord>? prevLand)
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
                if (usedWater.Contains(nb)) continue;
                if (prevLand != null && IsAdjacentTo(nb, prevLand)) continue;

                island.Add(nb);
                allLand.Add(nb);
                addedAny = true;
            }

            stuckCount = addedAny ? 0 : stuckCount + 1;
        }
    }

    // Cherche une configuration : L-L' (terre) / W1-W2-W3 (eau) / M1-M2 (départ île suivante).
    // Garantit 2 routes maritimes : Edge(W1,W2) et Edge(W2,W3).
    private bool TryFindPassage(
        List<HexCoord> island, HashSet<HexCoord> allLand, HashSet<HexCoord> usedWater,
        out HexCoord w1, out HexCoord w2, out HexCoord w3, out HexCoord m1, out HexCoord m2)
    {
        w1 = w2 = w3 = m1 = m2 = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        var islandSet = new HashSet<HexCoord>(island);

        var pairs = new List<(HexCoord L, HexCoord Lp)>();
        foreach (var L in island)
        {
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var Lp = L.Neighbor(dir);
                if (islandSet.Contains(Lp) && (L.Q < Lp.Q || (L.Q == Lp.Q && L.R < Lp.R)))
                    pairs.Add((L, Lp));
            }
        }
        _prng.Shuffle(pairs);

        foreach (var (L, Lp) in pairs)
        {
            var (cn1, cn2) = GetCommonNeighbors(L, Lp);

            foreach (var W2cand in new[] { cn1, cn2 })
            {
                if (allLand.Contains(W2cand) || usedWater.Contains(W2cand)) continue;

                // W1 : voisin commun de L et W2, != Lp, libre
                var (a1, a2) = GetCommonNeighbors(L, W2cand);
                HexCoord? W1 = null;
                foreach (var c in new[] { a1, a2 })
                    if (!c.Equals(Lp) && !allLand.Contains(c) && !usedWater.Contains(c))
                    { W1 = c; break; }
                if (W1 == null) continue;

                // W3 : voisin commun de Lp et W2, != L, libre
                var (b1, b2) = GetCommonNeighbors(Lp, W2cand);
                HexCoord? W3 = null;
                foreach (var c in new[] { b1, b2 })
                    if (!c.Equals(L) && !allLand.Contains(c) && !usedWater.Contains(c))
                    { W3 = c; break; }
                if (W3 == null) continue;

                // M1 : voisin commun de W1 et W2, != L, libre, non adjacent à allLand
                var (d1, d2) = GetCommonNeighbors(W1, W2cand);
                HexCoord? M1 = null;
                foreach (var c in new[] { d1, d2 })
                    if (!c.Equals(L) && !allLand.Contains(c) && !usedWater.Contains(c)
                        && !IsAdjacentTo(c, allLand))
                    { M1 = c; break; }
                if (M1 == null) continue;

                // M2 : voisin commun de W3 et W2, != Lp, libre, non adjacent à allLand
                var (e1, e2) = GetCommonNeighbors(W3, W2cand);
                HexCoord? M2 = null;
                foreach (var c in new[] { e1, e2 })
                    if (!c.Equals(Lp) && !allLand.Contains(c) && !usedWater.Contains(c)
                        && !IsAdjacentTo(c, allLand))
                    { M2 = c; break; }
                if (M2 == null) continue;

                // M1 et M2 ne doivent pas être adjacents l'un à l'autre via la terre
                // (ils seront dans la même île, pas de problème de connectivité)
                w1 = W1; w2 = W2cand; w3 = W3; m1 = M1; m2 = M2;
                return true;
            }
        }

        return false;
    }

    // Retourne les 2 hexs voisins communs de A et B (A et B sont adjacents).
    private static (HexCoord, HexCoord) GetCommonNeighbors(HexCoord A, HexCoord B)
    {
        HexCoord first = new HexCoord(0, 0, IslandMap.SurfaceLayer), second = new HexCoord(0, 0, IslandMap.SurfaceLayer);
        bool foundFirst = false;

        foreach (var dir in HexDirectionUtils.AllHexDirections)
        {
            var nb = A.Neighbor(dir);
            if (nb.Equals(B)) continue;
            foreach (var dir2 in HexDirectionUtils.AllHexDirections)
            {
                if (B.Neighbor(dir2).Equals(nb))
                {
                    if (!foundFirst) { first = nb; foundFirst = true; }
                    else { second = nb; return (first, second); }
                    break;
                }
            }
        }
        return (first, second);
    }

    private static bool IsAdjacentTo(HexCoord hex, HashSet<HexCoord> set)
    {
        foreach (var dir in HexDirectionUtils.AllHexDirections)
            if (set.Contains(hex.Neighbor(dir))) return true;
        return false;
    }

    public override HexCoord? GetPreferredStartHex(IReadOnlyList<HexCoord> coords)
        => coords.Count > 0
            ? coords.OrderBy(c => c.Q).ThenBy(c => Math.Abs(c.R)).First()
            : null;
}
