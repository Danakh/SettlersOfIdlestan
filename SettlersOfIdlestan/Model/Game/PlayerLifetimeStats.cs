using System;

namespace SettlersOfIdlestan.Model.Game;

/// <summary>
/// Statistiques cumulatives à vie du joueur, persistées indépendamment de la sauvegarde de partie
/// (survivent à "Nouvelle partie" et au hard reset). Sert de source pour les stats Steam.
/// </summary>
[Serializable]
public class PlayerLifetimeStats
{
    public int TotalPrestigesPerformed { get; set; }
    public int TotalPrestigePointsEarned { get; set; }
    public int TotalGodPointsEarned { get; set; }
    public int MaxMonstersDefeatedInSingleRun { get; set; }
    public int MaxCitiesFoundedInSingleRun { get; set; }
}
