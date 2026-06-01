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
/// Represents the state of an island, containing the map and all civilizations.
/// </summary>
[Serializable]
public class IslandState : IJsonOnDeserialized
{
    public IslandMap Map { get; set; }

    /// <summary>
    /// The Underworld — created when the DeepestMine building is constructed. Null until then.
    /// </summary>
    public UnderworldState? Underworld { get; set; }

    /// <summary>
    /// Runtime toggle: true while the player is viewing the Underworld map. Not persisted.
    /// </summary>
    [JsonIgnore]
    public bool IsViewingUnderworld { get; set; }

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


    [JsonIgnore]
    private Dictionary<int, Dictionary<int, VisibleIslandMap>> VisibleIslandMapsByZ { get; set; } = new();


    [JsonIgnore]
    public int CurrentMapZ => IsViewingUnderworld && Underworld != null
        ? HexCoord.UnderworldZ
        : HexCoord.SurfaceZ;

    [JsonIgnore]
    public IslandMap CurrentMap => GetMapForZ(CurrentMapZ);

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
        Features = new List<IslandFeature>();
        BanditCooldownUntil = new Dictionary<HexCoord, long>();
    }

    public void OnDeserialized()
    {
        NormalizeUnderworldCitiesIntoCivilizations();
        RecalculateVisibleIslandMaps();
    }

    /// <summary>
    /// Rebuilds visible maps for every civilization.
    /// </summary>
    public void RecalculateVisibleIslandMaps()
    {
        NormalizeUnderworldCitiesIntoCivilizations();

        VisibleIslandMapsByZ = GetMapsByZ().ToDictionary(
            kvp => kvp.Key,
            kvp => Civilizations.ToDictionary(
                civilization => civilization.Index,
                civilization => new VisibleIslandMap(kvp.Value, civilization)));
    }

    /// <summary>
    /// Rebuilds the visible map for one civilization after a road or city changed.
    /// </summary>
    public void RecalculateVisibleIslandMap(int civilizationIndex)
    {
        var civilization = Civilizations.FirstOrDefault(c => c.Index == civilizationIndex)
            ?? throw new ArgumentException("Civilization not found", nameof(civilizationIndex));

        NormalizeUnderworldCitiesIntoCivilizations();

        foreach (var (z, map) in GetMapsByZ())
        {
            if (!VisibleIslandMapsByZ.TryGetValue(z, out var visibleMaps))
            {
                visibleMaps = new Dictionary<int, VisibleIslandMap>();
                VisibleIslandMapsByZ[z] = visibleMaps;
            }

            visibleMaps[civilizationIndex] = new VisibleIslandMap(map, civilization);
        }
    }

    public IReadOnlyDictionary<int, VisibleIslandMap> GetVisibleIslandMapsForZ(int z)
    {
        if (!VisibleIslandMapsByZ.TryGetValue(z, out var visibleMaps))
        {
            var map = GetMapForZ(z);
            visibleMaps = Civilizations.ToDictionary(
                civilization => civilization.Index,
                civilization => new VisibleIslandMap(map, civilization));
            VisibleIslandMapsByZ[z] = visibleMaps;
        }

        return visibleMaps;
    }

    public IslandMap GetMapForZ(int z)
    {
        if (z == Map.Z)
            return Map;

        if (Underworld?.Map.Z == z)
            return Underworld.Map;

        throw new ArgumentException($"No island map exists for layer z={z}.", nameof(z));
    }

    public IslandMap GetMapFor(HexCoord coord) => GetMapForZ(coord.Z);
    public IslandMap GetMapFor(Vertex vertex) => GetMapForZ(vertex.Z);
    public IslandMap GetMapFor(Edge edge) => GetMapForZ(edge.Z);

    public bool TryGetMapForZ(int z, out IslandMap map)
    {
        if (z == Map.Z)
        {
            map = Map;
            return true;
        }

        if (Underworld?.Map.Z == z)
        {
            map = Underworld.Map;
            return true;
        }

        map = null!;
        return false;
    }

    public IEnumerable<KeyValuePair<int, IslandMap>> GetMapsByZ()
    {
        yield return new KeyValuePair<int, IslandMap>(Map.Z, Map);

        if (Underworld != null)
            yield return new KeyValuePair<int, IslandMap>(Underworld.Map.Z, Underworld.Map);
    }

    public void NormalizeUnderworldCitiesIntoCivilizations()
    {
        if (Underworld == null || Underworld.Cities.Count == 0)
            return;

        foreach (var city in Underworld.Cities)
        {
            if (city.Position.Z != Underworld.Map.Z)
                throw new InvalidOperationException("Underworld city is not on the underworld map layer.");

            var civilization = Civilizations.FirstOrDefault(c => c.Index == city.CivilizationIndex);
            if (civilization == null)
                continue;

            if (!civilization.Cities.Any(existing => existing.Position.Equals(city.Position)))
                civilization.Cities.Add(city);
        }
    }

    /// <summary>
    /// Tick de simulation de la dernière récolte manuelle par civilisation et par hex (1 tick = 0.01 s).
    /// </summary>
    public Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, long>> HarvestLastTimesByCivilization { get; set; }

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
