using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

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
    /// Distance to the nearest city of the same civilization (1 = adjacent to a city).
    /// </summary>
    public int DistanceToNearestCity { get; set; }

    /// <summary>
    /// True si cette route du Vide a été posée gratuitement par le sort Pont du Vide
    /// (<see cref="Controller.Island.RoadController.BuildVoidBridge"/>) plutôt que payée en points
    /// de recherche. Ces routes ne comptent pas dans l'exposant du coût des futures routes du Vide
    /// (voir <see cref="Controller.Island.RoadController.GetVoidRouteResearchCostFor"/>) : le sort
    /// ne doit pas renchérir la route du Vide classique.
    /// </summary>
    public bool BuiltBySpell { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Road"/> class with the specified position.
    /// </summary>
    /// <param name="position">The position of the road on the hex grid.</param>
    public Road(Edge position)
    {
        Position = position;
    }
}