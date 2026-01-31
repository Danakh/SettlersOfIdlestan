using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.City;

/// <summary>
/// Represents a city in the game.
/// </summary>
[Serializable]
public class City
{
    /// <summary>
    /// Gets or sets the position of the city on the hex grid.
    /// </summary>
    public Vertex Position { get; set; }

    /// <summary>
    /// Gets or sets the index of the civilization this city belongs to.
    /// </summary>
    public int CivilizationIndex { get; set; }

    /// <summary>
    /// Gets the list of buildings in the city.
    /// </summary>
    public List<Building> Buildings { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="City"/> class with the specified position.
    /// </summary>
    /// <param name="position">The position of the city on the hex grid.</param>
    public City(Vertex position)
    {
        Position = position;
    }
}