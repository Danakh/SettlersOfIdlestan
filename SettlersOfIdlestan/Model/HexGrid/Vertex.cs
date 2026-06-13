using System;
using System.Linq;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// ReprÃ©sente un sommet (vertex) partagÃ© par plusieurs hexagones.
/// 
/// Un sommet est un point d'intersection gÃ©omÃ©trique entre trois cellules
/// hexagonales mutuellement adjacentes. Cette abstraction est indÃ©pendante
/// de tout usage mÃ©tier (bÃ¢timents, nÅ“uds de technologies, etc.).
/// 
/// Un sommet est identifiÃ© de maniÃ¨re unique par trois hexagones adjacents
/// qui se rencontrent Ã  ce point. L'ordre des hexagones est normalisÃ© pour
/// garantir l'unicitÃ©.
/// </summary>
[Serializable]
[System.Text.Json.Serialization.JsonConverter(typeof(VertexJsonConverter))]
public class Vertex
{
    private Vertex(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        EnsureSameZ(hex1, hex2, hex3, "create a vertex");

        // Validation: les hexagones doivent former un triangle valide
        if (!IsValidTriangle(hex1, hex2, hex3))
        {
            throw new ArgumentException("Triangle invalide");
        }

        Hex1 = hex1;
        Hex2 = hex2;
        Hex3 = hex3;
    }

    public HexCoord Hex1 { get; private set; }
    public HexCoord Hex2 { get; private set; }
    public HexCoord Hex3 { get; private set; }
    public int Z => Hex1.Z;

    /// <summary>
    /// CrÃ©e un sommet Ã  partir de trois hexagones adjacents.
    /// Normalise l'ordre pour garantir l'unicitÃ©.
    /// </summary>
    public static Vertex Create(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        var normalized = Normalize(hex1, hex2, hex3);
        return new Vertex(normalized[0], normalized[1], normalized[2]);
    }

    /// <summary>
    /// VÃ©rifie si trois hexagones forment un triangle valide (se rencontrent Ã  un sommet).
    /// Dans une grille hexagonale, trois hexagones se rencontrent Ã  un sommet si et seulement si
    /// ils sont tous mutuellement adjacents (distance 1 entre chaque paire).
    /// </summary>
    private static bool IsValidTriangle(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        var d12 = hex1.DistanceTo(hex2);
        var d13 = hex1.DistanceTo(hex3);
        var d23 = hex2.DistanceTo(hex3);

        // Les trois hexagones doivent Ãªtre mutuellement adjacents
        return d12 == 1 && d13 == 1 && d23 == 1;
    }

    /// <summary>
    /// Normalise l'ordre de trois coordonnÃ©es pour garantir l'unicitÃ©.
    /// Trie par q puis r.
    /// </summary>
    private static HexCoord[] Normalize(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        var hexes = new[] { hex1, hex2, hex3 };
        Array.Sort(hexes, (a, b) =>
        {
            if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
            if (a.Q != b.Q) return a.Q.CompareTo(b.Q);
            return a.R.CompareTo(b.R);
        });
        return hexes;
    }

    /// <summary>
    /// VÃ©rifie si ce sommet est Ã©gal Ã  un autre.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Vertex other) return false;
        var thisHexes = Normalize(Hex1, Hex2, Hex3);
        var otherHexes = Normalize(other.Hex1, other.Hex2, other.Hex3);

