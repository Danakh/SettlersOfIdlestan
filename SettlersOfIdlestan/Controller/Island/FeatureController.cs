using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestan.Controller.Island;

/// <summary>
/// Gère la découverte de toutes les IslandFeature (Bandit, BanditHideout, TreasureTrove, futures features).
/// Chaque feature visible et non encore trouvée est marquée Found et logge son event de découverte.
/// </summary>
public class FeatureController
{
    private WorldState? _state;
    private GameClock? _clock;
    private List<IslandFeature> _features = new();

    public event EventHandler<IslandFeature>? OnFeatureDiscovered;

    internal void Initialize(WorldState? state, GameClock? clock)
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

        RefreshContestedTerritories();
    }

    /// <summary>
    /// Recalcule les features ContestedTerritory à partir des positions de villes actuelles.
    /// À appeler après toute création ou destruction de ville.
    /// </summary>
    public void RefreshContestedTerritories()
    {
        if (_state == null) return;

        // Supprime les anciennes features contestées
        var old = _features.OfType<ContestedTerritory>().ToList();
        foreach (var f in old)
            _state.RemoveFeature(f);

        // Calcule quels hexes sont adjacents à des villes de 2 civilisations distinctes ou plus
        var hexCivs = new Dictionary<HexCoord, HashSet<int>>();
        foreach (var civ in _state.Civilizations)
            foreach (var city in civ.Cities)
                foreach (var hex in city.Position.GetHexes())
                {
                    if (hex == null) continue;
                    if (!hexCivs.TryGetValue(hex, out var civSet))
                        hexCivs[hex] = civSet = new HashSet<int>();
                    civSet.Add(civ.Index);
                }

        foreach (var (hex, civs) in hexCivs)
            if (civs.Count >= 2)
                _state.AddFeature(new ContestedTerritory(hex));
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FeatureController] Discover: {ex}"); }
    }

    private void DiscoverFeatures()
    {
        if (_state == null) return;

        var playerIdx = _state.PlayerCivilization.Index;
        var visibleMaps = _state.GetMapsByZ()
            .Select(map => _state.Visibility.GetForZ(map.Key))
            .Where(maps => maps.TryGetValue(playerIdx, out _))
            .Select(maps => maps[playerIdx])
            .ToList();

        foreach (var feature in _features)
        {
            if (feature.IsDiscoverable &&
                visibleMaps.Any(visibleMap => visibleMap.IsOnSameLayer(feature.Position) && visibleMap.HasTile(feature.Position)))
            {
                feature.Found = true;
                bool featureToast = feature.DiscoveredEventType is GameEventType.BanditHideoutDiscovered or GameEventType.DragonDiscovered or GameEventType.MinorDemonDiscovered or GameEventType.MajorDemonDiscovered or GameEventType.VolcanoDiscovered;
                _state.EventLog.Add(feature.DiscoveredEventType, toast: featureToast);
                OnFeatureDiscovered?.Invoke(this, feature);
            }
        }
    }

    private void DiscoverCivilizations()
    {
        if (_state == null) return;

        var playerIdx = _state.PlayerCivilization.Index;
        var visibleMaps = _state.GetMapsByZ()
            .Select(map => _state.Visibility.GetForZ(map.Key))
            .Where(maps => maps.TryGetValue(playerIdx, out _))
            .Select(maps => maps[playerIdx])
            .ToList();

        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerIdx || civ.DiscoveredByPlayer) continue;

            var isCivVisible =
                visibleMaps.Any(visibleMap =>
                    civ.Cities.Any(city => visibleMap.IsVertexVisible(city.Position)));

            if (isCivVisible)
            {
                civ.DiscoveredByPlayer = true;
                _state.EventLog.Add(GameEventType.CivilizationDiscovered, toast: true);
            }
        }
    }
}
