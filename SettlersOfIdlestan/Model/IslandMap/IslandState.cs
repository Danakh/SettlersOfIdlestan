using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model;
using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Civilization;
using System.Text.Json.Serialization;

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
        // Initialize per-civilization per-hex last-harvest timestamps so cooldowns
        // are part of the persisted model.
        HarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>>();
        // Separate timestamps for automatic production harvests performed by buildings
        AutomaticHarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>>();
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
        HarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>>();
        AutomaticHarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>>();
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
    /// Tracks the last in-game time each civilization harvested each hex.
    /// Key: civilization index. Value: map HexCoord -> last harvest time.
    /// Stored here so harvest cooldowns are persisted with the island state.
    /// </summary>
    public Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>> HarvestLastTimesByCivilization { get; set; }

    /// <summary>
    /// Tracks the last in-game time each civilization had an automatic harvest on each hex
    /// performed by producer buildings. This is separate from Manual harvest cooldowns.
    /// </summary>
    public Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>> AutomaticHarvestLastTimesByCivilization { get; set; }

    public IEnumerable<City> GetAllCities()
    {
        return Civilizations.SelectMany(c => c.Cities);
    }

    public City? FindCityAt(Vertex vertex)
    {
        return GetAllCities().FirstOrDefault(c => c.Position.Equals(vertex));
    }
}
