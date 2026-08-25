using SettlersOfIdlestan.Controller.Ascension;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Ascension;
using SettlersOfIdlestan.Model.Buildings;
using static SettlersOfIdlestan.Model.GameplayModifier.Modifier;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services;
using System;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public sealed class PlayerCivilizationPanelRenderer : PanelRendererBase
{
    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly Action _closeAll;
    private readonly TradePopupRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private TargetSelectionService? _targetSelectionService;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly ResourceManager _resourceManager;
    private readonly Action<int, float, float> _centerCameraOnMapPosition;

    public bool IsCollapsed  => Collapsed;
    public void Collapse()   => Collapsed = true;
    public Action? OnExpanded { get; set; }

    private bool _disposed;

    public PlayerCivilizationPanelRenderer(
        GameControllerService gameControllerService,
        LocalizationService localization,
        Action closeAll,
        TradePopupRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        TargetSelectionService? targetSelectionService,
        TooltipRenderer tooltipRenderer,
        ResourceManager resourceManager,
        Action<int, float, float> centerCameraOnMapPosition)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _closeAll = closeAll;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _targetSelectionService = targetSelectionService;
        _tooltipRenderer = tooltipRenderer;
        _resourceManager = resourceManager;
        _centerCameraOnMapPosition = centerCameraOnMapPosition;
    }

    public void ConnectTargetSelectionService(TargetSelectionService service)
        => _targetSelectionService = service;

    // ── Visibilité et disponibilité des actions ───────────────────────────────
    //
    // Source de vérité unique, partagée par le rendu Skia, son hit-testing, l'instantané destiné
    // à l'hôte et l'exécution des actions. Ces règles étaient auparavant évaluées dans Render et
    // mémorisées dans des champs : le clic les relisait donc avec une frame de retard, et une
    // fois le panneau porté par l'hôte, Render ne tourne plus du tout et les champs restaient
    // figés sur leur dernière valeur.

    private AscensionController Ascension => _gameControllerService.MainGameController.AscensionController;

    private int CurrentLayer => _gameControllerService.CurrentWorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;

    private bool WonderVisible => IsWonderVisible() && CanPlaceWonder();
    private bool WonderEnabled => WonderVisible && CurrentLayer == IslandMap.SurfaceLayer;

    private bool GreatLighthouseVisible => IsGreatLighthouseVisible() && CanPlaceGreatLighthouse();
    private bool GreatLighthouseEnabled => GreatLighthouseVisible && CurrentLayer == IslandMap.SurfaceLayer;

    private bool ObservatoryVisible => CanPlaceObservatory();
    private bool ObservatoryEnabled => ObservatoryVisible && CurrentLayer == IslandMap.SurfaceLayer;

    // La Nécropole se bâtit sur des Os Divins, qui n'existent que sur les îles de l'Abysse.
    private bool NecropolisVisible => CanPlaceNecropolis();
    private bool NecropolisEnabled => NecropolisVisible && CurrentLayer == LayerState.AbyssZ;

    /// <summary>Purification Supérieure : la Nécropole récolte les Os Divins au lieu de les
    /// détruire — l'invite de sélection et l'infobulle doivent le dire (voir NecropolisController).</summary>
    private bool NecropolisHarvestsBones =>
        _gameControllerService.CurrentGameState?.GodState.AscensionState.IsGreaterPurificationActive ?? false;

    private bool DeepestMineVisible => CanPlaceDeepestMine();
    private bool DeepestMineEnabled => DeepestMineVisible && CurrentLayer == IslandMap.SurfaceLayer;

    private bool SpireVisible => CanPlaceSpire();
    private bool SpireEnabled => SpireVisible && CurrentLayer == LayerState.UnderworldZ;

    private bool RelocationVisible => IsRelocationVisible();
    private bool RelocationEnabled => RelocationVisible && CanAffordRelocation();

    private bool WalkOfGodVisible => Ascension.IsPowerUnlocked(AscensionPowerId.WalkOfGod);
    private bool WalkOfGodEnabled => WalkOfGodVisible
                                  && CurrentLayer == IslandMap.SurfaceLayer
                                  && Ascension.GetWalkOfGodTargetHexes().Count > 0
                                  && Ascension.CanUseWalkOfGod();

    private bool PresenceOfGodVisible => Ascension.IsPowerUnlocked(AscensionPowerId.PresenceOfGod);
    private bool PresenceOfGodEnabled => PresenceOfGodVisible
                                      && CurrentLayer == IslandMap.SurfaceLayer
                                      && Ascension.GetPresenceOfGodTargetHexes().Count > 0
                                      && Ascension.CanUsePresenceOfGod();

    // Poing de Dieu frappe sur n'importe quel calque (voir GetFistOfGodTargetHexes) : pas de garde
    // de surface, contrairement aux deux pouvoirs ci-dessus.
    private bool FistOfGodVisible => Ascension.IsPowerUnlocked(AscensionPowerId.FistOfGod);
    private bool FistOfGodEnabled => FistOfGodVisible
                                  && Ascension.GetFistOfGodTargetHexes().Count > 0
                                  && Ascension.CanUseFistOfGod();

    // ── Actions ───────────────────────────────────────────────────────────────
    //
    // Chaque action porte sa propre garde de disponibilité plutôt que de la laisser à l'appelant :
    // elles sont déclenchées aussi bien par le hit-testing Skia que par le panneau porté par
    // l'hôte, et une garde dupliquée finirait par diverger entre les deux.

    private void DoTrade()
    {
        if (!IsTradeVisible()) return;
        _closeAll();
        _tradeRenderer.Open();
    }

    private void DoPrestige()
    {
        if (!IsPrestigeAvailable()) return;
        _closeAll();
        _prestigeRenderer.Open();
    }

    private void DoWonder()
    {
        if (!WonderEnabled || _targetSelectionService == null) return;
        _closeAll();
        var wonderController = _gameControllerService.MainGameController.WonderController;
        _targetSelectionService.EnterHexSelection("wonder_select_hex", wonderController.GetPlaceableHexes(),
            hex => wonderController.PlaceWonder(hex), TargetSelectionTheme.Friendly);
    }

    private void DoGreatLighthouse()
    {
        if (!GreatLighthouseEnabled || _targetSelectionService == null) return;
        _closeAll();
        var greatLighthouseController = _gameControllerService.MainGameController.GreatLighthouseController;
        _targetSelectionService.EnterHexSelection("great_lighthouse_select_hex", greatLighthouseController.GetPlaceableHexes(),
            hex => greatLighthouseController.PlaceGreatLighthouse(hex), TargetSelectionTheme.Friendly);
    }

    private void DoObservatory()
    {
        if (!ObservatoryEnabled || _targetSelectionService == null) return;
        _closeAll();
        var observatoryController = _gameControllerService.MainGameController.ObservatoryController;
        _targetSelectionService.EnterHexSelection("observatory_select_hex", observatoryController.GetPlaceableHexes(),
            hex => observatoryController.PlaceObservatory(hex), TargetSelectionTheme.Friendly);
    }

    private void DoNecropolis()
    {
        if (!NecropolisEnabled || _targetSelectionService == null) return;
        _closeAll();
        var necropolisController = _gameControllerService.MainGameController.NecropolisController;
        _targetSelectionService.EnterHexSelection(
            NecropolisHarvestsBones ? "necropolis_select_hex_harvest" : "necropolis_select_hex",
            necropolisController.GetPlaceableHexes(),
            hex => necropolisController.PlaceNecropolis(hex), TargetSelectionTheme.Friendly);
    }

    private void DoDeepestMine()
    {
        if (!DeepestMineEnabled || _targetSelectionService == null) return;
        _closeAll();
        var deepestMineController = _gameControllerService.MainGameController.DeepestMineController;
        _targetSelectionService.EnterHexSelection("deepest_mine_select_hex", deepestMineController.GetPlaceableHexes(),
            hex => deepestMineController.PlaceDeepestMine(hex), TargetSelectionTheme.Friendly);
    }

    private void DoSpire()
    {
        if (!SpireEnabled || _targetSelectionService == null) return;
        _closeAll();
        var spireController = _gameControllerService.MainGameController.CorruptionSpireController;
        var spireHexes = spireController.GetPlaceableHexes();
        var spireHexLabels = spireHexes.ToDictionary(hex => hex,
            hex => _localization.GetFormated("map_switch_corruption_level", spireController.GetCorruptionLevel(hex)));
        _targetSelectionService.EnterHexSelection("spire_select_hex", spireHexes,
            hex => spireController.PlaceCorruptionSpire(hex), TargetSelectionTheme.Friendly, spireHexLabels);
    }

    /// <summary>Lance un pillage, ou arrête celui en cours si le bouton est déjà actif.</summary>
    private void DoRaid()
    {
        if (!IsRaidVisible()) return;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null) return;

        if (IsRaidActive())
        {
            _gameControllerService.MainGameController.MilitaryController.StopRaid(playerCiv);
            return;
        }

        if (_targetSelectionService == null) return;
        _closeAll();
        var militaryController = _gameControllerService.MainGameController.MilitaryController;
        var cityTargets = militaryController.GetSelectableTargets(playerCiv);
        var monsterTargets = militaryController.GetSelectableMonsterTargets();
        if (cityTargets.Count > 0 || monsterTargets.Count > 0)
            _targetSelectionService.EnterMixedSelection("raid_select_city",
                cityTargets, target => militaryController.StartRaid(playerCiv, target),
                monsterTargets, target => militaryController.StartMonsterRaid(playerCiv, target),
                TargetSelectionTheme.Hostile);
    }

    private void DoWarHerald()
    {
        if (!IsWarHeraldVisible() || _targetSelectionService == null) return;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null) return;

        _closeAll();
        var militaryController = _gameControllerService.MainGameController.MilitaryController;
        var allyTargets = militaryController.GetWarHeraldTargets(playerCiv);
        if (allyTargets.Count > 0)
            _targetSelectionService.EnterVertexSelection("warherald_select_target", allyTargets,
                target => militaryController.StartWarHeraldRaid(playerCiv, target),
                TargetSelectionTheme.Friendly);
    }

    private void DoRelocation()
    {
        if (!RelocationEnabled || _targetSelectionService == null) return;
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv == null) return;

        _closeAll();
        var cityBuilderController = _gameControllerService.MainGameController.CityBuilderController;
        var cityTargets = playerCiv.Cities.Select(c => c.Position).ToList();
        if (cityTargets.Count == 0) return;

        _targetSelectionService.EnterVertexSelection("relocation_select_city", cityTargets,
            source =>
            {
                var city = playerCiv.Cities.FirstOrDefault(c => c.Position.Equals(source));
                if (city == null) return;
                var destinations = cityBuilderController.GetRelocationTargets(city);
                if (destinations.Count == 0) return;
                _targetSelectionService.EnterVertexSelection("relocation_select_destination", destinations,
                    destination => cityBuilderController.RelocateCity(city, destination),
                    TargetSelectionTheme.Friendly,
                    secondaryActionLabelKey: "relocation_destroy_city",
                    secondaryAction: () => cityBuilderController.DestroyCity(city, CityDestructionCause.PlayerChoice));
            }, TargetSelectionTheme.Friendly);
    }

    private void DoWalkOfGod()
    {
        if (!WalkOfGodEnabled || _targetSelectionService == null) return;
        _closeAll();
        var ascension = Ascension;
        _targetSelectionService.EnterHexSelection("walkofgod_select_hex", ascension.GetWalkOfGodTargetHexes(),
            hex => ascension.ApplyWalkOfGod(hex), TargetSelectionTheme.Friendly);
    }

    private void DoPresenceOfGod()
    {
        if (!PresenceOfGodEnabled || _targetSelectionService == null) return;
        _closeAll();
        var ascension = Ascension;
        _targetSelectionService.EnterHexSelection("presenceofgod_select_hex", ascension.GetPresenceOfGodTargetHexes(),
            hex => ascension.ApplyPresenceOfGod(hex), TargetSelectionTheme.Friendly);
    }

    private void DoFistOfGod()
    {
        if (!FistOfGodEnabled || _targetSelectionService == null) return;
        _closeAll();
        var ascension = Ascension;
        _targetSelectionService.EnterHexSelection("fistofgod_select_hex", ascension.GetFistOfGodTargetHexes(),
            hex => ascension.ApplyFistOfGod(hex), TargetSelectionTheme.Hostile);
    }

    private void HandlePinnedToggle(string key, Civilization? civ, SettlersOfIdlestan.Model.IslandMap.WorldState? worldState)
    {
        var settings = worldState?.AutomationSettings;
        switch (key)
        {
            case AutomationRenderer.PinKeyBarracks:      if (civ != null) ToggleAll<Barracks>(civ);    break;
            case AutomationRenderer.PinKeyArsenal:       if (civ != null) ToggleAll<Arsenal>(civ);     break;
            case AutomationRenderer.PinKeyLaboratory:    if (civ != null) ToggleAll<Laboratory>(civ);  break;
            case AutomationRenderer.PinKeySmelter:       if (civ != null) ToggleAll<Smelter>(civ);     break;
            case AutomationRenderer.PinKeyWeaponSmith:   if (civ != null) ToggleAll<WeaponSmith>(civ); break;
            case AutomationRenderer.PinKeyArmorSmith:    if (civ != null) ToggleAll<ArmorSmith>(civ);  break;
            case AutomationRenderer.PinKeyAlchimistHut:  if (civ != null) ToggleAll<AlchimistHut>(civ); break;
            case AutomationRenderer.PinKeyTownHall:      if (settings != null) settings.TownHallAutomationEnabled = !settings.TownHallAutomationEnabled;                   break;
            case AutomationRenderer.PinKeyGrandTemple:   if (settings != null) settings.TempleAutomationEnabled = !settings.TempleAutomationEnabled;                       break;
            case AutomationRenderer.PinKeyMithrilMine:   if (settings != null) settings.MithrilMineBuildingAutomationEnabled = !settings.MithrilMineBuildingAutomationEnabled;   break;
            case AutomationRenderer.PinKeyArcaneTower:   if (settings != null) settings.ArcaneTowerBuildingAutomationEnabled = !settings.ArcaneTowerBuildingAutomationEnabled;   break;
            case AutomationRenderer.PinKeyMonumentInvestment: if (settings != null) settings.MonumentInvestmentAutomationEnabled = !settings.MonumentInvestmentAutomationEnabled; break;
            case AutomationRenderer.PinKeyRoad:          if (settings != null) settings.RoadAutomationEnabled = !settings.RoadAutomationEnabled;                           break;
            case AutomationRenderer.PinKeyOutpost:       if (settings != null) settings.OutpostAutomationEnabled = !settings.OutpostAutomationEnabled;                     break;
            case AutomationRenderer.PinKeyRoadUnderworld:    if (settings != null) settings.RoadAutomationEnabledUnderworld = !settings.RoadAutomationEnabledUnderworld;       break;
            case AutomationRenderer.PinKeyOutpostUnderworld: if (settings != null) settings.OutpostAutomationEnabledUnderworld = !settings.OutpostAutomationEnabledUnderworld; break;
            case AutomationRenderer.PinKeyProduction:    if (settings != null) settings.ProductionBuildingAutomationEnabled = !settings.ProductionBuildingAutomationEnabled; break;
            case AutomationRenderer.PinKeyArtisan:       if (settings != null) settings.ArtisanBuildingAutomationEnabled = !settings.ArtisanBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyLibrary:       if (settings != null) settings.LibraryBuildingAutomationEnabled = !settings.LibraryBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyMarket:        if (settings != null) settings.MarketBuildingAutomationEnabled = !settings.MarketBuildingAutomationEnabled;       break;
            case AutomationRenderer.PinKeySeaport:       if (settings != null) settings.SeaportBuildingAutomationEnabled = !settings.SeaportBuildingAutomationEnabled;     break;
            case AutomationRenderer.PinKeyMilBuildings:  if (settings != null) settings.MilitaryBuildingAutomationEnabled = !settings.MilitaryBuildingAutomationEnabled;   break;
            case AutomationRenderer.PinKeyMilReinforce:
                if (settings != null)
                {
                    settings.MilitaryReinforcementAutomationEnabled = !settings.MilitaryReinforcementAutomationEnabled;
                    if (!settings.MilitaryReinforcementAutomationEnabled && civ != null)
                        _gameControllerService.MainGameController.MilitaryController.ClearReinforcementFlows(civ);
                }
                break;
            case AutomationRenderer.PinKeyMilVendetta:
                if (settings != null)
                {
                    settings.MilitaryVendettaAutomationEnabled = !settings.MilitaryVendettaAutomationEnabled;
                    if (civ != null)
                        _gameControllerService.MainGameController.MilitaryController.StopRaid(civ);
                }
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProduction:
                ToggleRestrictSoldierProductionByLayer(settings, IslandMap.SurfaceLayer);
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld:
                ToggleRestrictSoldierProductionByLayer(settings, LayerState.UnderworldZ);
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProductionAbyss:
                ToggleRestrictSoldierProductionByLayer(settings, LayerState.AbyssZ);
                break;
            case AutomationRenderer.PinKeyRestrictSoldierProductionPandemonium:
                ToggleRestrictSoldierProductionByLayer(settings, LayerState.PandemoniumZ);
                break;
        }
    }

    private static void ToggleRestrictSoldierProductionByLayer(AutomationSettings? settings, int layerZ)
    {
        if (settings == null) return;
        var byLayer = settings.RestrictSoldierProductionToFreeSoldiersByLayer;
        bool current = byLayer.TryGetValue(layerZ, out var v) && v;
        byLayer[layerZ] = !current;
    }

    private static bool IsRestrictSoldierProductionByLayer(AutomationSettings settings, int layerZ)
        => settings.RestrictSoldierProductionToFreeSoldiersByLayer.TryGetValue(layerZ, out var v) && v;

    private static bool IsKeyShowable(string key,
        SettlersOfIdlestan.Model.IslandMap.WorldState? worldState,
        bool hasBarracks, bool hasArsenal, bool hasLabs, bool hasSmelters,
        bool hasWeaponSmiths, bool hasArmorSmiths, bool hasAlchimistHuts,
        IReadOnlyDictionary<string, bool> structuralUnlocks, int freePerCitySoldierQuota)
    {
        if (worldState == null) return false;

        return key switch
        {
            AutomationRenderer.PinKeyBarracks     => hasBarracks,
            AutomationRenderer.PinKeyArsenal      => hasArsenal,
            AutomationRenderer.PinKeyLaboratory   => hasLabs,
            AutomationRenderer.PinKeySmelter      => hasSmelters,
            AutomationRenderer.PinKeyWeaponSmith  => hasWeaponSmiths,
            AutomationRenderer.PinKeyArmorSmith   => hasArmorSmiths,
            AutomationRenderer.PinKeyAlchimistHut => hasAlchimistHuts,
            AutomationRenderer.PinKeyRestrictSoldierProduction or
            AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld or
            AutomationRenderer.PinKeyRestrictSoldierProductionAbyss or
            AutomationRenderer.PinKeyRestrictSoldierProductionPandemonium =>
                freePerCitySoldierQuota > 0 && (hasBarracks || hasArsenal),
            // Bascules structurelles (guildes, techs...) : masquees si le deblocage a ete perdu.
            _ => structuralUnlocks.GetValueOrDefault(key, true),
        };
    }

    /// <summary>
    /// Etat, libellé et infobulle d'une bascule épinglée. Source de vérité unique, partagée par
    /// le rendu Skia et l'instantané destiné à l'hôte : les deux affichages ne peuvent pas
    /// diverger sur ce qu'une bascule montre.
    /// </summary>
    private (bool? Value, string NameKey, string TooltipKey) ResolvePinnedToggle(
        string key, Civilization civ, SettlersOfIdlestan.Model.IslandMap.WorldState? worldState)
    {
        switch (key)
        {
            case AutomationRenderer.PinKeyBarracks:     return (AreAllActiveNullable<Barracks>(civ),     "building_barracks_name",     "tooltip_toggle_barracks");
            case AutomationRenderer.PinKeyArsenal:      return (AreAllActiveNullable<Arsenal>(civ),      "building_arsenal_name",      "tooltip_toggle_arsenal");
            case AutomationRenderer.PinKeyLaboratory:   return (AreAllActiveNullable<Laboratory>(civ),   "building_laboratory_name",   "tooltip_toggle_lab");
            case AutomationRenderer.PinKeySmelter:      return (AreAllActiveNullable<Smelter>(civ),      "building_smelter_name",      "tooltip_toggle_smelter");
            case AutomationRenderer.PinKeyWeaponSmith:  return (AreAllActiveNullable<WeaponSmith>(civ),  "building_weaponsmith_name",  "tooltip_toggle_weaponsmith");
            case AutomationRenderer.PinKeyArmorSmith:   return (AreAllActiveNullable<ArmorSmith>(civ),   "building_armorsmith_name",   "tooltip_toggle_armorsmith");
            case AutomationRenderer.PinKeyAlchimistHut: return (AreAllActiveNullable<AlchimistHut>(civ), "building_alchimisthut_name", "tooltip_toggle_alchimisthut");
        }

        var settings = worldState?.AutomationSettings;
        if (settings == null) return (false, key, GetAutomationPinDescKey(key));

        // Seule la valeur depend d'un switch : le libelle vient de la table des racines, pour
        // qu'un automatisme ne puisse pas etre bascule ici sans y etre nomme.
        bool value = key switch
        {
            AutomationRenderer.PinKeyTownHall     => settings.TownHallAutomationEnabled,
            AutomationRenderer.PinKeyGrandTemple  => settings.TempleAutomationEnabled,
            AutomationRenderer.PinKeyMithrilMine  => settings.MithrilMineBuildingAutomationEnabled,
            AutomationRenderer.PinKeyArcaneTower  => settings.ArcaneTowerBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMonumentInvestment => settings.MonumentInvestmentAutomationEnabled,
            AutomationRenderer.PinKeyRoad         => settings.RoadAutomationEnabled,
            AutomationRenderer.PinKeyOutpost      => settings.OutpostAutomationEnabled,
            AutomationRenderer.PinKeyRoadUnderworld    => settings.RoadAutomationEnabledUnderworld,
            AutomationRenderer.PinKeyOutpostUnderworld => settings.OutpostAutomationEnabledUnderworld,
            AutomationRenderer.PinKeyProduction   => settings.ProductionBuildingAutomationEnabled,
            AutomationRenderer.PinKeyArtisan      => settings.ArtisanBuildingAutomationEnabled,
            AutomationRenderer.PinKeyLibrary      => settings.LibraryBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMarket       => settings.MarketBuildingAutomationEnabled,
            AutomationRenderer.PinKeySeaport      => settings.SeaportBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMilBuildings => settings.MilitaryBuildingAutomationEnabled,
            AutomationRenderer.PinKeyMilReinforce => settings.MilitaryReinforcementAutomationEnabled,
            AutomationRenderer.PinKeyMilVendetta  => settings.MilitaryVendettaAutomationEnabled,
            AutomationRenderer.PinKeyRestrictSoldierProduction =>
                IsRestrictSoldierProductionByLayer(settings, IslandMap.SurfaceLayer),
            AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld =>
                IsRestrictSoldierProductionByLayer(settings, LayerState.UnderworldZ),
            AutomationRenderer.PinKeyRestrictSoldierProductionAbyss =>
                IsRestrictSoldierProductionByLayer(settings, LayerState.AbyssZ),
            AutomationRenderer.PinKeyRestrictSoldierProductionPandemonium =>
                IsRestrictSoldierProductionByLayer(settings, LayerState.PandemoniumZ),
            _ => false,
        };

        string nameKey = AutomationPinLocalizationRoots.TryGetValue(key, out var root) ? $"{root}_name" : key;

        return (value, nameKey, GetAutomationPinDescKey(key));
    }

    /// <summary>
    /// Racine de clé de localisation de chaque automatisme épinglable : le libellé est
    /// <c>{racine}_name</c> et la description <c>{racine}_desc</c>.
    ///
    /// Une seule table plutôt que deux switch en miroir — ils divergeaient déjà, et c'est ainsi
    /// que cinq automatismes (hôtel de ville, grand temple, mine de mithril, tour des arcanes,
    /// investissement monument) se retrouvaient épinglables mais sans libellé ici.
    ///
    /// Toute case à cocher d'épinglage ajoutée dans <see cref="AutomationRenderer"/> doit y
    /// figurer, sinon le panneau afficherait la clé brute. Un test le vérifie.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AutomationPinLocalizationRoots =
        new Dictionary<string, string>
        {
            [AutomationRenderer.PinKeyTownHall]           = "automation_townhall",
            [AutomationRenderer.PinKeyGrandTemple]        = "automation_grandtemple",
            [AutomationRenderer.PinKeyMithrilMine]        = "automation_mithrilmine",
            [AutomationRenderer.PinKeyArcaneTower]        = "automation_arcanetower",
            [AutomationRenderer.PinKeyMonumentInvestment] = "automation_monument_investment",
            [AutomationRenderer.PinKeyRoad]               = "automation_road",
            [AutomationRenderer.PinKeyOutpost]            = "automation_outpost",
            [AutomationRenderer.PinKeyRoadUnderworld]     = "automation_road_underworld",
            [AutomationRenderer.PinKeyOutpostUnderworld]  = "automation_outpost_underworld",
            [AutomationRenderer.PinKeyProduction]         = "automation_production",
            [AutomationRenderer.PinKeyArtisan]            = "automation_artisan",
            [AutomationRenderer.PinKeyLibrary]            = "automation_library",
            [AutomationRenderer.PinKeyMarket]             = "automation_market",
            [AutomationRenderer.PinKeySeaport]            = "automation_seaport",
            [AutomationRenderer.PinKeyMilBuildings]       = "automation_military_buildings",
            [AutomationRenderer.PinKeyMilReinforce]       = "automation_military_reinforcement",
            [AutomationRenderer.PinKeyMilVendetta]        = "automation_military_vendetta",
            [AutomationRenderer.PinKeyRestrictSoldierProduction]           = "automation_restrict_soldier_production",
            [AutomationRenderer.PinKeyRestrictSoldierProductionUnderworld] = "automation_restrict_soldier_production_underworld",
            [AutomationRenderer.PinKeyRestrictSoldierProductionAbyss]      = "automation_restrict_soldier_production_abyss",
            [AutomationRenderer.PinKeyRestrictSoldierProductionPandemonium] = "automation_restrict_soldier_production_pandemonium",
        };

    /// <summary>
    /// Clé de description d'un automatisme épinglé — et non le générique
    /// "tooltip_pin_to_civ_panel", qui n'a de sens que sur la case à cocher de l'épinglage,
    /// pas une fois l'élément déjà épinglé.
    /// </summary>
    private static string GetAutomationPinDescKey(string key) =>
        AutomationPinLocalizationRoots.TryGetValue(key, out var root) ? $"{root}_desc" : "tooltip_pin_to_civ_panel";

    private bool IsTradeVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.TradeController.IsTradeAvailable(civ.Index); }
        catch { return false; }
    }

    private bool IsPrestigeVisible()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.PrestigeIsVisible(); }
        catch { return false; }
    }

    private bool IsPrestigeAvailable()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable(); }
        catch { return false; }
    }

    private bool HasPrestigeImperialPort()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.HasImperialPort(); }
        catch { return true; }
    }

    private int GetPrestigePoints()
    {
        try { return _gameControllerService.MainGameController.PrestigeController.CalculatePrestigePoints(); }
        catch { return 0; }
    }

    private bool IsWonderVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.WonderController.HasWondersUnlocked(civ); }
        catch { return false; }
    }

    private bool CanPlaceWonder()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.WonderController.CanPlaceWonder(civ); }
        catch { return false; }
    }

    private bool IsGreatLighthouseVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.GreatLighthouseController.HasGreatLighthouseUnlocked(civ); }
        catch { return false; }
    }

    private bool CanPlaceGreatLighthouse()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.GreatLighthouseController.CanPlaceGreatLighthouse(civ); }
        catch { return false; }
    }

    private bool CanPlaceObservatory()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.ObservatoryController.CanPlaceObservatory(civ); }
        catch { return false; }
    }

    private bool CanPlaceNecropolis()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.NecropolisController.CanPlaceNecropolis(civ); }
        catch { return false; }
    }

    private bool CanPlaceDeepestMine()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.DeepestMineController.CanPlaceDeepestMine(civ); }
        catch { return false; }
    }

    private bool CanPlaceSpire()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.CorruptionSpireController.CanPlaceCorruptionSpire(civ); }
        catch { return false; }
    }

    private bool IsRaidVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.MilitaryController.IsRaidUnlocked(civ); }
        catch { return false; }
    }

    private bool IsRaidActive()
    {
        try { return _gameControllerService.MainGameController.MilitaryController.IsRaidActive(); }
        catch { return false; }
    }

    private bool IsWarHeraldVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.MilitaryController.IsWarHeraldUnlocked(civ); }
        catch { return false; }
    }

    private static (float x, float y) HexToWorld(HexCoord hex)
    {
        float sqrt3 = MathF.Sqrt(3f);
        float x = GameConstants.HexSize * sqrt3 * (hex.Q + hex.R / 2f);
        float y = GameConstants.HexSize * -3f / 2f * hex.R;
        return (x, y);
    }

    private static (float x, float y) VertexToWorld(Vertex v)
    {
        var (x1, y1) = HexToWorld(v.Hex1);
        var (x2, y2) = HexToWorld(v.Hex2);
        var (x3, y3) = HexToWorld(v.Hex3);
        return ((x1 + x2 + x3) / 3f, (y1 + y2 + y3) / 3f);
    }

    private bool IsRelocationVisible()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return _gameControllerService.MainGameController.CityBuilderController.IsRelocationUnlocked(civ); }
        catch { return false; }
    }

    private bool CanAffordRelocation()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return false;
        try { return civ.CanPayResourceCost(CityBuilderController.RelocationCost()); }
        catch { return false; }
    }

    private static bool HasBuilt<T>(Civilization civ) where T : Building
        => civ.Cities.Any(c => c.Buildings.OfType<T>().Any(b => b.Level >= 1));

    private static bool? AreAllActiveNullable<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        if (list.Count == 0) return false;
        bool allOn = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        if (allOn) return true;
        bool anyOn = list.Any(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        return anyOn ? null : false;
    }

    private static void ToggleAll<T>(Civilization civ) where T : Building
    {
        var list = civ.Cities.SelectMany(c => c.Buildings.OfType<T>()).Where(b => b.Level >= 1).ToList();
        bool allActive = list.All(b => b.ActivationStatus == ActivationStatus.ACTIVE);
        var next = allActive ? ActivationStatus.INACTIVE : ActivationStatus.ACTIVE;
        foreach (var b in list) b.ActivationStatus = next;
    }

    // ── Pont vers l'hôte Avalonia ─────────────────────────────────────────────

    /// <summary>
    /// Instantané du panneau pour une vue portée par l'hôte. Reprend les règles de visibilité,
    /// de disponibilité, les libellés et les infobulles de <see cref="Render"/> — les mêmes
    /// prédicats, pas une réécriture — afin que les deux affichages ne divergent pas.
    ///
    /// Les infobulles sont ici du texte simple : la vue les rend en ToolTip Avalonia natif,
    /// contrairement à celle du panneau ville qui reste dessinée en Skia.
    /// </summary>
    public CivPanelSnapshot GetSnapshot()
    {
        var civ = _gameControllerService.PlayerCivilization;
        if (civ == null) return CivPanelSnapshot.Hidden;

        var worldState = _gameControllerService.CurrentWorldState;
        var pinned = _gameControllerService.CurrentGameState?.Settings.PinnedCivPanelKeys
                     ?? (IReadOnlySet<string>)new HashSet<string>();

        var iconActions = new List<CivActionSnapshot>();
        var actions     = new List<CivActionSnapshot>();

        // ── En-tête : boutons icône, dans l'ordre d'affichage de gauche à droite ──

        if (IsTradeVisible())
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyTrade, _localization.Get("trade_action"), true, false,
                IconName: null, Glyph: "💰",
                TooltipLines: [_localization.Get("trade_action"), _localization.Get("tooltip_trade")]));

        if (IsRaidVisible())
        {
            bool raidActive = IsRaidActive();
            var raidTooltip = raidActive
                ? new List<string>
                {
                    _localization.Get("raid_action_stop"),
                    _localization.Get("tooltip_raid_active"),
                    _localization.GetFormated("raid_upkeep_cost_current", worldState?.AutomationSettings.RaidCurrentUpkeep ?? 0),
                }
                : new List<string>
                {
                    _localization.Get("raid_action"),
                    _localization.Get("tooltip_raid"),
                    _localization.Get("raid_upkeep_cost"),
                };
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyRaid,
                _localization.Get(raidActive ? "raid_action_stop" : "raid_action"),
                IsEnabled: true, IsHighlighted: raidActive,
                IconName: "Resources.icons.military.attack.svg", Glyph: null,
                TooltipLines: raidTooltip));
        }

        if (IsWarHeraldVisible())
            iconActions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyWarHerald, _localization.Get("warherald_action_short"), true, false,
                IconName: "Resources.icons.military.defense.svg", Glyph: null,
                TooltipLines: [_localization.Get("warherald_action_short"), _localization.Get("tooltip_warherald")]));

        // ── Grille d'actions, dans l'ordre du rendu Skia ──

        if (IsPrestigeVisible())
        {
            bool available = IsPrestigeAvailable();
            int points = GetPrestigePoints();

            var tooltip = new List<string>();
            if (!available)
            {
                if (!HasPrestigeImperialPort())
                    tooltip.Add(_localization.Get("tooltip_prestige_no_imperial_port"));
                if (points < PrestigeController.PrestigeRequiredPoints)
                    tooltip.Add(_localization.GetFormated("tooltip_prestige_not_enough_points",
                        SkiaTextUtils.FormatNumber(points), SkiaTextUtils.FormatNumber(PrestigeController.PrestigeRequiredPoints)));
            }
            tooltip.Add(_localization.Get("tooltip_prestige_next_island"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyPrestige,
                $"{_localization.Get("prestige_action")} (+{SkiaTextUtils.FormatNumber(points)})",
                available, false, null, null, tooltip));
        }

        if (WonderVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyWonder, "wonder_action_short",
                WonderEnabled, "tooltip_wonder", "tooltip_wonder_surface_only"));

        if (GreatLighthouseVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyGreatLighthouse, "great_lighthouse_action_short",
                GreatLighthouseEnabled, "tooltip_great_lighthouse", "tooltip_great_lighthouse_surface_only"));

        if (ObservatoryVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyObservatory, "observatory_action_short",
                ObservatoryEnabled, "tooltip_observatory", "tooltip_observatory_surface_only"));

        if (NecropolisVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyNecropolis, "necropolis_action_short",
                NecropolisEnabled,
                NecropolisHarvestsBones ? "tooltip_necropolis_harvest" : "tooltip_necropolis",
                "tooltip_necropolis_abyss_only"));

        if (DeepestMineVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyDeepestMine, "deepest_mine_action_short",
                DeepestMineEnabled, "tooltip_deepest_mine", "tooltip_deepest_mine_surface_only"));

        if (SpireVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeySpire, "spire_action_short",
                SpireEnabled, "tooltip_spire", "tooltip_spire_underworld_only"));

        if (RelocationVisible)
            actions.Add(SimpleAction(CivPanelSnapshot.KeyRelocation, "relocation_action_short",
                RelocationEnabled, "tooltip_relocation", "tooltip_relocation_insufficient_resources"));

        if (WalkOfGodVisible)
        {
            int cost = Ascension.GetWalkOfGodCost();
            var tooltip = new List<string>
            {
                _localization.Get("tooltip_walkofgod"),
                _localization.GetFormated("tooltip_walkofgod_cost", cost),
            };
            if (!Ascension.CanUseWalkOfGod())
                tooltip.Add(_localization.Get("tooltip_walkofgod_insufficient_prestige"));
            if (Ascension.GetWalkOfGodTargetHexes().Count == 0)
                tooltip.Add(_localization.Get("tooltip_walkofgod_no_dominion"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyWalkOfGod,
                $"{_localization.Get("walkofgod_action_short")} ({cost})",
                WalkOfGodEnabled, false, null, null, tooltip));
        }

        if (PresenceOfGodVisible)
        {
            int cost = Ascension.GetPresenceOfGodCost();
            var tooltip = new List<string>
            {
                _localization.Get("tooltip_presenceofgod"),
                _localization.GetFormated("tooltip_presenceofgod_cost", cost),
            };
            if (!Ascension.CanUsePresenceOfGod())
                tooltip.Add(_localization.Get("tooltip_presenceofgod_insufficient_prestige"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyPresenceOfGod,
                $"{_localization.Get("presenceofgod_action_short")} ({cost})",
                PresenceOfGodEnabled, false, null, null, tooltip));
        }

        if (FistOfGodVisible)
        {
            int cost = Ascension.GetFistOfGodCost();
            var tooltip = new List<string>
            {
                _localization.GetFormated("tooltip_fistofgod", AscensionController.FistOfGodDamage),
                _localization.GetFormated("tooltip_fistofgod_cost", cost),
            };
            if (!Ascension.CanUseFistOfGod())
                tooltip.Add(_localization.Get("tooltip_fistofgod_insufficient_prestige"));

            actions.Add(new CivActionSnapshot(
                CivPanelSnapshot.KeyFistOfGod,
                $"{_localization.Get("fistofgod_action_short")} ({cost})",
                FistOfGodEnabled, false, null, null, tooltip));
        }

        // ── Bascules épinglées ──

        bool hasBarracks      = HasBuilt<Barracks>(civ);
        bool hasArsenal       = HasBuilt<Arsenal>(civ);
        bool hasLabs          = HasBuilt<Laboratory>(civ);
        bool hasSmelters      = HasBuilt<Smelter>(civ);
        bool hasWeaponSmiths  = HasBuilt<WeaponSmith>(civ);
        bool hasArmorSmiths   = HasBuilt<ArmorSmith>(civ);
        bool hasAlchimistHuts = HasBuilt<AlchimistHut>(civ);

        // Meme table que l'onglet Automatisation (AutomationRenderer.ComputeStructuralUnlocks) :
        // une bascule epinglee dont le deblocage a ete perdu (guilde retombee sous le niveau
        // requis apres une Ascension, par ex.) ne doit pas rester affichee ici.
        var structuralUnlocks = AutomationRenderer.ComputeStructuralUnlocks(civ);
        int freePerCitySoldierQuota = (int)civ.ModifierAggregator.ApplyModifiers(ECategory.SOLDIER_FOOD_FREE_PER_CITY, "", 0.0);

        var toggles = new List<CivToggleSnapshot>();
        foreach (var key in pinned)
        {
            if (!IsKeyShowable(key, worldState, hasBarracks, hasArsenal, hasLabs, hasSmelters,
                    hasWeaponSmiths, hasArmorSmiths, hasAlchimistHuts, structuralUnlocks, freePerCitySoldierQuota))
                continue;

            var (value, nameKey, tooltipKey) = ResolvePinnedToggle(key, civ, worldState);
            var category = AutomationRenderer.PinKeyCategories.GetValueOrDefault(key, AutomationCategory.Construction);
            toggles.Add(new CivToggleSnapshot(key, _localization.Get(nameKey), value, _localization.Get(tooltipKey), category));
        }

        // Regroupees par famille (construction, comportement, activation) plutot que dans l'ordre
        // d'epinglage : c'est ce classement que la vue s'appuie dessus pour styler chaque bascule,
        // et regrouper les bascules de meme style les rend plus faciles a parcourir d'un coup d'oeil.
        toggles = toggles.OrderBy(t => t.Category).ToList();

        // Même règle que Render : sans action ni bascule, le panneau n'a rien à montrer.
        if (iconActions.Count == 0 && actions.Count == 0 && toggles.Count == 0)
            return CivPanelSnapshot.Hidden;

        return new CivPanelSnapshot(
            IsVisible: true,
            IsCollapsed: Collapsed,
            ActionsTitle: _localization.Get("panel_civ_actions"),
            ControlsTitle: _localization.Get("panel_civ_controls"),
            IconActions: iconActions,
            Actions: actions,
            Toggles: toggles);
    }

    /// Action de la grille dont l'infobulle se réduit à une raison unique selon sa disponibilité.
    private CivActionSnapshot SimpleAction(string key, string labelKey, bool enabled,
        string enabledTooltipKey, string disabledTooltipKey) =>
        new(key, _localization.Get(labelKey), enabled, false, null, null,
            [_localization.Get(enabled ? enabledTooltipKey : disabledTooltipKey)]);

    /// <summary>Déclenche une action du panneau depuis une vue portée par l'hôte.</summary>
    public void ExecuteActionFromHost(string key)
    {
        switch (key)
        {
            case CivPanelSnapshot.KeyTrade:           DoTrade();           break;
            case CivPanelSnapshot.KeyPrestige:        DoPrestige();        break;
            case CivPanelSnapshot.KeyWonder:          DoWonder();          break;
            case CivPanelSnapshot.KeyGreatLighthouse: DoGreatLighthouse(); break;
            case CivPanelSnapshot.KeyObservatory:     DoObservatory();     break;
            case CivPanelSnapshot.KeyNecropolis:      DoNecropolis();      break;
            case CivPanelSnapshot.KeyDeepestMine:     DoDeepestMine();     break;
            case CivPanelSnapshot.KeySpire:           DoSpire();           break;
            case CivPanelSnapshot.KeyRaid:            DoRaid();            break;
            case CivPanelSnapshot.KeyWarHerald:       DoWarHerald();       break;
            case CivPanelSnapshot.KeyRelocation:      DoRelocation();      break;
            case CivPanelSnapshot.KeyWalkOfGod:       DoWalkOfGod();       break;
            case CivPanelSnapshot.KeyPresenceOfGod:   DoPresenceOfGod();   break;
            case CivPanelSnapshot.KeyFistOfGod:       DoFistOfGod();       break;
        }
    }

    /// <summary>Bascule un élément épinglé depuis une vue portée par l'hôte.</summary>
    public void ToggleFromHost(string key) =>
        HandlePinnedToggle(key, _gameControllerService.PlayerCivilization, _gameControllerService.CurrentWorldState);

    /// <summary>
    /// Replie/déplie le panneau depuis une vue portée par l'hôte. Le repli reste stocké ici :
    /// en disposition mobile, <c>OverlayRenderer</c> replie ce panneau quand un panneau latéral
    /// droit s'ouvre, et cette règle doit continuer de s'imposer à la vue.
    /// </summary>
    public void SetCollapsedFromHost(bool collapsed)
    {
        bool wasCollapsed = Collapsed;
        Collapsed = collapsed;
        if (wasCollapsed && !collapsed) OnExpanded?.Invoke();
    }

    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        base.Dispose();
    }
}
