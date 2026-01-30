using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;

namespace SettlersOfIdlestan.Model.City;

/// <summary>
/// Represents a city in the game.
/// </summary>
public class City
{
    /// <summary>
    /// Gets the list of buildings in the city.
    /// </summary>
    public List<Building> Buildings { get; } = new();
}