using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Controller.Island;

/// <summary>
/// Gère la découverte de toutes les IslandFeature (Bandit, BanditHideout, TreasureTrove, futures features).
/// Chaque feature visible et non encore trouvée est marquée Found et logge son event de découverte.
/// </summary>
public class FeatureController
{
    private IslandState? _state;
    private GameClock? _clock;
    private List<IslandFeature> _features = new();

    internal void Initialize(IslandState? state, GameClock? clock)
    {
        if (_clock != null)
            _clock.Advanced -= OnClockAdvanced;

        if (_state != null)
        {
            _state.FeatureAdded -= OnFeatureAdded;
            _state.FeatureRemoved -= OnFeatureRemoved;
        }

        _state = state;
        _clock = clock;

        _features = _state?.Features.ToList() ?? new();

        if (_state != null)
        {
            _state.FeatureAdded += OnFeatureAdded;
            _state.FeatureRemoved += OnFeatureRemoved;
        }

        if (_clock != null)
            _clock.Advanced += OnClockAdvanced;
    }

    private void OnFeatureAdded(object? sender, IslandFeature feature) => _features.Add(feature);
    private void OnFeatureRemoved(object? sender, IslandFeature feature) => _features.Remove(feature);

    private void OnClockAdvanced(object? sender, GameClockAdvancedEventArgs e)
    {
        try
        {
            DiscoverFeatures();
            DiscoverCivilizations();
        }
        catch { }
    }

    private void DiscoverFeatures()
    {
        if (_state == null) return;

        var playerIdx = _state.PlayerCivilization.Index;
        if (!_state.VisibleIslandMaps.TryGetValue(playerIdx, out var visibleMap)) return;

        foreach (var feature in _features)
        {
            if (feature.IsDiscoverable && visibleMap.HasTile(feature.Position))
            {
                feature.Found = true;
                _state.EventLog.Add(feature.DiscoveredEventType);
            }
        }
    }

    private void DiscoverCivilizations()
    {
        if (_state == null) return;

        var playerIdx = _state.PlayerCivilization.Index;
        if (!_state.VisibleIslandMaps.TryGetValue(playerIdx, out var visibleMap)) return;

        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerIdx || civ.DiscoveredByPlayer) continue;

            var isCivVisible =
                civ.Cities.Any(city => city.Position.GetHexes().Any(visibleMap.HasTile)) ||
                civ.Roads.Any(road =>
                {
                    var (h1, h2) = road.Position.GetHexes();
                    return visibleMap.HasTile(h1) || visibleMap.HasTile(h2);
                });

            if (isCivVisible)
            {
                civ.DiscoveredByPlayer = true;
                _state.EventLog.Add(GameEventType.CivilizationDiscovered);
            }
        }
    }
}
