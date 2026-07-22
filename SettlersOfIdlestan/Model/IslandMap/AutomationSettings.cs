using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

public class AutomationSettings
{
    public bool RoadAutomationEnabled { get; set; } = true;
    public bool OutpostAutomationEnabled { get; set; } = false;

    /// <summary>Comme RoadAutomationEnabled, mais pour l'automatisation des routes dans l'Inframonde (nécessite la recherche Cartographie Souterraine, voir RoadController.PerformBuildersGuildConstruction).</summary>
    public bool RoadAutomationEnabledUnderworld { get; set; } = true;

    /// <summary>Comme OutpostAutomationEnabled, mais pour l'automatisation des avant-postes dans l'Inframonde (nécessite la recherche Cartographie Souterraine, voir CityBuilderController.PerformBuildersGuildOutpostConstruction).</summary>
    public bool OutpostAutomationEnabledUnderworld { get; set; } = false;
    public bool ProductionBuildingAutomationEnabled { get; set; } = false;
    public bool ArtisanBuildingAutomationEnabled { get; set; } = false;
    public bool LibraryBuildingAutomationEnabled { get; set; } = false;
    public bool MarketBuildingAutomationEnabled { get; set; } = false;
    public bool SeaportBuildingAutomationEnabled { get; set; } = false;
    public bool MilitaryBuildingAutomationEnabled { get; set; } = false;
    public bool MilitaryReinforcementAutomationEnabled { get; set; } = false;
    public bool MilitaryPatrolAutomationEnabled { get; set; } = false;
    public bool MilitaryVendettaAutomationEnabled { get; set; } = false;

    /// <summary>
    /// Démarre automatiquement (et relance après chaque palier franchi) l'investissement des
    /// Monuments (Merveille, Mine Profonde, Spire de Corruption, Faille des Abysses, Grand Phare)
    /// sur toutes les ressources de leur coût courant — mais seulement si la civilisation dispose
    /// d'un moyen de production pour chacune d'entre elles (voir MonumentInvestment.TryAutoStartInvestment).
    /// </summary>
    public bool MonumentInvestmentAutomationEnabled { get; set; } = false;

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

    /// <summary>
    /// Index de la civilisation actuellement ciblée par la recherche Vendetta (raids automatiques).
    /// Une seule civilisation à la fois ; mis à jour après un raid manuel du joueur sur une ville
    /// ennemie ou lorsqu'une civilisation attaque le joueur (voir RaidEngine.StartRaid et
    /// CityAttackEngine.ResolveCityAttacks). Null si aucune cible valide.
    /// </summary>
    public int? VendettaTargetCivIndex { get; set; } = null;
}
