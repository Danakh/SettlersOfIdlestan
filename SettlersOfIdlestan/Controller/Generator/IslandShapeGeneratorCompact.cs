using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates a compact (roughly circular) island shape using a spiral coordinate layout.
/// </summary>
public class IslandShapeGeneratorCompact : IslandShapeGenerator
{
    public override IReadOnlyList<HexCoord> GenerateCoords(int count)
    {
        var result = new List<HexCoord>(count);
        foreach (var coord in GenerateSpiralCoords(count))
            result.Add(coord);
        return result;
    }

    private static IEnumerable<HexCoord> GenerateSpiralCoords(int count)
    {
        if (count <= 0) yield break;
        yield return new HexCoord(0, 0);
        if (count == 1) yield break;

        int radius = 1;
        int yielded = 1;
        while (yielded < count)
        {
            foreach (var coord in GenerateRingCoords(radius))
            {
                yield return coord;
                yielded++;
                if (yielded >= count) yield break;
            }
            radius++;
        }
    }

    private static IEnumerable<HexCoord> GenerateRingCoords(int radius)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Math.Max(-radius, -q - radius);
            int r2 = Math.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                int s = -q - r;
                if (Math.Abs(q) + Math.Abs(r) + Math.Abs(s) == 2 * radius)
                    yield return new HexCoord(q, r);
            }
        }
    }
}
