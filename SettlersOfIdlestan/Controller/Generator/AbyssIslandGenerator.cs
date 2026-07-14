using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Génère une île de l'Abysse (3 à 5 hexes de terrain aléatoire, entourée d'un anneau de Void)
/// au-delà d'un hex de Void donné, lorsque ce Void devient visible.
/// </summary>
public static class AbyssIslandGenerator
{
    public const int MinIslandHexCount = 3;
    public const int MaxIslandHexCount = 5;

    private static readonly TerrainType[] TerrainPool =
    {
        TerrainType.Forest, TerrainType.Hill, TerrainType.Mountain, TerrainType.Plain, TerrainType.Water,
    };

    /// <summary>
    /// Fait pousser une île au-delà de <paramref name="voidHex"/>, à partir d'un de ses voisins non
    /// encore occupés, puis l'entoure d'un anneau de Void. Ne modifie pas la carte : retourne les
    /// tuiles à ajouter (appelant responsable de <see cref="IslandMap.AddTile"/>). Retourne une liste
    /// vide si <paramref name="voidHex"/> n'a plus aucun voisin libre (déjà entouré de tous côtés) —
    /// sûr à appeler plusieurs fois pour le même hex de Void.
    /// </summary>
    public static List<HexTile> GenerateIslandBeyondVoid(IslandMap map, HexCoord voidHex, GamePRNG prng)
    {
        var emptyNeighbors = voidHex.Neighbors().Where(n => !map.HasTile(n)).ToList();
        if (emptyNeighbors.Count == 0) return new List<HexTile>();

        var seed = emptyNeighbors[prng.Next(emptyNeighbors.Count)];
        int targetCount = prng.Next(MinIslandHexCount, MaxIslandHexCount + 1);

        var islandHexes = new List<HexCoord> { seed };
        var visited = new HashSet<HexCoord> { seed, voidHex };
        var frontier = new Queue<HexCoord>();
        frontier.Enqueue(seed);

        while (frontier.Count > 0 && islandHexes.Count < targetCount)
        {
            var current = frontier.Dequeue();
            foreach (var n in current.Neighbors())
            {
                if (islandHexes.Count >= targetCount) break;
                if (visited.Contains(n) || map.HasTile(n)) continue;
                visited.Add(n);
                islandHexes.Add(n);
                frontier.Enqueue(n);
            }
        }

        var newTiles = new List<HexTile>(islandHexes.Count);
        foreach (var hex in islandHexes)
            newTiles.Add(new HexTile(hex, TerrainPool[prng.Next(TerrainPool.Length)]));

        var islandSet = new HashSet<HexCoord>(islandHexes);
        var ringHexes = new HashSet<HexCoord>();
        foreach (var hex in islandHexes)
        {
            foreach (var n in hex.Neighbors())
            {
                if (!islandSet.Contains(n) && !map.HasTile(n))
                    ringHexes.Add(n);
            }
        }

        foreach (var ring in ringHexes)
            newTiles.Add(new HexTile(ring, TerrainType.Void));

        return newTiles;
    }
}
