using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Génère une île compacte classique, puis y creuse un lac intérieur : 2 à 4 hexs d'eau
/// entièrement entourés de terre, sans former de triangle d'hexs. Les routes maritimes
/// se construisent ensuite normalement sur les edges de ce lac (RoadController).
/// </summary>
public class IslandShapeGeneratorLake : IslandShapeGenerator
{
    private const int MinCountForLake = 50;
    private const int MaxAttempts = 12;
    private readonly GamePRNG _prng;

    public IslandShapeGeneratorLake(GamePRNG prng)
    {
        _prng = prng;
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer)
    {
        if (count < MinCountForLake)
            return new IslandShapeGeneratorCompact(_prng).GenerateCoords(count, layer);

        int lakeSize = _prng.Next(2, 5); // 2 à 4 hexs
        var grown = new IslandShapeGeneratorCompact(_prng).GenerateCoords(count + lakeSize, layer);
        var landSet = new HashSet<HexCoord>(grown);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var seed = PickInteriorHex(grown, landSet);
            if (seed == null) break;

            var chain = WaterPocketCarver.CarveChain(_prng, seed, lakeSize, landSet);
            if (chain == null || !IsFullyEnclosed(chain, landSet)) continue;

            var chainSet = new HashSet<HexCoord>(chain);
            return grown.Where(h => !chainSet.Contains(h)).ToList();
        }

        return new IslandShapeGeneratorCompact(_prng).GenerateCoords(count, layer);
    }

    private HexCoord? PickInteriorHex(IReadOnlyList<HexCoord> grown, HashSet<HexCoord> landSet)
    {
        var interior = grown
            .Where(h => HexDirectionUtils.AllHexDirections.All(d => landSet.Contains(h.Neighbor(d))))
            .ToList();

        return interior.Count > 0 ? interior[_prng.Next(interior.Count)] : null;
    }

    // Vrai si aucun hex de la chaîne ne touche l'extérieur de l'île (le lac reste fermé).
    private static bool IsFullyEnclosed(List<HexCoord> chain, HashSet<HexCoord> landSet)
    {
        var chainSet = new HashSet<HexCoord>(chain);
        foreach (var h in chain)
            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                var nb = h.Neighbor(dir);
                if (!chainSet.Contains(nb) && !landSet.Contains(nb)) return false;
            }
        return true;
    }
}
