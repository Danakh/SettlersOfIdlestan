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
    public int TotalDragonsDefeated { get; set; }
    public int TotalTrollsDefeated { get; set; }
    public int TotalOgresDefeated { get; set; }

    /// <summary>Nombre de fois que chaque type de bâtiment a été construit (clé = BuildingType.ToString()).</summary>
    public Dictionary<string, int> BuildingCounts { get; set; } = new();

    /// <summary>Quantité totale récoltée par ressource (clé = Resource.ToString()).</summary>
    public Dictionary<string, int> HarvestedResources { get; set; } = new();

    /// <summary>Nombre de bâtiments de production ayant atteint le niveau 2 (cross-prestige).</summary>
    public int ProductionBuildingsReachedLevel2 { get; set; }

    /// <summary>True si une ville a eu simultanément un Port niveau 4 et un Hôtel de ville niveau 4.</summary>
    public bool HasSeaportAndTownHallLevel4SameCity { get; set; }

    /// <summary>True si un Port de pêche a atteint le niveau 4.</summary>
    public bool HasSeaportLevel4 { get; set; }

    /// <summary>True si un Hôtel de ville a atteint le niveau 4.</summary>
    public bool HasTownHallLevel4 { get; set; }

    /// <summary>Or total reçu via échanges commerciaux.</summary>
    public int TotalGoldObtainedFromTrade { get; set; }

    /// <summary>True si le joueur a acheté le sommet Caserne dans la carte de prestige.</summary>
    public bool HasPurchasedBarracksVertex { get; set; }

    /// <summary>True si un flux de renfort a été envoyé entre deux villes du joueur.</summary>
    public bool HasCreatedReinforcementFlow { get; set; }

    /// <summary>Nombre de villes ennemies détruites par le joueur (cross-prestige).</summary>
    public int TotalEnemyCitiesDestroyed { get; set; }

    /// <summary>Nombre de civilisations ennemies entièrement éliminées par le joueur (cross-prestige).</summary>
    public int TotalCivilizationsDestroyed { get; set; }

    /// <summary>True si le joueur a placé une merveille sur la carte au moins une fois.</summary>
    public bool HasPlacedWonder { get; set; }

    /// <summary>True si le joueur a construit une merveille (niveau ≥ 1) au moins une fois.</summary>
    public bool HasBuiltWonder { get; set; }

    /// <summary>Niveau maximum atteint par la merveille (cross-prestige).</summary>
    public int MaxWonderLevelReached { get; set; }

    /// <summary>True si le joueur a fondé une ville dans l'Inframonde au moins une fois.</summary>
    public bool HasFoundedUnderworldCity { get; set; }

    /// <summary>True si le joueur a entièrement construit une Spire de Corruption.</summary>
    public bool HasBuiltCorruptionSpire { get; set; }

    /// <summary>Nombre maximum de types de bâtiments différents présents simultanément sur une même île (cross-prestige).</summary>
    public int MaxUniqueBuildingTypesOnIsland { get; set; }

    /// <summary>True si le joueur a aperçu des rats pour la première fois (cross-prestige).</summary>
    public bool HasEncounteredRats { get; set; }

    /// <summary>Maximum de points de prestige gagnés en une seule partie (total gagné, pas le solde).</summary>
    public int MaxPrestigePointsInSingleRun { get; set; }

    /// <summary>Nombre maximum de villes atteint sur une seule île avant prestige (cross-prestige, ne diminue jamais).</summary>
    public int MaxCitiesInSingleRun { get; set; }

    /// <summary>Nombre maximum de bâtiments atteint sur une seule île avant prestige (cross-prestige, ne diminue jamais).</summary>
    public int MaxBuildingsInSingleRun { get; set; }

    /// <summary>Somme maximale des niveaux de bâtiments atteinte sur une seule île avant prestige (cross-prestige, ne diminue jamais).</summary>
    public int MaxTotalBuildingLevelsInSingleRun { get; set; }

    /// <summary>Nombre maximum de bâtiments uniques atteint sur une seule île avant prestige (cross-prestige, ne diminue jamais).</summary>
    public int MaxUniqueBuildingsInSingleRun { get; set; }

    /// <summary>Nombre maximum de recherches complétées sur une seule île avant prestige (cross-prestige, ne diminue jamais).</summary>
    public int MaxResearchInSingleRun { get; set; }

    /// <summary>Temps de jeu maximum (ticks) d'une seule île avant prestige (cross-prestige, ne diminue jamais).</summary>
    public long MaxPlaytimeInSingleRun { get; set; }

    /// <summary>True si une Mine Profonde a été creusée au moins une fois (cross-prestige).</summary>
    public bool HasDugDeepestMine { get; set; }

    /// <summary>True si une Faille des Abysses a été construite au moins une fois (cross-prestige).</summary>
    public bool HasBuiltAbyssGate { get; set; }

    /// <summary>IDs des tâches tutoriel complétées (clé = TutorialTaskId.ToString()).</summary>
    public HashSet<string> CompletedTasks { get; set; } = new();

    /// <summary>IDs des achievements débloqués (clé = AchievementId.ToString()).</summary>
    public HashSet<string> CompletedAchievements { get; set; } = new();
}
