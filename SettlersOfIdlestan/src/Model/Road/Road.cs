using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Road;

/// <summary>
/// Represents a road connecting two cities.
/// </summary>
[Serializable]
public class Road
{
    /// <summary>
    /// Gets or sets the position of the road on the hex grid.
    /// </summary>
    public Edge Position { get; set; }

    /// <summary>
    /// Gets or sets the index of the civilization this road belongs to.
    /// </summary>
    public int CivilizationIndex { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Road"/> class with the specified position.
    /// </summary>
    /// <param name="position">The position of the road on the hex grid.</param>
    public Road(Edge position)
    {
        Position = position;
    }
}