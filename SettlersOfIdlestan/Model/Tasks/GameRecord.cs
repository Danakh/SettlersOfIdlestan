using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Tasks;

/// <summary>
/// Statistiques cumulatives cross-prestige (persistées dans GodState).
/// Sert de base pour les achievements et les tâches tutoriel.
/// </summary>
public class GameRecord
{
    public int TotalRoadsBuilt { get; set; }
    public int TotalCitiesBuilt { get; set; }
    public int TotalBuildingsConstructed { get; set; }
    public int TotalBuildingsUpgraded { get; set; }
    public int TotalResearchCompleted { get; set; }
    public int TotalPrestigeVerticesPurchased { get; set; }
    public int TotalPrestigesPerformed { get; set; }
    public int TotalBanditsDefeated { get; set; }
    public int TotalHideoutsDestroyed { get; set; }

    /// <summary>Nombre de fois que chaque type de bâtiment a été construit (clé = BuildingType.ToString()).</summary>
    public Dictionary<string, int> BuildingCounts { get; set; } = new();

    /// <summary>IDs des tâches tutoriel complétées (clé = TutorialTaskId.ToString()).</summary>
    public HashSet<string> CompletedTasks { get; set; } = new();
}
