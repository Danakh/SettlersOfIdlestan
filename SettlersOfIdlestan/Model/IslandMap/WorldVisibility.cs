using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Manages the per-civilization visibility cache derived from a WorldState.
/// Not serialized — rebuilt on load and invalidated after map/city/road mutations.
/// </summary>
public class WorldVisibility
{
    private readonly WorldState _world;
    private Dictionary<int, Dictionary<int, VisibleIslandMap>> _byZ = new();

    public WorldVisibility(WorldState world)
    {
        _world = world;
    }

    /// <summary>Grand Phare niveau 1+ : les Tours de Guet voient 1 hex plus loin (rayon 3 au lieu de 2).</summary>
    private bool WatchtowerVisionBonus
        => _world.Features.OfType<GreatLighthouse>().FirstOrDefault()?.Level >= 1;

    /// <summary>Rebuilds visibility for every civilization on every layer.</summary>
    public void Recalculate()
    {
        bool watchtowerVisionBonus = WatchtowerVisionBonus;
        _byZ = _world.GetMapsByZ().ToDictionary(
            kvp => kvp.Key,
            kvp => _world.Civilizations.ToDictionary(
                civ => civ.Index,
                civ => new VisibleIslandMap(kvp.Value, civ, watchtowerVisionBonus)));
    }

    /// <summary>Rebuilds visibility for a single civilization after a road or city changed.</summary>
    public void RecalculateFor(int civilizationIndex)
    {
        var civilization = _world.Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
            ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

        bool watchtowerVisionBonus = WatchtowerVisionBonus;
        foreach (var (z, map) in _world.GetMapsByZ())
        {
            if (!_byZ.TryGetValue(z, out var visibleMaps))
            {
                visibleMaps = new Dictionary<int, VisibleIslandMap>();
                _byZ[z] = visibleMaps;
            }
            visibleMaps[civilizationIndex] = new VisibleIslandMap(map, civilization, watchtowerVisionBonus);
        }
    }

    /// <summary>Returns the visibility map for the given layer, computing it on first access.</summary>
    public IReadOnlyDictionary<int, VisibleIslandMap> GetForZ(int z)
    {
        if (!_byZ.TryGetValue(z, out var visibleMaps))
        {
            var map = _world.GetMapForZ(z);
            if (map == null) return new Dictionary<int, VisibleIslandMap>();
            bool watchtowerVisionBonus = WatchtowerVisionBonus;
            visibleMaps = _world.Civilizations.ToDictionary(
                civ => civ.Index,
                civ => new VisibleIslandMap(map, civ, watchtowerVisionBonus));
            _byZ[z] = visibleMaps;
        }
        return visibleMaps;
    }
}