        return thisHexes[0].Equals(otherHexes[0]) &&
               thisHexes[1].Equals(otherHexes[1]) &&
               thisHexes[2].Equals(otherHexes[2]);
    }

    /// <summary>
    /// Retourne les trois hexagones de ce sommet.
    /// </summary>
    public HexCoord[] GetHexes()
    {
        return new[] { Hex1, Hex2, Hex3 };
    }

    /// <summary>
    /// VÃ©rifie si ce sommet est adjacent Ã  un hexagone donnÃ©.
    /// </summary>
    public bool IsAdjacentTo(HexCoord hex)
    {
        return Hex1.Equals(hex) || Hex2.Equals(hex) || Hex3.Equals(hex);
    }

    /// <summary>
    /// Retourne les trois sommets voisins (Ã  distance d'un edge).
    /// Chaque voisin est l'autre sommet de l'une des trois arÃªtes formÃ©es par les paires de hex de ce sommet.
    /// </summary>
    public Vertex[] GetAdjacentVertices()
    {
        var hexes = GetHexes();
        return new[]
        {
            Edge.Create(hexes[0], hexes[1]).OtherVertex(this),
            Edge.Create(hexes[1], hexes[2]).OtherVertex(this),
            Edge.Create(hexes[0], hexes[2]).OtherVertex(this),
        };
    }

    /// <summary>
    /// VÃ©rifie si ce sommet est adjacent Ã  un autre sommet (distance d'un edge).
    /// </summary>
    public bool IsAdjacentTo(Vertex other) => EdgeDistanceTo(other) == 1;

    public bool HasSameZ(Vertex other)
    {
        return Z == other.Z;
    }

    public void EnsureSameZ(Vertex other, string operation)
    {
        if (!HasSameZ(other))
        {
            throw new ArgumentException(
                $"Cannot {operation} across different map layers: {this} and {other}");
        }
    }

    /// <summary>
    /// Retourne une reprÃ©sentation en chaÃ®ne pour le dÃ©bogage.
    /// </summary>
    public override string ToString()
    {
        return $"Vertex({Hex1}, {Hex2}, {Hex3})";
    }

    /// <summary>
    /// GÃ©nÃ¨re un hash pour utiliser comme clÃ© dans des Maps/Sets.
    /// </summary>
    public override int GetHashCode()
    {
        var normalized = Normalize(Hex1, Hex2, Hex3);
        unchecked
        {
            return (normalized[0].GetHashCode() * 31 + normalized[1].GetHashCode()) * 31 + normalized[2].GetHashCode();
        }
    }

    /// <summary>
    /// Retourne l'hexagone prÃ©sent dans cette direction, s'il existe.
    /// 
    /// Si direction = N (Nord), retourne l'hexagone qui a ce vertex dans sa direction S (Sud).
    /// 
    /// Cet hexagone doit Ãªtre l'un des trois hexagones du vertex et doit avoir ce vertex
    /// comme l'un de ses sommets dans la direction opposÃ©e (direction inverse).
    /// </summary>
    public HexCoord? Hex(SecondaryHexDirection direction)
    {
        // DÃ©terminer la direction inverse
        var oppositeDirection = SecondaryHexDirectionUtils.InverseSecondaryHexDirection(direction);

        // Chercher lequel des 3 hexagones a ce vertex dans la direction inverse
        var hexes = GetHexes();
        foreach (var hexCoord in hexes)
        {
            // CrÃ©er le vertex depuis cet hex dans la direction inverse
            // et vÃ©rifier si c'est ce vertex
            try
            {
                var vertexInOppositeDir = hexCoord.Vertex(oppositeDirection);
                if (vertexInOppositeDir.Equals(this))
                {
                    return hexCoord;
                }
            }
            catch
            {
                // Ignorer les erreurs de crÃ©ation de vertex (hex invalides)
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Distance entre ce vertex et un autre vertex, dÃ©finie comme le nombre d'edges Ã  parcourir pour aller de l'un Ã  l'autre.
    /// </summary>
    public int EdgeDistanceTo(Vertex other)
    {
        EnsureSameZ(other, nameof(EdgeDistanceTo));

        var thisCubeSum = CubeSum();
        var otherCubeSum = other.CubeSum();
        var delta = (
            X: otherCubeSum.X - thisCubeSum.X,
            Y: otherCubeSum.Y - thisCubeSum.Y,
            Z: otherCubeSum.Z - thisCubeSum.Z
        );

        var thisResidue = PositiveModulo(thisCubeSum.X, 3);
        var otherResidue = PositiveModulo(otherCubeSum.X, 3);

        if (thisResidue == otherResidue)
        {
            return 2 * CubeDistance(DivideByThree(delta));
        }

        var stepSign = thisResidue == 2 ? 1 : -1;
        var possibleFirstSteps = new[]
        {
            (X: 2, Y: -1, Z: -1),
            (X: -1, Y: 2, Z: -1),
            (X: -1, Y: -1, Z: 2),
        };

        return 1 + 2 * possibleFirstSteps
            .Select(step => CubeDistance(DivideByThree((
                X: delta.X - stepSign * step.X,
                Y: delta.Y - stepSign * step.Y,
                Z: delta.Z - stepSign * step.Z
            ))))
            .Min();
    }

    private (int X, int Y, int Z) CubeSum()
    {
        var x = Hex1.Q + Hex2.Q + Hex3.Q;
        var z = Hex1.R + Hex2.R + Hex3.R;
        var y = -x - z;
        return (x, y, z);
    }

    private static (int X, int Y, int Z) DivideByThree((int X, int Y, int Z) cube)
    {
        return (cube.X / 3, cube.Y / 3, cube.Z / 3);
    }

    private static int CubeDistance((int X, int Y, int Z) cube)
    {
        return (Math.Abs(cube.X) + Math.Abs(cube.Y) + Math.Abs(cube.Z)) / 2;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        return ((value % modulo) + modulo) % modulo;
    }

    /// <summary>
    /// SÃ©rialise le sommet en [h1, h2, h3] (chaque hi = [q, r]).
    /// </summary>
    public int[][] Serialize()
    {
        return GetHexes().Select(h => h.Serialize()).ToArray();
    }

    /// <summary>
    /// DÃ©sÃ©rialise depuis [[q1,r1],[q2,r2],[q3,r3]].
    /// </summary>
    public static Vertex Deserialize(int[][] data)
    {
        return Create(
            HexCoord.Deserialize(data[0]),
            HexCoord.Deserialize(data[1]),
            HexCoord.Deserialize(data[2])
        );
    }

    private static void EnsureSameZ(HexCoord hex1, HexCoord hex2, HexCoord hex3, string operation)
    {
        hex1.EnsureSameZ(hex2, operation);
        hex1.EnsureSameZ(hex3, operation);
    }
}
