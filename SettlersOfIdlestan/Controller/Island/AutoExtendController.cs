using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestan.Controller.Island;

/// <summary>
/// Étend automatiquement la carte de l'underworld quand une route touche un hexagone manquant.
/// Probabilités fixes : 50% Montagne, 50% Désert.
/// </summary>
public class AutoExtendController
{
    private WorldState? _state;
    private GamePRNG _prng = new();

    private static readonly TerrainType[] TerrainPool = new[]
    {
        TerrainType.Mountain,
        TerrainType.Desert,
    };

    internal AutoExtendController() { }

    internal void Initialize(WorldState state, GamePRNG prng)
    {
        _state = state;
        _prng = prng;
    }

    /// <summary>
    /// À appeler après la construction d'une route. Génère les hexagones manquants
    /// aux deux vertex de l'arête sur les cartes marquées AutoExtend.
    /// </summary>
    public void TryExtendMapAfterRoad(int civIndex, Edge roadEdge)
    {
        if (_state == null) return;

        int z = roadEdge.Z;
        if (!_state.Layers.TryGetValue(z, out var layerState) || !layerState.AutoExtend)
            return;

        var map = layerState.Map;
        bool generatedAny = false;

        foreach (var vertex in roadEdge.GetVertices())
        {
            foreach (var hex in vertex.GetHexes())
            {
                if (!map.HasTile(hex))
                {
                    map.AddTile(new HexTile(hex, RollTerrain()));
                    generatedAny = true;
                }
            }
        }

        if (generatedAny)
            _state.RecalculateVisibleIslandMap(civIndex);
    }

    private TerrainType RollTerrain()
    {
        return TerrainPool[_prng.Next(TerrainPool.Length)];
    }
}
