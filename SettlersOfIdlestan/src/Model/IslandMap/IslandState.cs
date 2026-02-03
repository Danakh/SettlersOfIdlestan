using System;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the state of an island, containing the map and all civilizations.
/// </summary>
[Serializable]
public class IslandState
{
    /// <summary>
    /// Gets or sets the island map.
    /// </summary>
    public IslandMap Map { get; set; }

    /// <summary>
    /// Gets the list of civilizations on the island.
    /// </summary>
    public List<SettlersOfIdlestan.Model.Civilization.Civilization> Civilizations { get; set; }

    /// <summary>
    /// Gets the player's civilization (always at index 0).
    /// </summary>
    public SettlersOfIdlestan.Model.Civilization.Civilization PlayerCivilization => Civilizations[0];

    /// <summary>
    /// Initializes a new instance of the <see cref="IslandState"/> class.
    /// </summary>
    /// <param name="map">The island map.</param>
    /// <param name="civilizations">The list of civilizations.</param>
    public IslandState(IslandMap map, List<SettlersOfIdlestan.Model.Civilization.Civilization> civilizations)
    {
        Map = map;
        Civilizations = civilizations;
        // Initialize per-civilization per-hex last-harvest timestamps so cooldowns
        // are part of the persisted model.
        HarvestLastTimesByCivilization = new Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>>();
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
    }

    /// <summary>
    /// Tracks the last in-game time each civilization harvested each hex.
    /// Key: civilization index. Value: map HexCoord -> last harvest time.
    /// Stored here so harvest cooldowns are persisted with the island state.
    /// </summary>
    public Dictionary<int, Dictionary<SettlersOfIdlestan.Model.HexGrid.HexCoord, DateTimeOffset>> HarvestLastTimesByCivilization { get; set; }
}