using System.Linq;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Représente un sommet (vertex) partagé par plusieurs hexagones.
/// 
/// Un sommet est un point d'intersection géométrique entre trois cellules
/// hexagonales mutuellement adjacentes. Cette abstraction est indépendante
/// de tout usage métier (bâtiments, nœuds de technologies, etc.).
/// 
/// Un sommet est identifié de manière unique par trois hexagones adjacents
/// qui se rencontrent à ce point. L'ordre des hexagones est normalisé pour
/// garantir l'unicité.
/// </summary>
public class Vertex
{
    private Vertex(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        // Validation: les hexagones doivent former un triangle valide
        if (!IsValidTriangle(hex1, hex2, hex3))
        {
            throw new ArgumentException("Triangle invalide");
        }

        Hex1 = hex1;
        Hex2 = hex2;
        Hex3 = hex3;
    }

    public HexCoord Hex1 { get; }
    public HexCoord Hex2 { get; }
    public HexCoord Hex3 { get; }

    /// <summary>
    /// Crée un sommet à partir de trois hexagones adjacents.
    /// Normalise l'ordre pour garantir l'unicité.
    /// </summary>
    public static Vertex Create(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        var normalized = Normalize(hex1, hex2, hex3);
        return new Vertex(normalized[0], normalized[1], normalized[2]);
    }

    /// <summary>
    /// Vérifie si trois hexagones forment un triangle valide (se rencontrent à un sommet).
    /// Dans une grille hexagonale, trois hexagones se rencontrent à un sommet si et seulement si
    /// ils sont tous mutuellement adjacents (distance 1 entre chaque paire).
    /// </summary>
    private static bool IsValidTriangle(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        var d12 = hex1.DistanceTo(hex2);
        var d13 = hex1.DistanceTo(hex3);
        var d23 = hex2.DistanceTo(hex3);

        // Les trois hexagones doivent être mutuellement adjacents
        return d12 == 1 && d13 == 1 && d23 == 1;
    }

    /// <summary>
    /// Normalise l'ordre de trois coordonnées pour garantir l'unicité.
    /// Trie par q puis r.
    /// </summary>
    private static HexCoord[] Normalize(HexCoord hex1, HexCoord hex2, HexCoord hex3)
    {
        var hexes = new[] { hex1, hex2, hex3 };
        Array.Sort(hexes, (a, b) =>
        {
            if (a.Q != b.Q) return a.Q.CompareTo(b.Q);
            return a.R.CompareTo(b.R);
        });
        return hexes;
    }

    /// <summary>
    /// Vérifie si ce sommet est égal à un autre.
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
        return [Hex1, Hex2, Hex3];
    }

    /// <summary>
    /// Vérifie si ce sommet est adjacent à un hexagone donné.
    /// </summary>
    public bool IsAdjacentTo(HexCoord hex)
    {
        return Hex1.Equals(hex) || Hex2.Equals(hex) || Hex3.Equals(hex);
    }

    /// <summary>
    /// Retourne une représentation en chaîne pour le débogage.
    /// </summary>
    public override string ToString()
    {
        return $"Vertex({Hex1}, {Hex2}, {Hex3})";
    }

    /// <summary>
    /// Génère un hash pour utiliser comme clé dans des Maps/Sets.
    /// </summary>
    public override int GetHashCode()
    {
        var normalized = Normalize(Hex1, Hex2, Hex3);
        return HashCode.Combine(normalized[0], normalized[1], normalized[2]);
    }

    /// <summary>
    /// Retourne l'hexagone présent dans cette direction, s'il existe.
    /// 
    /// Si direction = N (Nord), retourne l'hexagone qui a ce vertex dans sa direction S (Sud).
    /// 
    /// Cet hexagone doit être l'un des trois hexagones du vertex et doit avoir ce vertex
    /// comme l'un de ses sommets dans la direction opposée (direction inverse).
    /// </summary>
    public HexCoord? Hex(SecondaryHexDirection direction)
    {
        // Déterminer la direction inverse
        var oppositeDirection = SecondaryHexDirectionUtils.InverseSecondaryHexDirection(direction);

        // Chercher lequel des 3 hexagones a ce vertex dans la direction inverse
        var hexes = GetHexes();
        foreach (var hexCoord in hexes)
        {
            // Créer le vertex depuis cet hex dans la direction inverse
            // et vérifier si c'est ce vertex
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
                // Ignorer les erreurs de création de vertex (hex invalides)
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Sérialise le sommet en [h1, h2, h3] (chaque hi = [q, r]).
    /// </summary>
    public int[][] Serialize()
    {
        return GetHexes().Select(h => h.Serialize()).ToArray();
    }

    /// <summary>
    /// Désérialise depuis [[q1,r1],[q2,r2],[q3,r3]].
    /// </summary>
    public static Vertex Deserialize(int[][] data)
    {
        return Create(
            HexCoord.Deserialize(data[0]),
            HexCoord.Deserialize(data[1]),
            HexCoord.Deserialize(data[2])
        );
    }
}