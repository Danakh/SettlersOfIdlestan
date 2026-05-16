using System;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Représente les six directions principales dans une grille hexagonale.
/// Ces directions permettent d'explorer le voisinage des hexagones.
/// 
/// Les déplacements en coordonnées (q, r) :
/// - W (Ouest) : (-1, 0)
/// - E (Est) : (+1, 0)
/// - NE (Nord-Est) : (0, +1)
/// - SE (Sud-Est) : (+1, -1)
/// - NW (Nord-Ouest) : (-1, +1)
/// - SW (Sud-Ouest) : (0, -1)
/// </summary>
public enum HexDirection
{
    /// <summary> Ouest </summary>
    W = 0,
    /// <summary> Nord-Ouest </summary>
    NW = 1,
    /// <summary> Nord-Est </summary>
    NE = 2,
    /// <summary> Est </summary>
    E = 3,
    /// <summary> Sud-Est </summary>
    SE = 4,
    /// <summary> Sud-Ouest </summary>
    SW = 5,
}

/// <summary>
/// Fonctions utilitaires pour les directions hexagonales.
/// </summary>
public static class HexDirectionUtils
{
    /// <summary>
    /// Tableau de toutes les directions principales dans l'ordre.
    /// </summary>
    public static readonly HexDirection[] AllHexDirections = [
        HexDirection.W,
        HexDirection.NW,
        HexDirection.NE,
        HexDirection.E,
        HexDirection.SE,
        HexDirection.SW,
    ];

    /// <summary>
    /// Retourne la direction inverse (opposee) d'une direction principale.
    /// W <-> E, NE <-> SW, NW <-> SE
    /// </summary>
    public static HexDirection InverseHexDirection(HexDirection direction)
    {
        return direction switch
        {
            HexDirection.W => HexDirection.E,
            HexDirection.E => HexDirection.W,
            HexDirection.NE => HexDirection.SW,
            HexDirection.SW => HexDirection.NE,
            HexDirection.NW => HexDirection.SE,
            HexDirection.SE => HexDirection.NW,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    /// <summary>
    /// Retourne la direction suivante dans le sens horaire.
    /// </summary>
    public static HexDirection Next(this HexDirection direction)
    {
        return (HexDirection)((int)(direction + 1) % 6);
    }

    /// <summary>
    /// Retourne la direction précédante dans le sens horaire.
    /// </summary>
    public static HexDirection Previous(this HexDirection direction)
    {
        return (HexDirection)((int)(direction + 5) % 6);
    }
}