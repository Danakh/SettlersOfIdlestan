using System;
using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Represents a civilization with a list of cities and roads.
/// </summary>
[Serializable]
public class Civilization
{
    /// <summary>
    /// Gets or sets the index of the civilization in the island state.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets the list of cities in the civilization.
    /// </summary>
    public List<SettlersOfIdlestan.Model.City.City> Cities { get; } = new();

    /// <summary>
    /// Gets the list of roads in the civilization.
    /// </summary>
    public List<SettlersOfIdlestan.Model.Road.Road> Roads { get; } = new();
}