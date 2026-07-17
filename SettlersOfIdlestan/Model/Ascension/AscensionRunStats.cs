using System;

namespace SettlersOfIdlestan.Model.Ascension;

/// <summary>
/// Instantané des statistiques d'un cycle d'Ascension terminé (voir AscensionController.
/// PerformAscension). Conservé dans AscensionState.RunHistory, plafonné aux 5 derniers cycles.
/// </summary>
[Serializable]
public class AscensionRunStats
{
    public int MaxIslandTierReached { get; set; }
    public int MaxCorruptionReached { get; set; }
    public long TickDuration { get; set; }
    public int ResearchCompleted { get; set; }
    public int FinalPrestigePoints { get; set; }
}
