using System.Collections.Generic;

namespace SettlersOfIdlestan.Model.Tasks;

/// <summary>
/// Statistiques de l'île courante (persistées dans WorldState, réinitialisées à chaque prestige).
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
    public int DragonsDefeated { get; set; }
    public int TrollsDefeated { get; set; }
    public int OgresDefeated { get; set; }
    public int TreasuresTroveClaimed { get; set; }
    public int CivilizationsDestroyed { get; set; }

    /// <summary>
    /// Nombre d'Os Divins purifiés sur cette île, Purification ordinaire
    /// (DivineBonesController.ProcessInvestment) comme Purification Supérieure de la Nécropole
    /// (NecropolisController.HarvestBonesUnderNecropolis). Multiplie le prestige de fin de run pour
    /// qui a la Théologie de l'Ascension — voir PrestigeController.GetDivineBonesPrestigeMultiplier.
    /// </summary>
    public int DivineBonesPurified { get; set; }

    /// <summary>
    /// Niveau de pointe le plus élevé d'une zone de Corruption entièrement nettoyée <b>sur cette île</b>
    /// (Level ramené à 0), par la Spire de Corruption/la Faille des Abysses ou par le Dominion
    /// (Temple, débordement). Conditionne l'ouverture de la Faille des Abysses — voir
    /// AbyssGateController.IsAbyssGateEligible. Distinct de PrestigeState.MaxCorruptionLevelCleared,
    /// qui est le record global de la partie (bonus de prestige) et ne se réinitialise jamais : la
    /// Faille doit être re-méritée à chaque run, alors que le bonus de prestige reste acquis.
    /// </summary>
    public int MaxCorruptionLevelCleared { get; set; }

    /// <summary>Nombre de fois que chaque type de bâtiment a été construit ce run (clé = BuildingType.ToString()).</summary>
    public Dictionary<string, int> BuildingCounts { get; set; } = new();

    /// <summary>Quantité récoltée par ressource ce run (clé = Resource.ToString()).</summary>
    public Dictionary<string, int> HarvestedResources { get; set; } = new();
}
