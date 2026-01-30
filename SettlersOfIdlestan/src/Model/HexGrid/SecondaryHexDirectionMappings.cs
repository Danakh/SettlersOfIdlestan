namespace SettlersOfIdlestan.Model.HexGrid;

/// <summary>
/// Mapping centralisé entre directions secondaires et paires de directions principales.
/// 
/// Chaque direction secondaire correspond à un sommet (vertex) qui se situe entre
/// deux directions principales adjacentes dans le sens horaire.
/// 
/// Exemple : SecondaryHexDirection.N se situe entre MainHexDirection.NW et MainHexDirection.NE
/// </summary>
public static class SecondaryHexDirectionMappings
{
    /// <summary>
    /// Dictionnaire des paires de directions principales pour chaque direction secondaire.
    /// </summary>
    public static readonly Dictionary<SecondaryHexDirection, (HexDirection, HexDirection)> SecondaryToMainDirectionPairs = new()
    {
        { SecondaryHexDirection.N, (HexDirection.NW, HexDirection.NE) },
        { SecondaryHexDirection.EN, (HexDirection.NE, HexDirection.E) },
        { SecondaryHexDirection.ES, (HexDirection.E, HexDirection.SE) },
        { SecondaryHexDirection.S, (HexDirection.SE, HexDirection.SW) },
        { SecondaryHexDirection.WS, (HexDirection.SW, HexDirection.W) },
        { SecondaryHexDirection.WN, (HexDirection.W, HexDirection.NW) },
    };
}