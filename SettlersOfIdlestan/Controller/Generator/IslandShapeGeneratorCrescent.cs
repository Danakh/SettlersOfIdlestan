using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates a crescent-shaped island. The crescent opens toward the East: a large outer arc
/// minus an inner bite on the East side. Tile order is outer-arc-first via a BFS priority queue,
/// guaranteeing connectivity.
/// </summary>
public class IslandShapeGeneratorCrescent : IslandShapeGenerator
{
    public override IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer)
    {
        if (count <= 0) return [];

        // Choose outer radius so the disc holds at least 2× the needed tiles
        int R = 2;
        while (3 * R * R + 3 * R + 1 < count * 2) R++;

        // Bite: offset to the East, radius ≈ 55% of R
        int biteQ = (R + 1) / 2;
        int biteRadius = Math.Max(1, (int)Math.Round(R * 0.55));
        var biteCenter = new HexCoord(biteQ, 0, layer);
        var origin = new HexCoord(0, 0, layer);

        // Collect all hexes inside the outer disc that are outside the bite
        var validHexes = new HashSet<HexCoord>();
        for (int q = -R; q <= R; q++)
        {
            int rMin = Math.Max(-R, -q - R);
            int rMax = Math.Min(R, -q + R);
            for (int r = rMin; r <= rMax; r++)
            {
                var coord = new HexCoord(q, r, layer);
                if (coord.DistanceTo(biteCenter) > biteRadius)
                    validHexes.Add(coord);
            }
        }

        // Start from the westernmost hex (natural tip of the crescent)
        var startCoord = validHexes
            .OrderBy(h => h.Q)
            .ThenBy(h => Math.Abs(h.R))
            .First();

        // BFS with outer-first priority: (negDist, q, r) — Min gives highest dist from origin
        var result = new List<HexCoord>(count);
        var inQueue = new HashSet<HexCoord> { startCoord };
        var pq = new SortedSet<(int negDist, int q, int r)>
        {
            (-startCoord.DistanceTo(origin), startCoord.Q, startCoord.R)
        };

        while (result.Count < count && pq.Count > 0)
        {
            var item = pq.Min;
            pq.Remove(item);
            var coord = new HexCoord(item.q, item.r, layer);
            result.Add(coord);

            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var neighbor = coord.Neighbor(dir);
                if (validHexes.Contains(neighbor) && inQueue.Add(neighbor))
                    pq.Add((-neighbor.DistanceTo(origin), neighbor.Q, neighbor.R));
            }
        }

        return result;
    }

    // The first generated hex is the crescent tip (westernmost outer-arc hex)
    public override HexCoord? GetPreferredStartHex(IReadOnlyList<HexCoord> coords)
        => coords.Count > 0 ? coords[0] : null;
}
