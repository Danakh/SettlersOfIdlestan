using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model;
using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Game;
using System.Text.Json.Serialization;
using System.Linq;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the state of an island, containing the map and all civilizations.
/// </summary>
[Serializable]
public class IslandState : IJsonOnDeserialized
{
    public IslandMap Map { get; set; }

    public int IslandID { get; set; }

    /// <summary>
    /// Tick de simulation au moment où cette île a démarré (pour calculer la durée de jeu).
    /// </summary>
    public long StartTick { get; set; } = 0;

    /// <summary>
    /// Gets the list of civilizations on the island.
    /// </summary>
    public List<SettlersOfIdlestan.Model.Civilization.Civilization> Civilizations { get; set; }

    /// <summary>
    /// Gets the player's civilization (always at index 0).
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization PlayerCivilization => Civilizations[0];

    /// <summary>
    /// Runtime-only visible maps, keyed by civilization index.
    /// Rebuilt from Map and Civilizations after deserialization and after construction changes.
    /// </summary>
    [JsonIgnore]
    public Dictionary<int, VisibleIslandMap> VisibleIslandMaps { get; private set; } = new();

    /// <summary>
    /// Transient event log for the current session. Not persisted.
    /// </summary>
    [JsonIgnore]
    public GameEventLog EventLog { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="IslandState"/> class.
    /// </summary>
    /// <param name="map">The island map.</param>
    /// <param name="civilizations">The list of civilizations.</param>
    /// <param name="islandID">The ID of the island.</param>
    public IslandState(IslandMap map, List<SettlersOfIdlestan.Model.Civilization.Civilization> civilizations, int islandID)
    {
        Map = map;
        Civilizations = civilizations;
        IslandID = islandID;
        HarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>>();
        AutomaticHarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>>();
        Features = new List<IslandFeature>();
        BanditCooldownUntil = new Dictionary<HexCoord, long>();
        RecalculateVisibleIslandMaps();
    }

    /// <summary>
    /// Parameterless constructor for deserialization.
    /// </summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public IslandState()
    {
        Map = new IslandMap(Array.Empty<HexTile>());
        Civilizations = new List<SettlersOfIdlestan.Model.Civilization.Civilization>();
        HarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>>();
        AutomaticHarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>>();
        Features = new List<IslandFeature>();
        BanditCooldownUntil = new Dictionary<HexCoord, long>();
    }

    public void OnDeserialized()
    {
        RecalculateVisibleIslandMaps();
    }

    /// <summary>
    /// Rebuilds visible maps for every civilization.
    /// </summary>
    public void RecalculateVisibleIslandMaps()
    {
        VisibleIslandMaps = Civilizations.ToDictionary(
            civilization => civilization.Index,
            civilization => new VisibleIslandMap(Map, civilization));
    }

    /// <summary>
    /// Rebuilds the visible map for one civilization after a road or city changed.
    /// </summary>
    public void RecalculateVisibleIslandMap(int civilizationIndex)
    {
        var civilization = Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
            ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

        VisibleIslandMaps[civilizationIndex] = new VisibleIslandMap(Map, civilization);
    }

    /// <summary>
    /// Tick de simulation de la dernière récolte manuelle par civilisation et par hex (1 tick = 0.01 s).
    /// </summary>
    public Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>> HarvestLastTimesByCivilization { get; set; }

    /// <summary>
    /// Tick de simulation de la dernière récolte automatique par civilisation et par hex (1 tick = 0.01 s).
    /// </summary>
    public Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>> AutomaticHarvestLastTimesByCivilization { get; set; }

    /// <summary>
    /// Toutes les features de l'île (Bandit, BanditHideout, TreasureTrove).
    /// Utiliser AddFeature / RemoveFeature pour modifier afin de déclencher les events.
    /// </summary>
    public List<IslandFeature> Features { get; set; }

    /// <summary>Déclenché quand une feature est ajoutée via AddFeature.</summary>
    public event EventHandler<IslandFeature>? FeatureAdded;

    /// <summary>Déclenché quand une feature est supprimée via RemoveFeature.</summary>
    public event EventHandler<IslandFeature>? FeatureRemoved;

    public void AddFeature(IslandFeature feature)
    {
        Features.Add(feature);
        FeatureAdded?.Invoke(this, feature);
    }

    public bool RemoveFeature(IslandFeature feature)
    {
        if (!Features.Remove(feature)) return false;
        FeatureRemoved?.Invoke(this, feature);
        return true;
    }

    /// <summary>
    /// Tick jusqu'auquel la récolte est bloquée sur un hex après le départ d'un bandit (1000 ticks).
    /// </summary>
    public Dictionary<HexCoord, long> BanditCooldownUntil { get; set; }

    /// <summary>
    /// Player-controlled automation toggles. Persisted with the island state.
    /// </summary>
    public AutomationSettings AutomationSettings { get; set; } = new();

    public IEnumerable<City> GetAllCities()
    {
        return Civilizations.SelectMany(c => c.Cities);
    }

    public City? FindCityAt(Vertex vertex)
    {
        return GetAllCities().FirstOrDefault(c => c.Position.Equals(vertex));
    }
}
