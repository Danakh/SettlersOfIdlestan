using System.Text.Json.Serialization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;

namespace SettlersOfIdlestan.Model.IslandMap;

public class AutomationSettings
{
    /// <summary>
    /// Référence vers les settings globaux, utilisée uniquement pour appliquer l'interrupteur
    /// GameSettings.AutomationsEnabled aux propriétés IsXActive ci-dessous. Câblée une fois via
    /// Bind (voir MainGameController.InitializeControllersForCurrentIsland), jamais sérialisée.
    /// </summary>
    [JsonIgnore]
    private GameSettings? _globalSettings;

    public void Bind(GameSettings settings) => _globalSettings = settings;

    /// <summary>Combine un flag d'automatisation individuel avec l'interrupteur global. Point d'entrée
    /// unique du "kill switch" : le reste du code doit lire les propriétés IsXActive, jamais les
    /// champs XEnabled bruts (réservés à la persistance et à l'UI de configuration par flag).</summary>
    private bool Active(bool flag) => flag && (_globalSettings?.AutomationsEnabled ?? true);

    public bool RoadAutomationEnabled { get; set; } = true;
    [JsonIgnore] public bool IsRoadAutomationActive => Active(RoadAutomationEnabled);

    public bool OutpostAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsOutpostAutomationActive => Active(OutpostAutomationEnabled);

    /// <summary>Automatisation de l'amélioration de l'Hôtel de Ville par la Guilde des bâtisseurs (dès niveau 1), voir BuildingController.PerformTownHallGuildAutomation.</summary>
    public bool TownHallAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsTownHallAutomationActive => Active(TownHallAutomationEnabled);

    /// <summary>Comme RoadAutomationEnabled, mais pour l'automatisation des routes dans l'Inframonde (nécessite la recherche Cartographie Souterraine, voir RoadController.PerformBuildersGuildConstruction).</summary>
    public bool RoadAutomationEnabledUnderworld { get; set; } = true;
    [JsonIgnore] public bool IsRoadAutomationActiveUnderworld => Active(RoadAutomationEnabledUnderworld);

    /// <summary>Comme OutpostAutomationEnabled, mais pour l'automatisation des avant-postes dans l'Inframonde (nécessite la recherche Cartographie Souterraine, voir CityBuilderController.PerformBuildersGuildOutpostConstruction).</summary>
    public bool OutpostAutomationEnabledUnderworld { get; set; } = false;
    [JsonIgnore] public bool IsOutpostAutomationActiveUnderworld => Active(OutpostAutomationEnabledUnderworld);

    public bool ProductionBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsProductionBuildingAutomationActive => Active(ProductionBuildingAutomationEnabled);

    public bool ArtisanBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsArtisanBuildingAutomationActive => Active(ArtisanBuildingAutomationEnabled);

    public bool LibraryBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsLibraryBuildingAutomationActive => Active(LibraryBuildingAutomationEnabled);

    public bool MarketBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMarketBuildingAutomationActive => Active(MarketBuildingAutomationEnabled);

    public bool SeaportBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsSeaportBuildingAutomationActive => Active(SeaportBuildingAutomationEnabled);

    public bool MilitaryBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMilitaryBuildingAutomationActive => Active(MilitaryBuildingAutomationEnabled);

    /// <summary>Automatisation de la construction/amélioration des Temples par le Grand Temple, voir BuildingController.PerformGrandTempleAutomation.</summary>
    public bool TempleAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsTempleAutomationActive => Active(TempleAutomationEnabled);

    /// <summary>Automatisation de la construction/amélioration des Mines de Mithril par la Forge Volcanique, voir BuildingController.PerformVolcanicForgeAutomation.</summary>
    public bool MithrilMineBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMithrilMineBuildingAutomationActive => Active(MithrilMineBuildingAutomationEnabled);

    /// <summary>Automatisation de la construction/amélioration des Tours de Mages et Huttes d'Alchimie par la Tour des Arcanes, voir BuildingController.PerformArcaneTowerAutomation.</summary>
    public bool ArcaneTowerBuildingAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsArcaneTowerBuildingAutomationActive => Active(ArcaneTowerBuildingAutomationEnabled);

    public bool MilitaryReinforcementAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMilitaryReinforcementAutomationActive => Active(MilitaryReinforcementAutomationEnabled);

    public bool MilitaryPatrolAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMilitaryPatrolAutomationActive => Active(MilitaryPatrolAutomationEnabled);

    public bool MilitaryVendettaAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMilitaryVendettaAutomationActive => Active(MilitaryVendettaAutomationEnabled);

    /// <summary>
    /// Démarre automatiquement (et relance après chaque palier franchi) l'investissement des
    /// Monuments (Merveille, Mine Profonde, Spire de Corruption, Faille des Abysses, Grand Phare)
    /// sur toutes les ressources de leur coût courant — mais seulement si la civilisation dispose
    /// d'un moyen de production pour chacune d'entre elles (voir MonumentInvestment.TryAutoStartInvestment).
    /// </summary>
    public bool MonumentInvestmentAutomationEnabled { get; set; } = false;
    [JsonIgnore] public bool IsMonumentInvestmentAutomationActive => Active(MonumentInvestmentAutomationEnabled);

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
    /// Dernière cible du War Herald (voir RaidEngine.StartWarHeraldRaid). Permet de détecter une
    /// réactivation sur la même cible pour désactiver tous les flux de renfort au lieu de les
    /// rediriger à nouveau. Null si le War Herald n'a jamais été activé ou vient d'être désactivé.
    /// </summary>
    public Vertex? WarHeraldTargetVertex { get; set; } = null;

    /// <summary>
    /// Index de la civilisation actuellement ciblée par la recherche Vendetta (raids automatiques).
    /// Une seule civilisation à la fois ; mis à jour après un raid manuel du joueur sur une ville
    /// ennemie ou lorsqu'une civilisation attaque le joueur (voir RaidEngine.StartRaid et
    /// CityAttackEngine.ResolveCityAttacks). Null si aucune cible valide.
    /// </summary>
    public int? VendettaTargetCivIndex { get; set; } = null;
}
