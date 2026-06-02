using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Generator;

public class IslandShapeGeneratorCompact : IslandShapeGenerator
{
    private readonly GamePRNG _prng;

    public IslandShapeGeneratorCompact(GamePRNG prng)
    {
        _prng = prng;
    }

    public override IReadOnlyList<HexCoord> GenerateCoords(int count, int layer = IslandMap.SurfaceLayer)
    {
        if (count <= 0) return [];

        var origin = new HexCoord(0, 0, layer);
        var island = new List<HexCoord>(count) { origin };
        var allLand = new HashSet<HexCoord> { origin };

        GrowIsland(island, count, allLand);
        return island;
    }

    private void GrowIsland(List<HexCoord> island, int targetSize, HashSet<HexCoord> allLand)
    {
        int stuckLimit = (targetSize + 1) * 6;
        int stuckCount = 0;

        while (island.Count < targetSize && stuckCount < stuckLimit)
        {
            int idx = _prng.Next(island.Count);
            var hex = island[idx];
            bool addedAny = false;

            foreach (var dir in HexDirectionUtils.AllHexDirections)
            {
                if (island.Count >= targetSize) break;
                var nb = hex.Neighbor(dir);
                if (allLand.Contains(nb)) continue;

                island.Add(nb);
                allLand.Add(nb);
                addedAny = true;
            }

            stuckCount = addedAny ? 0 : stuckCount + 1;
        }
    }
}
