using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.IslandMap;

/// <summary>
/// Represents the state of an island, containing the map and all civilizations.
/// </summary>
public class IslandState
{
    /// <summary>
    /// Gets or sets the island map.
    /// </summary>
    public IslandMap Map { get; set; }

    /// <summary>
    /// Gets the list of civilizations on the island.
    /// </summary>
    public List<SettlersOfIdlestan.Model.Civilization.Civilization> Civilizations { get; } = new();
}