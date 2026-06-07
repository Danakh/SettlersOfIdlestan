using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model;
using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Tasks;
using System.Text.Json.Serialization;
using System.Linq;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the state of a world run, containing all layers and civilizations.
/// </summary>
[Serializable]
public class WorldState : IJsonOnDeserialized
{
    private Dictionary<int, LayerState> _layers = new();

    /// <summary>
    /// All map layers indexed by Z coordinate. Use GetMapForZ(z) to retrieve a layer's map.
    /// </summary>
    public Dictionary<int, LayerState> Layers
    {
        get => _layers;
        set => _layers = value ?? new Dictionary<int, LayerState>();
    }

    public void AddLayer(int z, LayerState layer) => _layers[z] = layer;

    /// <summary>
    /// Z-coordinate of the layer currently displayed. Not persisted.
    /// </summary>
    [JsonIgnore]
    public int CurrentViewedLayer { get; set; } = IslandMap.SurfaceLayer;

    public int WorldId { get; set; }

    /// <summary>
    /// Tick de simulation au moment où ce monde a démarré (pour calculer la durée de jeu).
    /// </summary>
    public long StartTick { get; set; } = 0;

    /// <summary>
    /// Gets the list of civilizations on the world.
    /// </summary>
    public List<SettlersOfIdlestan.Model.Civilization.Civilization> Civilizations { get; set; }

    /// <summary>
    /// Gets the player's civilization (always at index 0).
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization PlayerCivilization => Civilizations[0];

    [JsonIgnore]
    public WorldVisibility Visibility { get; }

    [JsonIgnore]
    public IslandMap CurrentViewedMap => GetMapForZ(
        Layers.ContainsKey(CurrentViewedLayer) ? CurrentViewedLayer : IslandMap.SurfaceLayer);

    /// <summary>
    /// Transient event log for the current session. Not persisted.
    /// </summary>
    [JsonIgnore]
    public GameEventLog EventLog { get; } = new();

    public WorldState(IslandMap map, List<SettlersOfIdlestan.Model.Civilization.Civilization> civilizations, int worldId)
    {
        Visibility = new WorldVisibility(this);
        _layers[IslandMap.SurfaceLayer] = new LayerState(map);
        Civilizations = civilizations;
        WorldId = worldId;
        Features = new List<IslandFeature>();
        PlunderCooldownDuration = new Dictionary<HexCoord, long>();
        Visibility.Recalculate();
    }

    /// <summary>
    /// Parameterless constructor for deserialization.
    /// </summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public WorldState()
    {
        Visibility = new WorldVisibility(this);
        _layers[IslandMap.SurfaceLayer] = new LayerState();
        Civilizations = new List<SettlersOfIdlestan.Model.Civilization.Civilization>();
        Features = new List<IslandFeature>();
        PlunderCooldownDuration = new Dictionary<HexCoord, long>();
    }

    public void OnDeserialized()
    {
        Visibility.Recalculate();
    }

    public IslandMap GetMapForZ(int z)
    {
        if (Layers.TryGetValue(z, out var layer))
            return layer.Map;

        throw new ArgumentException($"No island map exists for layer z={z}.", nameof(z));
    }

    public IslandMap GetMapFor(HexCoord coord) => GetMapForZ(coord.Z);
    public IslandMap GetMapFor(Vertex vertex) => GetMapForZ(vertex.Z);
    public IslandMap GetMapFor(Edge edge) => GetMapForZ(edge.Z);

    public bool TryGetMapForZ(int z, out IslandMap map)
    {
        if (Layers.TryGetValue(z, out var layer))
        {
            map = layer.Map;
            return true;
        }

        map = null!;
        return false;
    }

    public IEnumerable<KeyValuePair<int, IslandMap>> GetMapsByZ()
    {
        foreach (var (z, layer) in Layers)
            yield return new KeyValuePair<int, IslandMap>(z, layer.Map);
    }

    private readonly Dictionary<int, Dictionary<HexCoord, long>> _harvestLastTimesByCivilization = new();

    /// <summary>
    /// Tick de simulation de la dernière récolte manuelle par civilisation et par hex (1 tick = 0.01 s).
    /// </summary>
    public IReadOnlyDictionary<int, Dictionary<HexCoord, long>> HarvestLastTimesByCivilization => _harvestLastTimesByCivilization;

    public Dictionary<HexCoord, long> GetOrCreateHarvestTimesForCiv(int civilizationIndex)
    {
        if (!_harvestLastTimesByCivilization.TryGetValue(civilizationIndex, out var perHex))
        {
            perHex = new Dictionary<HexCoord, long>();
            _harvestLastTimesByCivilization[civilizationIndex] = perHex;
        }
        return perHex;
    }

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

    private readonly Dictionary<HexCoord, long> _plunderCooldownUntil = new();

    /// <summary>
    /// Tick jusqu'auquel la récolte est bloquée sur un hex après le départ d'un monstre mobile.
    /// </summary>
    public IReadOnlyDictionary<HexCoord, long> PlunderCooldownUntil => _plunderCooldownUntil;

    public void SetPlunderCooldown(HexCoord hex, long untilTick) => _plunderCooldownUntil[hex] = untilTick;

    /// <summary>
    /// Durée totale du cooldown (en ticks) enregistrée au moment du départ du monstre.
    /// Utilisée pour calculer la progression de l'anneau dans les renderers.
    /// </summary>
    public Dictionary<HexCoord, long> PlunderCooldownDuration { get; set; }

    /// <summary>
    /// Player-controlled automation toggles. Persisted with the world state.
    /// </summary>
    public AutomationSettings AutomationSettings { get; set; } = new();

    /// <summary>
    /// Statistiques du run en cours (réinitialisées à chaque prestige).
    /// </summary>
    public RunRecord RunRecord { get; set; } = new();

    /// <summary>
    /// Tick du dernier cycle de nourrissage des soldats (toutes civilisations, global).
    /// </summary>
    public long LastSoldierFeedTick { get; set; } = 0;

    public IEnumerable<City> GetAllCities()
    {
        return Civilizations.SelectMany(c => c.Cities);
    }

    public City? FindCityAt(Vertex vertex)
    {
        return GetAllCities().FirstOrDefault(c => c.Position.Equals(vertex));
    }
}
