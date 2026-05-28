using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates two islands separated by water with a guaranteed maritime route connection.
///
/// Island 1 (left): complete spiral disc, ensuring its rightmost hex (R1, 0) exists.
/// Island 2 (right): organic BFS blob within a bounding disc, seeded for shape variation.
/// R1 is chosen to minimise the tile-count difference between the two islands.
///
/// After positioning:
///   IS1 shifted by -(R1+1, 0) → IS1's rightmost hex lands at (-1,  0)
///   IS2 shifted by  (R2+1,-1) → IS2's leftmost  hex lands at ( 1, -1)
///   No bridge — the islands are separated by water.
///   Guaranteed maritime edge: Edge((0,-1),(0,0)) because IS1's (-1,0) and IS2's (1,-1)
///   both share the water neighbors (0,0) and (0,-1).
/// </summary>
public class IslandShapeGeneratorArchipelago : IslandShapeGenerator
{
    private readonly GamePRNG _prng;

    public IslandShapeGeneratorArchipelago(GamePRNG prng)
    {
        _prng = prng;
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count)
    {
        if (count < 8)
            return new IslandShapeGeneratorCompact().GenerateCoords(count);

        // Choose R1 (full disc for IS1) that minimises |N1 - N2|.
        // On ties prefer the larger R1 so IS1's tip stays far from the maritime strait.
        int R1 = 0, bestDiff = int.MaxValue;
        for (int r = 0; DiscSize(r) < count; r++)
        {
            int n1 = DiscSize(r);
            int n2 = count - n1;
            if (n2 <= 0) break;
            int diff = System.Math.Abs(n1 - n2);
            if (diff <= bestDiff) { bestDiff = diff; R1 = r; }
        }

        int N1 = DiscSize(R1);

        if (N1 >= count)
        {
            if (R1 > 0) { R1--; N1 = DiscSize(R1); }
            else return new IslandShapeGeneratorCompact().GenerateCoords(count);
        }

        int N2 = count - N1;

        // R2: smallest radius whose disc contains at least N2 tiles (bounding disc for blob)
        int R2 = 0;
        while (DiscSize(R2) < N2) R2++;

        var compact = new IslandShapeGeneratorCompact();
        var is1 = compact.GenerateCoords(N1);                        // full disc → local (R1, 0) present
        // Two seeds guarantee two maritime edges after positioning:
        //   (-R2, 0)  → global (1,-1)  → shared vertex with IS1 via Edge((0,-1),(0,0))
        //   (-R2,-1)  → global (1,-2)  → shared vertex with IS1 via Edge((0,-2),(0,-1))
        var is2Seeds = new[] { new HexCoord(-R2, 0), new HexCoord(-R2, -1) };
        var is2 = GenerateOrganicBlob(N2, is2Seeds, R2);

        int dq1 = -(R1 + 1); // IS1: local (R1, 0) → global (-1,  0)
        int dq2 =   R2 + 1;  // IS2: local (-R2, 0) → global ( 1, -1)

        var result = new List<HexCoord>(count);

        foreach (var c in is1)
            result.Add(new HexCoord(c.Q + dq1, c.R));

        foreach (var c in is2)
            result.Add(new HexCoord(c.Q + dq2, c.R - 1));

        return result;
    }

    /// <summary>
    /// Grows a connected blob of <paramref name="count"/> hexes via BFS from
    /// <paramref name="start"/>, constrained to the disc of <paramref name="maxRadius"/>
    /// centred at origin. With a seeded PRNG the frontier is sampled randomly for organic
    /// shapes; without PRNG it falls back to deterministic breadth-first order.
    /// </summary>
    private IReadOnlyList<HexCoord> GenerateOrganicBlob(int count, HexCoord[] seeds, int maxRadius)
    {
        var origin = new HexCoord(0, 0);
        var result = new List<HexCoord>(count);
        var inSet = new HashSet<HexCoord>();
        var frontier = new List<HexCoord>();

        // Seeds are added to result first before any BFS expansion, so they are always present.
        // Phase 1: claim all seeds (register them before expanding neighbors, because seeds can be adjacent).
        foreach (var seed in seeds)
        {
            if (result.Count >= count) break;
            if (seed.DistanceTo(origin) <= maxRadius && inSet.Add(seed))
                result.Add(seed);
        }

        // Phase 2: expand neighbors of every seed that was accepted.
        foreach (var seed in seeds)
        {
            if (!inSet.Contains(seed)) continue;
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = seed.Neighbor(dir);
                if (nb.DistanceTo(origin) <= maxRadius && inSet.Add(nb))
                    frontier.Add(nb);
            }
        }

        while (result.Count < count && frontier.Count > 0)
        {
            int idx = _prng != null ? _prng.Next(frontier.Count) : 0;
            var current = frontier[idx];
            frontier.RemoveAt(idx);
            result.Add(current);

            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = current.Neighbor(dir);
                if (nb.DistanceTo(origin) <= maxRadius && inSet.Add(nb))
                    frontier.Add(nb);
            }
        }

        return result;
    }

    // Preferred start: westernmost hex of IS1, far from the maritime strait near Q=0
    public override HexCoord? GetPreferredStartHex(IReadOnlyList<HexCoord> coords)
        => coords.Count > 0
            ? coords.OrderBy(c => c.Q).ThenBy(c => System.Math.Abs(c.R)).First()
            : null;

    private static int DiscSize(int R) => 3 * R * R + 3 * R + 1;
}
