using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

/// <summary>
/// Génère une île compacte classique, puis y creuse une mer intérieure : une chaîne d'hexs d'eau
/// qui s'enfonce depuis la côte vers l'intérieur, reliée à l'extérieur par une seule embouchure
/// (un seul hex de la chaîne touche l'eau extérieure). Comme pour le lac, les routes maritimes se
/// construisent ensuite normalement sur les edges de cette poche d'eau (RoadController).
/// </summary>
public class IslandShapeGeneratorInlandSea : IslandShapeGenerator
{
    private const int MinCountForInlandSea = 24;
    private const int MaxAttempts = 12;
    private readonly GamePRNG _prng;

    public IslandShapeGeneratorInlandSea(GamePRNG prng)
    {
        _prng = prng;
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer)
    {
        if (count < MinCountForInlandSea)
            return new IslandShapeGeneratorCompact(_prng).GenerateCoords(count, layer);

        int seaSize = _prng.Next(3, 7); // 3 à 6 hexs
        var grown = new IslandShapeGeneratorCompact(_prng).GenerateCoords(count + seaSize, layer);
        var landSet = new HashSet<HexCoord>(grown);

        // Hexs purement intérieurs (0 voisin extérieur) : la chaîne ne peut s'étendre que là, sauf
        // pour son point de départ (l'unique embouchure). Garantit mouthEdges == 1 sans retirages.
        var interiorOnly = new HashSet<HexCoord>(grown
            .Where(h => HexDirectionUtils.AllHexDirections.All(d => landSet.Contains(h.Neighbor(d)))));

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var seed = PickSingleMouthCoastalHex(grown, landSet);
            if (seed == null) break;

            var allowed = new HashSet<HexCoord>(interiorOnly) { seed.Value };
            var chain = WaterPocketCarver.CarveChain(_prng, seed.Value, seaSize, allowed);
            if (chain == null) continue;

            var chainSet = new HashSet<HexCoord>(chain);
            return grown.Where(h => !chainSet.Contains(h)).ToList();
        }

        return new IslandShapeGeneratorCompact(_prng).GenerateCoords(count, layer);
    }

    // Un hex côtier avec exactement un voisin extérieur (pas un cap exposé sur plusieurs côtés) :
    // seul point de départ pouvant donner une embouchure à un seul edge.
    private HexCoord? PickSingleMouthCoastalHex(IReadOnlyList<HexCoord> grown, HashSet<HexCoord> landSet)
    {
        var coastal = grown
            .Where(h => HexDirectionUtils.AllHexDirections.Count(d => !landSet.Contains(h.Neighbor(d))) == 1)
            .ToList();

        return coastal.Count > 0 ? coastal[_prng.Next(coastal.Count)] : null;
    }
}
