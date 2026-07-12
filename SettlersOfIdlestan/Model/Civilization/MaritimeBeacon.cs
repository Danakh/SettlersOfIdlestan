using System;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.Civilization;

/// <summary>
/// Balise maritime : structure posée sur un vertex entouré de 3 hexagones d'eau non profonde.
/// Sert d'ancrage côtier artificiel pour les routes maritimes (RoadController), permettant de les
/// prolonger en pleine mer au-delà de la côte.
/// </summary>
[Serializable]
public class MaritimeBeacon : IBuildVertex
{
    /// <summary>
    /// Gets or sets the position of the beacon on the hex grid.
    /// </summary>
    public Vertex Position { get; set; }

    /// <summary>
    /// Gets or sets the index of the civilization this beacon belongs to.
    /// </summary>
    public int CivilizationIndex { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaritimeBeacon"/> class with the specified position.
    /// </summary>
    /// <param name="position">The position of the beacon on the hex grid.</param>
    public MaritimeBeacon(Vertex position)
    {
        Position = position;
    }
}
