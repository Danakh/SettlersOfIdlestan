using System;

namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Repr�sente les six directions secondaires dans une grille hexagonale.
/// Ces directions permettent d'indiquer les vertexs et edges autour d'un hexagone.
/// 
/// Les directions secondaires s'intercalent entre les directions principales
/// pour former un total de 12 directions espac�es comme les heures d'une horloge :
/// - N (Nord) : 12h
/// - EN (Est-Nord) : 1h
/// - ES (Est-Sud) : 4h
/// - S (Sud) : 6h
/// - WS (Ouest-Sud) : 7h
/// - WN (Ouest-Nord) : 10h
/// </summary>
public enum SecondaryHexDirection
{
    /// <summary> Nord </summary>
    N = 0,
    /// <summary> Est-Nord </summary>
    EN = 1,
    /// <summary> Est-Sud </summary>
    ES = 2,
    /// <summary> Sud </summary>
    S = 3,
    /// <summary> Ouest-Sud </summary>
    WS = 4,
    /// <summary> Ouest-Nord </summary>
    WN = 5,
}

/// <summary>
/// Fonctions utilitaires pour les directions secondaires.
/// </summary>
public static class SecondaryHexDirectionUtils
{
    /// <summary>
    /// Tableau de toutes les directions secondaires dans l'ordre.
    /// </summary>
    public static readonly SecondaryHexDirection[] AllSecondaryDirections = [
        SecondaryHexDirection.N,
        SecondaryHexDirection.EN,
        SecondaryHexDirection.ES,
        SecondaryHexDirection.S,
        SecondaryHexDirection.WS,
        SecondaryHexDirection.WN,
    ];

    /// <summary>
    /// Retourne la direction inverse (opposée) d'une direction secondaire.
    /// N ? S, EN ? WS, ES ? WN
    /// </summary>
    public static SecondaryHexDirection InverseSecondaryHexDirection(SecondaryHexDirection direction)
    {
        return direction switch
        {
            SecondaryHexDirection.N => SecondaryHexDirection.S,
            SecondaryHexDirection.S => SecondaryHexDirection.N,
            SecondaryHexDirection.EN => SecondaryHexDirection.WS,
            SecondaryHexDirection.WS => SecondaryHexDirection.EN,
            SecondaryHexDirection.ES => SecondaryHexDirection.WN,
            SecondaryHexDirection.WN => SecondaryHexDirection.ES,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    // Indexé par (int)SecondaryHexDirection : N=0, EN=1, ES=2, S=3, WS=4, WN=5
    private static readonly (HexDirection, HexDirection)[] _mainDirectionPairs =
    [
        (HexDirection.NW, HexDirection.NE),  // N
        (HexDirection.NE, HexDirection.E),   // EN
        (HexDirection.E,  HexDirection.SE),  // ES
        (HexDirection.SE, HexDirection.SW),  // S
        (HexDirection.SW, HexDirection.W),   // WS
        (HexDirection.W,  HexDirection.NW),  // WN
    ];

    public static (HexDirection, HexDirection) GetMainDirectionPair(SecondaryHexDirection direction)
        => _mainDirectionPairs[(int)direction];
}
