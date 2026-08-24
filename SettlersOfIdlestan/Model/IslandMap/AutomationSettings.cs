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

    /// <summary>Seuil de stock (en % du max) par ressource à partir duquel la vente automatique du
    /// surplus se déclenche (recherche Marché Automatique, voir HarvestController.TryAutoTradeOnOverflow).
    /// Une ressource absente du dictionnaire vaut AutoSellThresholdDefaultPercent. Borné à
    /// [AutoSellThresholdMinPercent, AutoSellThresholdMaxPercent] par SetAutoSellThresholdPercent, seul
    /// point d'écriture censé être utilisé (le setter public reste nécessaire à la désérialisation JSON).</summary>
    public Dictionary<Resource, int> AutoSellThresholdPercentByResource { get; set; } = new();

    public const int AutoSellThresholdMinPercent = 50;
    public const int AutoSellThresholdMaxPercent = 99;
    public const int AutoSellThresholdDefaultPercent = 99;

    public int GetAutoSellThresholdPercent(Resource resource) =>
        AutoSellThresholdPercentByResource.TryGetValue(resource, out var percent) ? percent : AutoSellThresholdDefaultPercent;

    public void SetAutoSellThresholdPercent(Resource resource, int percent) =>
        AutoSellThresholdPercentByResource[resource] = Math.Clamp(percent, AutoSellThresholdMinPercent, AutoSellThresholdMaxPercent);

    /// <summary>Part de l'or (en % du max) conservée avant que l'Achat Automatique (vertex de prestige)
    /// ne dépense l'excédent, voir TradeController.TryAutoBuyOnGoldOverflow. Borné à
    /// [0, AutoBuyGoldKeepMaxPercent] par SetAutoBuyGoldKeepPercent, seul point d'écriture censé être
    /// utilisé (le setter public reste nécessaire à la désérialisation JSON).</summary>
    public int AutoBuyGoldKeepPercent { get; set; } = AutoBuyGoldKeepDefaultPercent;

    public const int AutoBuyGoldKeepMaxPercent = 99;
    public const int AutoBuyGoldKeepDefaultPercent = 99;

    public void SetAutoBuyGoldKeepPercent(int percent) =>
        AutoBuyGoldKeepPercent = Math.Clamp(percent, 0, AutoBuyGoldKeepMaxPercent);

    /// <summary>
    /// Restreint la production de soldats (Casernes ET Arsenaux) du layer indexé (Z de LayerState —
    /// 0 = surface, LayerState.UnderworldZ, LayerState.AbyssZ) au quota de soldats nourris gratuitement
    /// (Modifier.ECategory.SOLDIER_FOOD_FREE_PER_CITY), même quand ils sont actifs — voir
    /// SoldierProductionEngine.ProduceSoldiers et ProduceArsenalSoldiers. Un Arsenal désactivé reste
    /// une exception à part : il ne produit jamais, restriction ou non (voir ProduceArsenalSoldiers).
    /// Un layer non présent dans le dictionnaire équivaut à false. Réglage par layer non épinglable à
    /// l'écran de civilisation (pas de clé PinKeyXxx, voir AutomationRenderer / PlayerCivilizationPanelRenderer).
    /// Nom JSON conservé (RestrictBarracksToFreeSoldiersByLayer) pour la compatibilité des sauvegardes
    /// existantes malgré le renommage du membre C# — la restriction ne concernait que les Casernes à
    /// l'origine.
    /// </summary>
    [JsonPropertyName("RestrictBarracksToFreeSoldiersByLayer")]
    public Dictionary<int, bool> RestrictSoldierProductionToFreeSoldiersByLayer { get; set; } = new();

    public bool IsRestrictSoldierProductionToFreeSoldiersActive(int layerZ)
        => Active(RestrictSoldierProductionToFreeSoldiersByLayer.TryGetValue(layerZ, out var v) && v);

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
    /// rediriger à nouveau. Null si le War Herald n'a jamais été activé, vient d'être désactivé, ou
    /// a été implicitement désactivé par le lancement d'un Raid (voir RaidEngine.StartRaid et
    /// StartMonsterRaid) — sans quoi réactiver le War Herald sur la même cible qu'avant le raid serait
    /// interprété comme une désactivation au lieu d'une (ré)activation.
    /// </summary>
    public Vertex? WarHeraldTargetVertex { get; set; } = null;

    /// <summary>
    /// Index de la civilisation actuellement ciblée par la recherche Vendetta (raids automatiques).
    /// Une seule civilisation à la fois ; mis à jour après un raid manuel du joueur sur une ville
    /// ennemie ou lorsqu'une civilisation attaque le joueur (voir RaidEngine.StartRaid et
    /// CityAttackEngine.ResolveCityAttacks). Null si aucune cible valide.
    /// </summary>
    public int? VendettaTargetCivIndex { get; set; } = null;

    /// <summary>
    /// Reporte les interrupteurs individuels d'automatisation (les champs XEnabled, y compris la
    /// restriction de production de soldats par layer) depuis l'île précédente vers celle-ci, appelé
    /// par PrestigeController.PerformPrestige. AutomationSettings est recréé à chaque île (voir
    /// WorldState.AutomationSettings) : sans ce report, un prestige désactiverait silencieusement
    /// toutes les automatisations. L'activation est conservée même si le bâtiment unique qui la rend
    /// effective (Guilde des bâtisseurs, Académie, etc.) n'est pas encore reconstruit sur la nouvelle
    /// île — chaque automatisation vérifie déjà la présence de son bâtiment avant d'agir (voir
    /// BuildingController/RoadController/CityBuilderController). L'état éphémère lié à l'île en cours
    /// (cible de raid, Héraut de Guerre, Vendetta, épingles obsolètes) n'est volontairement pas repris.
    /// </summary>
    public void CopyEnabledTogglesFrom(AutomationSettings previous)
    {
        RoadAutomationEnabled = previous.RoadAutomationEnabled;
        OutpostAutomationEnabled = previous.OutpostAutomationEnabled;
        TownHallAutomationEnabled = previous.TownHallAutomationEnabled;
        RoadAutomationEnabledUnderworld = previous.RoadAutomationEnabledUnderworld;
        OutpostAutomationEnabledUnderworld = previous.OutpostAutomationEnabledUnderworld;
        ProductionBuildingAutomationEnabled = previous.ProductionBuildingAutomationEnabled;
        ArtisanBuildingAutomationEnabled = previous.ArtisanBuildingAutomationEnabled;
        LibraryBuildingAutomationEnabled = previous.LibraryBuildingAutomationEnabled;
        MarketBuildingAutomationEnabled = previous.MarketBuildingAutomationEnabled;
        SeaportBuildingAutomationEnabled = previous.SeaportBuildingAutomationEnabled;
        MilitaryBuildingAutomationEnabled = previous.MilitaryBuildingAutomationEnabled;
        TempleAutomationEnabled = previous.TempleAutomationEnabled;
        MithrilMineBuildingAutomationEnabled = previous.MithrilMineBuildingAutomationEnabled;
        ArcaneTowerBuildingAutomationEnabled = previous.ArcaneTowerBuildingAutomationEnabled;
        MilitaryReinforcementAutomationEnabled = previous.MilitaryReinforcementAutomationEnabled;
        MilitaryVendettaAutomationEnabled = previous.MilitaryVendettaAutomationEnabled;
        MonumentInvestmentAutomationEnabled = previous.MonumentInvestmentAutomationEnabled;
        RestrictSoldierProductionToFreeSoldiersByLayer = new Dictionary<int, bool>(previous.RestrictSoldierProductionToFreeSoldiersByLayer);
        AutoSellThresholdPercentByResource = new Dictionary<Resource, int>(previous.AutoSellThresholdPercentByResource);
        AutoBuyGoldKeepPercent = previous.AutoBuyGoldKeepPercent;
    }
}
