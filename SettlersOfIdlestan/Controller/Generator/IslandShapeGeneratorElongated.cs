using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates an elongated island with a 2:1 length-to-width ratio.
/// The long axis is picked randomly from the three hex axes (flat-top layout: ~30°, ~90°, ~150°).
/// The preferred start hex is at one extremity of the long axis.
/// </summary>
public class IslandShapeGeneratorElongated : IslandShapeGenerator
{
    private readonly int _axisChoice;

    public IslandShapeGeneratorElongated(GamePRNG prng)
    {
        _axisChoice = prng.Next(3);
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count)
    {
        if (count <= 0) return Array.Empty<HexCoord>();

        int scanRange = (int)Math.Ceiling(1.5 * Math.Sqrt(count)) + 2;

        var candidates = new List<(HexCoord coord, double score)>();
        for (int q = -scanRange; q <= scanRange; q++)
        {
            int rMin = Math.Max(-scanRange, -q - scanRange);
            int rMax = Math.Min(scanRange, -q + scanRange);
            for (int r = rMin; r <= rMax; r++)
                candidates.Add((new HexCoord(q, r, IslandMap.SurfaceLayer), EllipseScore(q, r)));
        }

        return candidates
            .OrderBy(x => x.score)
            .Take(count)
            .Select(x => x.coord)
            .ToList();
    }

    private double EllipseScore(int q, int r)
    {
        // Flat-top hex pixel coordinates (size factor omitted)
        double px = 1.5 * q;
        double py = (Math.Sqrt(3) * 0.5) * q + Math.Sqrt(3) * r;

        // Three hex axes in flat-top layout are at 30°, 90°, 150°
        double angle = _axisChoice switch
        {
            0 => Math.PI / 6,
            1 => Math.PI / 2,
            _ => 5.0 * Math.PI / 6
        };

        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double lx = cos * px + sin * py;   // projection along long axis
        double ly = -sin * px + cos * py;  // projection along short axis

        // 3:1 ellipse: (lx/3)² + ly²
        return (lx / 3.0) * (lx / 3.0) + ly * ly;
    }

    public override HexCoord? GetPreferredStartHex(IReadOnlyList<HexCoord> coords)
    {
        if (coords.Count == 0) return null;

        double angle = _axisChoice switch
        {
            0 => Math.PI / 6,
            1 => Math.PI / 2,
            _ => 5.0 * Math.PI / 6
        };

        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        return coords.OrderBy(c =>
        {
            double px = 1.5 * c.Q;
            double py = (Math.Sqrt(3) * 0.5) * c.Q + Math.Sqrt(3) * c.R;
            return cos * px + sin * py;
        }).First(); // minimum projection = one extremity of the long axis
    }
}
