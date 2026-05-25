using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Generates two compact circular islands connected by a single bridge hex.
///
/// Island 1 (left) always uses a complete spiral disc so that its rightmost hex (R1,0)
/// is guaranteed to exist. Island 2 (right) fills the remaining tiles.
/// After positioning:
///   IS1 shifted by -(R1+1,0)  →  IS1's rightmost lands at (-1, 0)
///   IS2 shifted by  (R2+1, 0) →  IS2's leftmost  lands at ( 1, 0)
///   Bridge hex at (0, 0) connects both.
/// </summary>
public class IslandShapeGeneratorArchipelago : IslandShapeGenerator
{
    public override IReadOnlyList<HexCoord> GenerateCoords(int count)
    {
        if (count < 3)
            return new IslandShapeGeneratorCompact().GenerateCoords(count);

        // Island 1: full disc of radius R1 ≈ half the tiles
        int R1 = 0;
        while (DiscSize(R1 + 1) <= (count - 1) / 2) R1++;
        int N1 = DiscSize(R1);

        // Ensure at least 1 tile remains for island 2
        if (N1 + 1 >= count)
        {
            if (R1 > 0) { R1--; N1 = DiscSize(R1); }
            else return new IslandShapeGeneratorCompact().GenerateCoords(count);
        }

        int N2 = count - 1 - N1; // bridge takes 1

        // R2: smallest radius such that disc(R2) >= N2 (guarantees (-R2,0) is generated)
        int R2 = 0;
        while (DiscSize(R2) < N2) R2++;

        var compact = new IslandShapeGeneratorCompact();
        var is1 = compact.GenerateCoords(N1); // full disc → (R1,0) always present
        var is2 = compact.GenerateCoords(N2); // partial ok → (-R2,0) always present

        // IS1 shift: local (R1,0) → global (-1,0)
        int dq1 = -(R1 + 1);
        // IS2 shift: local (-R2,0) → global (1,0)
        int dq2 = R2 + 1;

        var result = new List<HexCoord>(count);

        foreach (var c in is1)
            result.Add(new HexCoord(c.Q + dq1, c.R));

        result.Add(new HexCoord(0, 0)); // bridge

        foreach (var c in is2)
            result.Add(new HexCoord(c.Q + dq2, c.R));

        return result;
    }

    // Preferred start: westernmost hex of island 1, away from island 2
    public override HexCoord? GetPreferredStartHex(IReadOnlyList<HexCoord> coords)
        => coords.Count > 0
            ? coords.OrderBy(c => c.Q).ThenBy(c => System.Math.Abs(c.R)).First()
            : null;

    // Number of hexes in a complete disc of radius R
    private static int DiscSize(int R) => 3 * R * R + 3 * R + 1;
}
