namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Représente une cellule hexagonale dans une grille.
/// 
/// Cette classe est volontairement générique et ne contient que des
/// informations géométriques (la coordonnée). Toute donnée métier
/// (ressource, technologie, biome, etc.) doit être portée par des
/// structures de niveau supérieur qui référencent cette cellule.
/// </summary>
public class Hex
{
    public Hex(HexCoord coord)
    {
        Coord = coord;
    }

    public HexCoord Coord { get; }

    /// <summary>
    /// Vérifie l'égalité avec un autre Hex (égalité structurelle sur la coordonnée).
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Hex other && Coord.Equals(other.Coord);
    }

    /// <summary>
    /// Retourne une représentation en chaîne pour le débogage.
    /// </summary>
    public override string ToString()
    {
        return $"Hex({Coord})";
    }

    /// <summary>
    /// Sérialise l'hexagone (délègue à la coordonnée).
    /// </summary>
    public int[] Serialize()
    {
        return Coord.Serialize();
    }

    /// <summary>
    /// Désérialise depuis [q, r].
    /// </summary>
    public static Hex Deserialize(int[] data)
    {
        return new Hex(HexCoord.Deserialize(data));
    }

    /// <summary>
    /// Retourne les coordonnées du voisin dans la direction principale spécifiée.
    /// </summary>
    public HexCoord Neighbor(HexDirection direction)
    {
        return Coord.Neighbor(direction);
    }

    /// <summary>
    /// Retourne tous les voisins de cet hexagone en utilisant les directions principales.
    /// </summary>
    public HexCoord[] Neighbors()
    {
        return Coord.Neighbors();
    }

    /// <summary>
    /// Retourne l'edge correspondant à une direction principale.
    /// L'edge est formé par cet hexagone et son voisin dans la direction principale spécifiée.
    /// </summary>
    public Edge GetEdgeByMainDirection(HexDirection direction)
    {
        var neighborCoord = Neighbor(direction);
        return Edge.Create(Coord, neighborCoord);
    }

    /// <summary>
    /// Retourne le vertex correspondant à une direction secondaire.
    /// Un vertex est formé par cet hexagone et deux de ses voisins selon les directions principales.
    /// 
    /// Correspondance des directions secondaires aux paires de directions principales :
    /// - N : entre NW et NE
    /// - EN : entre NE et E
    /// - ES : entre E et SE
    /// - S : entre SE et SW
    /// - WS : entre SW et W
    /// - WN : entre W et NW
    /// </summary>
    public Vertex GetVertexBySecondaryDirection(SecondaryHexDirection direction)
    {
        var (dir1, dir2) = SecondaryHexDirectionMappings.SecondaryToMainDirectionPairs[direction];
        var neighbor1 = Neighbor(dir1);
        var neighbor2 = Neighbor(dir2);

        return Vertex.Create(Coord, neighbor1, neighbor2);
    }

    /// <summary>
    /// Génère un hash pour utiliser comme clé dans des Maps/Sets.
    /// </summary>
    public override int GetHashCode()
    {
        return Coord.GetHashCode();
    }
}