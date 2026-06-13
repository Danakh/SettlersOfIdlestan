using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Système de coordonnées axiales pour les grilles hexagonales.
/// 
/// Dans ce système, chaque hexagone est identifié par deux coordonnées (q, r):
/// - q: coordonnée colonne (axe horizontal)
/// - r: coordonnée ligne (axe diagonal)
/// 
/// Les voisins d'un hexagone sont obtenus en ajoutant des déplacements prédéfinis
/// selon la direction choisie. Ce système est plus simple que les coordonnées
/// cubiques (q, r, s) car la troisième coordonnée peut être dérivée: s = -q - r
/// </summary>
[Serializable]
[JsonConverter(typeof(HexCoordJsonConverter))]
public class HexCoord
{
    public HexCoord(int q, int r, int z)
    {
        Q = q;
        R = r;
        Z = z;
    }

    public int Q { get; }
    public int R { get; }
    public int Z { get; }

    /// <summary>
    /// Retourne la coordonnée s (dérivée) pour compatibilité avec système cubique.
    /// Dans le système axial, s = -q - r
    /// </summary>
    public int S => -Q - R;

    /// <summary>
    /// Retourne les coordonnées du voisin dans la direction principale spécifiée.
    /// Les déplacements sont définis pour le système de coordonnées axiales.
    /// </summary>
    public HexCoord Neighbor(HexDirection direction)
    {
        var deltas = new Dictionary<HexDirection, (int dq, int dr)>
        {
            { HexDirection.W, (-1, 0) },
            { HexDirection.E, (1, 0) },
            { HexDirection.NE, (0, 1) },
            { HexDirection.SE, (1, -1) },
            { HexDirection.NW, (-1, 1) },
            { HexDirection.SW, (0, -1) },
        };

        var (dq, dr) = deltas[direction];
        return new HexCoord(Q + dq, R + dr, Z);
    }

    /// <summary>
    /// Retourne tous les voisins de cet hexagone en utilisant les directions principales.
    /// </summary>
    public HexCoord[] Neighbors()
    {
        return HexDirectionUtils.AllHexDirections.Select(dir => Neighbor(dir)).ToArray();
    }

    /// <summary>
    /// Retourne le vertex correspondant à une direction secondaire.
    /// Un vertex est formé par cet hexagone et deux de ses voisins selon les directions principales.
    /// </summary>
    public Vertex Vertex(SecondaryHexDirection direction)
    {
        var (dir1, dir2) = SecondaryHexDirectionUtils.SecondaryToMainDirectionPairs[direction];
        var neighbor1 = Neighbor(dir1);
        var neighbor2 = Neighbor(dir2);
        return HexGrid.Vertex.Create(this, neighbor1, neighbor2);
    }

    /// <summary>
    /// Retourne l'edge correspondant à une direction principale.
    /// Un edge est formé par cet hexagone et son voisin dans la direction principale spécifiée.
    /// </summary>
    public Edge Edge(HexDirection direction)
    {
        var neighbor = Neighbor(direction);
        return HexGrid.Edge.Create(this, neighbor);
    }

    /// <summary>
    /// Retourne l'edge sortant correspondant à une direction secondaire.
    /// Un edge sortant part de cet hexagone dans la direction principale qui suit 
    /// la direction secondaire dans le sens horaire.
    /// </summary>
    public Edge OutgoingEdge(SecondaryHexDirection direction)
    {
        var mainDirs = SecondaryHexDirectionUtils.SecondaryToMainDirectionPairs[direction];
        var neighbor0 = Neighbor(mainDirs.Item1);
        var neighbor1 = Neighbor(mainDirs.Item2);
        return HexGrid.Edge.Create(neighbor0, neighbor1);
    }

    /// <summary>
    /// Calcule la distance entre deux hexagones.
    /// </summary>
    public int DistanceTo(HexCoord other)
    {
        EnsureSameZ(other, nameof(DistanceTo));
        return (Math.Abs(Q - other.Q) +
                Math.Abs(Q + R - other.Q - other.R) +
                Math.Abs(R - other.R)) / 2;
    }

    public bool HasSameZ(HexCoord other)
    {
        return Z == other.Z;
    }

    public void EnsureSameZ(HexCoord other, string operation)
    {
        if (!HasSameZ(other))
        {
            throw new ArgumentException(
                $"Cannot {operation} across different map layers: {this} and {other}");
        }
    }

    /// <summary>
    /// Vérifie l'égalité avec un autre HexCoord.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is HexCoord other && Q == other.Q && R == other.R && Z == other.Z;
    }

    /// <summary>
    /// Retourne une représentation en chaîne pour le débogage.
    /// </summary>
    public override string ToString()
    {
        return $"({Q}, {R}, z={Z})";
    }

    /// <summary>
    /// Génère un hash pour utiliser comme clé dans des Maps/Sets.
    /// </summary>
    public override int GetHashCode()
    {
        unchecked
        {
            return (Q * 31 + R) * 31 + Z;
        }
    }

    /// <summary>
    /// Sérialise la coordonnée en [q, r, z].
    /// </summary>
    public int[] Serialize()
    {
        return new[] { Q, R, Z };
    }

    /// <summary>
    /// Désérialise depuis [q, r] (legacy, z=0) ou [q, r, z].
    /// </summary>
    public static HexCoord Deserialize(int[] data)
    {
        if (data.Length != 2 && data.Length != 3)
        {
            throw new ArgumentException("HexCoord data must contain [q, r] or [q, r, z]", nameof(data));
        }

        return new HexCoord(data[0], data[1], data.Length == 3 ? data[2] : 0);
    }
}
