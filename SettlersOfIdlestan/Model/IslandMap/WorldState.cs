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
    public void RemoveLayer(int z) => _layers.Remove(z);

    /// <summary>
    /// Z-coordinate of the layer currently displayed. Not persisted.
    /// </summary>
    [JsonIgnore]
    public int CurrentViewedLayer { get; set; } = IslandMap.SurfaceLayer;

    public int WorldId { get; set; }

    /// <summary>
    /// True une fois que le joueur a affiché la vue Inframonde au moins une fois sur cette île
    /// (voir OverlayRenderer.ApplyLayerForActiveTab). Sert à faire clignoter l'onglet Inframonde
    /// tant qu'il n'a pas encore été consulté après le creusement de la Mine Profonde.
    /// </summary>
    public bool HasVisitedUnderworld { get; set; }

    /// <summary>
    /// Os divins récupérés sur cette île (chaque Purification d'Os Divins en octroie 1, voir
    /// DivineBonesController). DivineBones.BonesPerEssence os se convertissent automatiquement en
    /// 1 essence divine. Volontairement stocké sur le WorldState et non le GodState : les os sont
    /// perdus au prestige — il faut en réunir 4 sur la même île.
    /// </summary>
    public int DivineBoneCount { get; set; }

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
        Layers.ContainsKey(CurrentViewedLayer) ? CurrentViewedLayer : IslandMap.SurfaceLayer)!;

    /// <summary>
    /// Transient event log for the current session. Not persisted.
    /// </summary>
    [JsonIgnore]
    public GameEventLog EventLog { get; } = new();

    /// <summary>
    /// Compteur transient incrémenté à chaque changement de type de terrain (Marche de Dieu,
    /// conversion des déserts en Filons de Mithril...) — voir <see cref="NotifyTerrainChanged"/>.
    /// Sert de clé d'invalidation aux caches dépendant du terrain, comme celui de
    /// CityBuilderController.GetBuildableVertices (restrictions raciales de placement). Non persisté.
    /// </summary>
    [JsonIgnore]
    public int TerrainVersion { get; private set; }

    /// <summary>À appeler après toute mutation de HexTile.TerrainType sur une carte de ce monde.</summary>
    public void NotifyTerrainChanged() => TerrainVersion++;

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
        RebuildHexCache();
        Visibility.Recalculate();
    }

    public IslandMap? GetMapForZ(int z)
    {
        if (Layers.TryGetValue(z, out var layer))
            return layer.Map;

        return null;
    }

    public IslandMap? GetMapFor(HexCoord coord) => GetMapForZ(coord.Z);
    public IslandMap? GetMapFor(Vertex vertex) => GetMapForZ(vertex.Z);
    public IslandMap? GetMapFor(Edge edge) => GetMapForZ(edge.Z);

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

    [JsonIgnore]
    private Dictionary<HexCoord, List<IslandFeature>> _featuresByHex = new();

    private void AddToHexCache(IslandFeature feature)
    {
        if (!_featuresByHex.TryGetValue(feature.Position, out var list))
        {
            list = new List<IslandFeature>();
            _featuresByHex[feature.Position] = list;
        }
        list.Add(feature);
    }

    private void RemoveFromHexCache(IslandFeature feature)
    {
        if (_featuresByHex.TryGetValue(feature.Position, out var list))
        {
            list.Remove(feature);
            if (list.Count == 0)
                _featuresByHex.Remove(feature.Position);
        }
    }

    private void RebuildHexCache()
    {
        _featuresByHex.Clear();
        foreach (var f in Features)
            AddToHexCache(f);
    }

    /// <summary>Retourne les features présentes sur cet hex (liste vide si aucune).</summary>
    public IReadOnlyList<IslandFeature> GetFeaturesAt(HexCoord hex)
        => _featuresByHex.TryGetValue(hex, out var list) ? list : Array.Empty<IslandFeature>();

    /// <summary>Retourne true si au moins une feature est présente sur cet hex.</summary>
    public bool HasFeaturesAt(HexCoord hex) => _featuresByHex.ContainsKey(hex);

    /// <summary>Déclenché quand une feature est ajoutée via AddFeature.</summary>
    public event EventHandler<IslandFeature>? FeatureAdded;

    /// <summary>Déclenché quand une feature est supprimée via RemoveFeature.</summary>
    public event EventHandler<IslandFeature>? FeatureRemoved;

    public void AddFeature(IslandFeature feature)
    {
        Features.Add(feature);
        AddToHexCache(feature);
        FeatureAdded?.Invoke(this, feature);
    }

    public bool RemoveFeature(IslandFeature feature)
    {
        if (!Features.Remove(feature)) return false;
        RemoveFromHexCache(feature);
        FeatureRemoved?.Invoke(this, feature);
        return true;
    }

    public void MoveFeature(IslandFeature feature, HexCoord newPosition)
    {
        RemoveFromHexCache(feature);
        feature.Position = newPosition;
        AddToHexCache(feature);
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
    /// État de la magie du joueur (rituels actifs). Réinitialisé à chaque prestige.
    /// </summary>
    public Magic.MagicState Magic { get; set; } = new();

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

    public IEnumerable<MaritimeBeacon> GetAllMaritimeBeacons()
    {
        return Civilizations.SelectMany(c => c.MaritimeBeacons);
    }

    public MaritimeBeacon? FindMaritimeBeaconAt(Vertex vertex)
    {
        return GetAllMaritimeBeacons().FirstOrDefault(b => b.Position.Equals(vertex));
    }

    public IEnumerable<WarFleet> GetAllFleets()
    {
        return Civilizations.SelectMany(c => c.Fleets);
    }

    public WarFleet? FindFleetAt(Vertex vertex)
    {
        return GetAllFleets().FirstOrDefault(f => f.Position.Equals(vertex));
    }

    public IEnumerable<MobileCamp> GetAllMobileCamps()
    {
        return Civilizations.SelectMany(c => c.MobileCamps);
    }

    public MobileCamp? FindMobileCampAt(Vertex vertex)
    {
        return GetAllMobileCamps().FirstOrDefault(c => c.Position.Equals(vertex));
    }

    /// <summary>Tous les emplacements militaires (villes, flottes et camps mobiles) de toutes les civilisations — voir IMilitaryVertex.</summary>
    public IEnumerable<IMilitaryVertex> GetAllMilitaryVertices()
    {
        return Civilizations.SelectMany(c => c.MilitaryVertices);
    }

    public IMilitaryVertex? FindMilitaryVertexAt(Vertex vertex)
    {
        return GetAllMilitaryVertices().FirstOrDefault(v => v.Position.Equals(vertex));
    }

    /// <summary>Tous les emplacements construits (villes, flottes, balises) de toutes les civilisations — voir IBuildVertex.</summary>
    public IEnumerable<IBuildVertex> GetAllBuildVertices()
    {
        return Civilizations.SelectMany(c => c.BuildVertices);
    }

    public IBuildVertex? FindBuildVertexAt(Vertex vertex)
    {
        return GetAllBuildVertices().FirstOrDefault(v => v.Position.Equals(vertex));
    }
}
