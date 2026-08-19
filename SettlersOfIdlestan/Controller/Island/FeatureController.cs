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

    /// <summary>
    /// Cartes de visibilité du joueur, une par couche, réutilisées d'un événement à l'autre.
    ///
    /// <para>La chaîne <c>Select/Where/Select/ToList</c> qu'elles remplacent était reconstruite à
    /// chaque événement d'horloge par <see cref="DiscoverFeatures"/> <b>et</b> par
    /// <see cref="DiscoverCivilizations"/>, itérateurs et fermetures compris, puis parcourue par un
    /// <c>Any</c> capturant — un lambda par feature et par civilisation. Ce contrôleur allouait
    /// 26 Ko par événement pour 0,02 ms de travail réel.</para>
    /// </summary>
    private readonly List<VisibleIslandMap> _playerVisibleMapsScratch = new();

    private List<VisibleIslandMap> GetPlayerVisibleMaps(int playerIdx)
    {
        var visibleMaps = _playerVisibleMapsScratch;
        visibleMaps.Clear();
        foreach (var (z, _) in _state!.GetMapsByZ())
            if (_state.Visibility.GetForZ(z).TryGetValue(playerIdx, out var map))
                visibleMaps.Add(map);
        return visibleMaps;
    }

    private void DiscoverFeatures()
    {
        if (_state == null) return;

        var playerIdx = _state.PlayerCivilization.Index;
        var visibleMaps = GetPlayerVisibleMaps(playerIdx);

        foreach (var feature in _features)
        {
            if (!feature.IsDiscoverable) continue;

            bool visible = false;
            for (int i = 0; i < visibleMaps.Count && !visible; i++)
                visible = visibleMaps[i].IsOnSameLayer(feature.Position) && visibleMaps[i].HasTile(feature.Position);

            if (visible)
            {
                feature.Found = true;
                bool featureToast = feature.DiscoveredEventType is GameEventType.BanditHideoutDiscovered or GameEventType.DragonDiscovered or GameEventType.MinorDemonDiscovered or GameEventType.MajorDemonDiscovered or GameEventType.VolcanoDiscovered or GameEventType.TentacleDiscovered or GameEventType.DemonGodDiscovered;
                _state.EventLog.Add(feature.DiscoveredEventType, toast: featureToast);
                OnFeatureDiscovered?.Invoke(this, feature);
            }
        }
    }

    private void DiscoverCivilizations()
    {
        if (_state == null) return;

        var playerIdx = _state.PlayerCivilization.Index;
        var visibleMaps = GetPlayerVisibleMaps(playerIdx);

        foreach (var civ in _state.Civilizations)
        {
            if (civ.Index == playerIdx || civ.DiscoveredByPlayer) continue;

            // Boucles indexées : le Any imbriqué allouait deux fermetures par civilisation examinée.
            bool isCivVisible = false;
            var cities = civ.Cities;
            for (int m = 0; m < visibleMaps.Count && !isCivVisible; m++)
                for (int c = 0; c < cities.Count && !isCivVisible; c++)
                    isCivVisible = visibleMaps[m].IsVertexVisible(cities[c].Position);

            if (isCivVisible)
            {
                civ.DiscoveredByPlayer = true;
                _state.EventLog.Add(GameEventType.CivilizationDiscovered, toast: true);
            }
        }
    }
}
