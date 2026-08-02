using System;

namespace SettlersOfIdlestan.Model.Prestige;

[Serializable]
public class PrestigeRunStats
{
    public int WorldId { get; set; }
    public long TickDuration { get; set; }
    public int CityCount { get; set; }
    public int BuildingCount { get; set; }
    public int TotalBuildingLevels { get; set; }
    public int PrestigePoints { get; set; }
    public int ResearchCompleted { get; set; }
    public int UniqueBuildings { get; set; }
    public int WonderLevel { get; set; }
    public bool HasDeepestMine { get; set; }
    public bool HasCorruptionSpire { get; set; }
    public bool HasAbyssGate { get; set; }

    /// <summary>Tier d'île (PrestigeState.Tier) au moment du prestige. 0 pour les entrées d'historique antérieures à l'ajout de ce champ.</summary>
    public int Tier { get; set; }

    /// <summary>Niveau de corruption (PrestigeState.CurrentCorruptionLevel) au moment du prestige. 0 pour les entrées d'historique antérieures à l'ajout de ce champ.</summary>
    public int Corruption { get; set; }
}
