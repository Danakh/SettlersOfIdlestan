using System;
using System.Linq;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Représente une arête (edge) entre deux hexagones adjacents.
/// 
/// Cette entité est purement géométrique et modélise une connexion
/// entre deux cellules voisines, quelle que soit la couche métier
/// (carte, arbre de technologies, etc.).
/// 
/// Une arête est identifiée de manière unique par deux hexagones adjacents.
/// L'ordre des hexagones est normalisé pour garantir l'unicité.
/// </summary>
[Serializable]
public class Edge
{
    private Edge(HexCoord hex1, HexCoord hex2)
    {
        // Validation: les hexagones doivent être adjacents
        var distance = hex1.DistanceTo(hex2);
        if (distance != 1)
        {
            throw new ArgumentException($"La distance entre les hexagones n'est pas de 1: {distance}");
        }

        Hex1 = hex1;
        Hex2 = hex2;
    }

    public HexCoord Hex1 { get; }
    public HexCoord Hex2 { get; }

    /// <summary>
    /// Crée une arête entre deux hexagones adjacents.
    /// Normalise l'ordre pour garantir l'unicité.
    /// </summary>
    public static Edge Create(HexCoord hex1, HexCoord hex2)
    {
        var normalized = Normalize(hex1, hex2);
        return new Edge(normalized.Item1, normalized.Item2);
    }

    /// <summary>
    /// Normalise l'ordre de deux coordonnées pour garantir l'unicité.
    /// Ordre: q d'abord, puis r si égalité.
    /// </summary>
    private static (HexCoord, HexCoord) Normalize(HexCoord hex1, HexCoord hex2)
    {
        if (hex1.Q < hex2.Q || (hex1.Q == hex2.Q && hex1.R < hex2.R))
        {
            return (hex1, hex2);
        }
        return (hex2, hex1);
    }

    /// <summary>
    /// Vérifie si cette arête est égale à une autre.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Edge other &&
               ((Hex1.Equals(other.Hex1) && Hex2.Equals(other.Hex2)) ||
                (Hex1.Equals(other.Hex2) && Hex2.Equals(other.Hex1)));
    }

    /// <summary>
    /// Retourne les deux hexagones de cette arête.
    /// </summary>
    public (HexCoord, HexCoord) GetHexes()
    {
        return (Hex1, Hex2);
    }

    /// <summary>
    /// Vérifie si cette arête est adjacente à un hexagone donné.
    /// </summary>
    public bool IsAdjacentTo(HexCoord hex)
    {
        return Hex1.Equals(hex) || Hex2.Equals(hex);
    }

    /// <summary>
    /// Retourne l'autre hexagone de l'arête donné un hexagone.
    /// </summary>
    public HexCoord OtherHex(HexCoord hex)
    {
        if (Hex1.Equals(hex))
        {
            return Hex2;
        }
        else if (Hex2.Equals(hex))
        {
            return Hex1;
        }
        else
        {
            throw new ArgumentException("L'hexagone n'est pas connecté à cette arête");
        }
    }

    /// <summary>
    /// Retourne l'autre vertex de l'arête donné un vertex.
    /// </summary>
    public Vertex OtherVertex(Vertex vertex)
    {
        var (h1, h2) = GetHexes();
        var verticesH1 = new[]
        {
            h1.Vertex(SecondaryHexDirection.N),
            h1.Vertex(SecondaryHexDirection.EN),
            h1.Vertex(SecondaryHexDirection.ES),
            h1.Vertex(SecondaryHexDirection.S),
            h1.Vertex(SecondaryHexDirection.WS),
            h1.Vertex(SecondaryHexDirection.WN)
        };
        var verticesH2 = new[]
        {
            h2.Vertex(SecondaryHexDirection.N),
            h2.Vertex(SecondaryHexDirection.EN),
            h2.Vertex(SecondaryHexDirection.ES),
            h2.Vertex(SecondaryHexDirection.S),
            h2.Vertex(SecondaryHexDirection.WS),
            h2.Vertex(SecondaryHexDirection.WN)
        };

        // Trouver les deux vertex communs aux deux hexagones
        var commonVertices = verticesH1.Where(v1 => verticesH2.Any(v2 => v1.Equals(v2))).ToList();
        if (commonVertices.Count != 2)
        {
            throw new InvalidOperationException("Les vertex ne sont pas partagés");
        }
        // Retourner l'autre vertex
        if (commonVertices[0].Equals(vertex))
        {
            return commonVertices[1];
        }
        else if (commonVertices[1].Equals(vertex))
        {
            return commonVertices[0];
        }
        else
        {
            throw new ArgumentException("Le vertex n'est pas connecté à cette arête");
        }
    }

    /// <summary>
    /// Retourne une représentation en chaîne pour le débogage.
    /// </summary>
    public override string ToString()
    {
        return $"Edge({Hex1} - {Hex2})";
    }

    /// <summary>
    /// Génère un hash pour utiliser comme clé dans des Maps/Sets.
    /// </summary>
    public override int GetHashCode()
    {
        var normalized = Normalize(Hex1, Hex2);
        return HashCode.Combine(normalized.Item1, normalized.Item2);
    }

    /// <summary>
    /// Sérialise l'arête en [h1, h2] (chaque hi = [q, r]).
    /// </summary>
    public int[][] Serialize()
    {
        var (a, b) = GetHexes();
        return [a.Serialize(), b.Serialize()];
    }

    /// <summary>
    /// Désérialise depuis [[q1,r1],[q2,r2]].
    /// </summary>
    public static Edge Deserialize(int[][] data)
    {
        return Create(HexCoord.Deserialize(data[0]), HexCoord.Deserialize(data[1]));
    }
}