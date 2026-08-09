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
public class Vertex : IEquatable<Vertex>
{
    // Hash pré-calculé : un Vertex est immuable, et il sert massivement de clé de dictionnaire
    // (graphes de routes, pathfinding). Le recalculer à chaque lookup coûtait 3 hash de HexCoord.
    private readonly int _hashCode;

    private Vertex(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        ValidateConstruction(hex1, hex2, hex3);

        Hex1 = hex1;
        Hex2 = hex2;
        Hex3 = hex3;

        unchecked
        {
            _hashCode = (hex1.GetHashCode() * 31 + hex2.GetHashCode()) * 31 + hex3.GetHashCode();
        }
    }

    /// <summary>
    /// Validations de construction coÃ»teuses, dÃ©sactivÃ©es hors DEBUG.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private static void ValidateConstruction(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        EnsureSameZ(hex1, hex2, hex3, "create a vertex");

        // Validation: les hexagones doivent former un triangle valide
        if (!IsValidTriangle(hex1, hex2, hex3))
        {
            throw new ArgumentException("Triangle invalide");
        }
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
        var (h1, h2, h3) = Normalize(hex1, hex2, hex3);
        return new Vertex(h1, h2, h3);
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
    private static (HexCoord, HexCoord, HexCoord) Normalize(HexCoord a, HexCoord b, HexCoord c)
    {
        static int Cmp(HexCoord x, HexCoord y)
        {
            if (x.Z != y.Z) return x.Z - y.Z;
            if (x.Q != y.Q) return x.Q - y.Q;
            return x.R - y.R;
        }
        if (Cmp(a, b) > 0) (a, b) = (b, a);
        if (Cmp(b, c) > 0) (b, c) = (c, b);
        if (Cmp(a, b) > 0) (a, b) = (b, a);
        return (a, b, c);
    }

    /// <summary>
    /// VÃ©rifie si ce sommet est Ã©gal Ã  un autre.
    /// </summary>
    public bool Equals(Vertex? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        // Rejet rapide : deux Vertex égaux ont forcément le même hash (pré-calculé).
        return _hashCode == other._hashCode &&
               Hex1.Equals(other.Hex1) &&
               Hex2.Equals(other.Hex2) &&
               Hex3.Equals(other.Hex3);
    }

    public override bool Equals(object? obj) => Equals(obj as Vertex);

    [NonSerialized]
    private HexCoord[]? _hexes;

    /// <summary>
    /// Retourne les trois hexagones de ce sommet.
    ///
    /// <para>Le tableau est mémoïsé — un Vertex est immuable — et <b>partagé</b> entre tous les
    /// appelants : ne jamais le muter. Cette méthode est appelée dans presque toutes les boucles par
    /// tick (visibilité, récolte, distances de route, pathfinding) et allouait auparavant un tableau
    /// à chaque appel ; les Vertex à longue durée de vie (position des villes, sommets des routes)
    /// ne paient donc plus qu'une seule allocation. Volontairement pas de mémoïsation sur
    /// <see cref="GetAdjacentVertices"/> : elle retiendrait des références vers d'autres Vertex, et
    /// un parcours du réseau routier finirait par retenir tout le graphe.</para>
    /// </summary>
    public HexCoord[] GetHexes() => _hexes ??= new[] { Hex1, Hex2, Hex3 };

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
        return
        [
            Edge.Create(Hex1, Hex2).OtherVertex(this),
            Edge.Create(Hex2, Hex3).OtherVertex(this),
            Edge.Create(Hex1, Hex3).OtherVertex(this),
        ];
    }

    /// <summary>
    /// VÃ©rifie si ce sommet est adjacent Ã  un autre sommet (distance d'un edge).
    /// </summary>
    public bool IsAdjacentTo(Vertex other) => EdgeDistanceTo(other) == 1;

    public bool HasSameZ(Vertex other)
    {
        return Z == other.Z;
    }

    [System.Diagnostics.Conditional("DEBUG")]
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
    public override int GetHashCode() => _hashCode;

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
        int dx = otherCubeSum.X - thisCubeSum.X;
        int dy = otherCubeSum.Y - thisCubeSum.Y;
        int dz = otherCubeSum.Z - thisCubeSum.Z;

        var thisResidue = PositiveModulo(thisCubeSum.X, 3);
        var otherResidue = PositiveModulo(otherCubeSum.X, 3);

        if (thisResidue == otherResidue)
        {
            return 2 * ThirdCubeDistance(dx, dy, dz);
        }

        // Les deux sommets appartiennent à des sous-réseaux différents : un premier pas est
        // obligatoire. On déroule les trois pas possibles — l'ancienne version passait par un
        // tableau + LINQ, ce qui allouait à chaque appel alors que la méthode est appelée en boucle.
        int stepSign = thisResidue == 2 ? 1 : -1;

        int best = ThirdCubeDistance(dx - 2 * stepSign, dy + stepSign, dz + stepSign);
        int alt = ThirdCubeDistance(dx + stepSign, dy - 2 * stepSign, dz + stepSign);
        if (alt < best) best = alt;
        alt = ThirdCubeDistance(dx + stepSign, dy + stepSign, dz - 2 * stepSign);
        if (alt < best) best = alt;

        return 1 + 2 * best;
    }

    /// <summary>
    /// Distance cubique du tiers du delta fourni (le delta est toujours un multiple de 3 ici).
    /// </summary>
    private static int ThirdCubeDistance(int x, int y, int z)
    {
        x /= 3;
        y /= 3;
        z /= 3;
        return (Math.Abs(x) + Math.Abs(y) + Math.Abs(z)) / 2;
    }

    private (int X, int Y, int Z) CubeSum()
    {
        var x = Hex1.Q + Hex2.Q + Hex3.Q;
        var z = Hex1.R + Hex2.R + Hex3.R;
        var y = -x - z;
        return (x, y, z);
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

    [System.Diagnostics.Conditional("DEBUG")]
    private static void EnsureSameZ(HexCoord hex1, HexCoord hex2, HexCoord hex3, string operation)
    {
        hex1.EnsureSameZ(hex2, operation);
        hex1.EnsureSameZ(hex3, operation);
    }
}
