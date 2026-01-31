using System.Collections.Generic;

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
public class HexCoord
{
    public HexCoord(int q, int r)
    {
        Q = q;
        R = r;
    }

    public int Q { get; }
    public int R { get; }

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
        return new HexCoord(Q + dq, R + dr);
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
        var (dir1, dir2) = SecondaryHexDirectionMappings.SecondaryToMainDirectionPairs[direction];
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
        var mainDirs = SecondaryHexDirectionMappings.SecondaryToMainDirectionPairs[direction];
        var neighbor0 = Neighbor(mainDirs.Item1);
        var neighbor1 = Neighbor(mainDirs.Item2);
        return HexGrid.Edge.Create(neighbor0, neighbor1);
    }

    /// <summary>
    /// Calcule la distance entre deux hexagones.
    /// </summary>
    public int DistanceTo(HexCoord other)
    {
        return (Math.Abs(Q - other.Q) +
                Math.Abs(Q + R - other.Q - other.R) +
                Math.Abs(R - other.R)) / 2;
    }

    /// <summary>
    /// Vérifie l'égalité avec un autre HexCoord.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is HexCoord other && Q == other.Q && R == other.R;
    }

    /// <summary>
    /// Retourne une représentation en chaîne pour le débogage.
    /// </summary>
    public override string ToString()
    {
        return $"({Q}, {R})";
    }

    /// <summary>
    /// Génère un hash pour utiliser comme clé dans des Maps/Sets.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Q, R);
    }

    /// <summary>
    /// Sérialise la coordonnée en [q, r].
    /// </summary>
    public int[] Serialize()
    {
        return [Q, R];
    }

    /// <summary>
    /// Désérialise depuis [q, r].
    /// </summary>
    public static HexCoord Deserialize(int[] data)
    {
        return new HexCoord(data[0], data[1]);
    }
}