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

    /// <summary>Quantité totale récoltée par ressource (clé = Resource.ToString()).</summary>
    public Dictionary<string, int> HarvestedResources { get; set; } = new();

    /// <summary>Nombre de bâtiments de production ayant atteint le niveau 2 (cross-prestige).</summary>
    public int ProductionBuildingsReachedLevel2 { get; set; }

    /// <summary>True si une ville a eu simultanément un Port niveau 4 et un Hôtel de ville niveau 4.</summary>
    public bool HasSeaportAndTownHallLevel4SameCity { get; set; }

    /// <summary>IDs des tâches tutoriel complétées (clé = TutorialTaskId.ToString()).</summary>
    public HashSet<string> CompletedTasks { get; set; } = new();
}
