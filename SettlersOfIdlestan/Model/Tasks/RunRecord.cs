using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Tasks;

/// <summary>
/// Statistiques de l'île courante (persistées dans IslandState, réinitialisées à chaque prestige).
/// </summary>
public class RunRecord
{
    public int RoadsBuilt { get; set; }
    public int CitiesBuilt { get; set; }
    public int BuildingsConstructed { get; set; }
    public int BuildingsUpgraded { get; set; }
    public int ResearchCompleted { get; set; }
    public int BanditsDefeated { get; set; }
    public int HideoutsDestroyed { get; set; }

    /// <summary>Nombre de fois que chaque type de bâtiment a été construit ce run (clé = BuildingType.ToString()).</summary>
    public Dictionary<string, int> BuildingCounts { get; set; } = new();

    /// <summary>Quantité récoltée par ressource ce run (clé = Resource.ToString()).</summary>
    public Dictionary<string, int> HarvestedResources { get; set; } = new();
}
