using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

public class AutomationSettings
{
    public bool RoadAutomationEnabled { get; set; } = true;
    public bool OutpostAutomationEnabled { get; set; } = false;
    public bool ProductionBuildingAutomationEnabled { get; set; } = false;
    public bool ArtisanBuildingAutomationEnabled { get; set; } = false;
    public bool LibraryBuildingAutomationEnabled { get; set; } = false;
    public bool MarketBuildingAutomationEnabled { get; set; } = false;
    public bool SeaportBuildingAutomationEnabled { get; set; } = false;
    public bool MilitaryBuildingAutomationEnabled { get; set; } = false;
    public bool MilitaryReinforcementAutomationEnabled { get; set; } = false;
    public bool MilitaryAttackAutomationEnabled { get; set; } = false;

    /// <summary>
    /// Obsolète : remplacé par GameSettings.PinnedCivPanelKeys (persiste entre îles/redémarrages).
    /// Conservé uniquement pour migrer les anciennes sauvegardes, voir MainGameController.InitializeControllersForCurrentIsland.
    /// </summary>
    public HashSet<string> PinnedToCivPanel { get; set; } = [];

    /// <summary>Position de la ville ciblée par un raid actif. Null si aucun raid en cours ou si la cible est une MonsterFeature.</summary>
    public Vertex? RaidTargetVertex { get; set; } = null;

    /// <summary>Position de la MonsterFeature ciblée par un raid actif. Null si aucun raid en cours ou si la cible est une ville.</summary>
    public HexCoord? RaidTargetHex { get; set; } = null;

    /// <summary>Coût en or par seconde du raid actif. 0 si aucun raid. Commence à 10, monte de 2 par seconde.</summary>
    public int RaidCurrentUpkeep { get; set; } = 0;
}
