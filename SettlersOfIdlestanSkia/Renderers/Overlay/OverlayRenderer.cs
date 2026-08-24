using System;
using System.Linq;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public sealed class OverlayRenderer : IGameRenderer
{
    private readonly InputHandlingService _inputService;
    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly PlayerResourcesOverlayRenderer _playerResourcesOverlayRenderer;
    private readonly SettingsMenu _settingsMenu;
    private readonly SettingsPopupRenderer _settingsPopupRenderer;
    private readonly SelectedCityPanelRenderer _selectedCityPanelRenderer;
    private readonly SelectedMonumentPanelRenderer _selectedMonumentPanelRenderer;
    private readonly TradePopupRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private readonly PrestigeMapRenderer _prestigeMapRenderer;
    private readonly PrestigeHistoryRenderer _prestigeHistoryRenderer;
    private readonly ResearchRenderer _researchRenderer;
    private readonly EventLogRenderer _eventLogRenderer;
    private readonly AutomationRenderer _automationRenderer;
    private readonly RitualsRenderer _ritualsRenderer;
    private readonly AscensionRenderer _ascensionRenderer;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly CameraService _cameraService;
    private readonly PlayerCivilizationPanelRenderer _playerCivPanel;
    private readonly SettlersOfIdlestanSkia.Renderers.Debug.HistoryTabRenderer? _historyRenderer;
    private readonly TabBarRenderer _tabBar;

    private readonly UILayoutService _uiLayout;
    private SKSize _canvasSize;
    private SKPoint _lastPointerPosition;
    private TargetSelectionService? _targetSelectionService;

    private bool _disposed;
    private bool _isVisible = true;
    private bool _suppressNextPress;

    public OverlayRenderer(
        InputHandlingService inputService,
        GameControllerService gameControllerService,
        LocalizationService localization,
        PlayerResourcesOverlayRenderer playerResourcesOverlayRenderer,
        SettingsMenu settingsMenu,
        SettingsPopupRenderer settingsPopupRenderer,
        SelectedCityPanelRenderer selectedCityPanelRenderer,
        SelectedMonumentPanelRenderer selectedMonumentPanelRenderer,
        TradePopupRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        PrestigeMapRenderer prestigeMapRenderer,
        PrestigeHistoryRenderer prestigeHistoryRenderer,
        ResearchRenderer researchRenderer,
        EventLogRenderer eventLogRenderer,
        AutomationRenderer automationRenderer,
        RitualsRenderer ritualsRenderer,
        AscensionRenderer ascensionRenderer,
        TooltipRenderer tooltipRenderer,
        CameraService cameraService,
        ResourceManager resourceManager,
        UILayoutService uiLayout,
        bool allowDebugMode = false,
        SettlersOfIdlestanSkia.Renderers.Debug.HistoryTabRenderer? historyRenderer = null)
    {
        _uiLayout                       = uiLayout;
        _inputService                   = inputService;
        _gameControllerService          = gameControllerService;
        _localization                   = localization;
        _playerResourcesOverlayRenderer = playerResourcesOverlayRenderer;
        _settingsMenu                   = settingsMenu;
        _settingsPopupRenderer          = settingsPopupRenderer;
        _selectedCityPanelRenderer      = selectedCityPanelRenderer;
        _selectedCityPanelRenderer.CenterCameraOnMapPosition = CenterCameraOnMapPosition;
        _selectedMonumentPanelRenderer    = selectedMonumentPanelRenderer;
        _tradeRenderer                  = tradeRenderer;
        _prestigeRenderer               = prestigeRenderer;
        _prestigeMapRenderer            = prestigeMapRenderer;
        _prestigeHistoryRenderer        = prestigeHistoryRenderer;
        _researchRenderer               = researchRenderer;
        _eventLogRenderer               = eventLogRenderer;
        _automationRenderer             = automationRenderer;
        _ritualsRenderer                = ritualsRenderer;
        _ascensionRenderer              = ascensionRenderer;
        _tooltipRenderer                = tooltipRenderer;
        _cameraService                  = cameraService;
        _historyRenderer                = historyRenderer;

        _tabBar          = new TabBarRenderer(localization, gameControllerService, uiLayout, allowDebugMode);

        _playerCivPanel = new PlayerCivilizationPanelRenderer(
            gameControllerService,
            localization,
            closeAll: CloseAll,
            tradeRenderer,
            prestigeRenderer,
            targetSelectionService: null,
            tooltipRenderer,
            resourceManager,
            centerCameraOnMapPosition: CenterCameraOnMapPosition);
        _playerCivPanel.OnExpanded = () => { if (_uiLayout.TabsAtBottom) DeselectCityAndMonument(); };

        _inputService.PointerPressed  += HandlePointerPressed;
        _inputService.PointerMoved    += HandlePointerMoved;
        _inputService.PointerReleased += HandlePointerReleased;
        _inputService.ZoomChanged     += HandleZoomChanged;
        _inputService.PinchChanged    += HandlePinchChanged;
        _inputService.KeyPressed      += HandleKeyInput;
        _inputService.KeyReleased     += HandleKeyRelease;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _uiLayout.UpdateCanvasSize(canvasSize);

        _selectedCityPanelRenderer.Initialize(canvasSize);
        _selectedMonumentPanelRenderer.Initialize(canvasSize);
        _tradeRenderer.Initialize(canvasSize);
        _prestigeRenderer.Initialize(canvasSize);
        _settingsPopupRenderer.Initialize(canvasSize);
        _prestigeMapRenderer.Initialize(canvasSize);
        _prestigeHistoryRenderer.Initialize(canvasSize);
        _researchRenderer.Initialize(canvasSize);
        _eventLogRenderer.Initialize(canvasSize);
        _automationRenderer.Initialize(canvasSize);
        _ritualsRenderer.Initialize(canvasSize);
        _ascensionRenderer.Initialize(canvasSize);
        _historyRenderer?.Initialize(canvasSize);
        _playerCivPanel.Initialize(canvasSize);
        _tabBar.Initialize(canvasSize);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_isVisible) return;

        if (context.GameState is MainGameState mgs)
            _uiLayout.SetMenuPosition(mgs.Settings.ForceMenuPosition);

        _tabBar.Update(context);

        int activeTab      = _tabBar.ActiveTab;
        bool panelsEnabled = IsMapViewTab(activeTab)
                          && !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
        _selectedCityPanelRenderer.IsInputEnabled  = panelsEnabled;
        _selectedMonumentPanelRenderer.IsInputEnabled = panelsEnabled;
        _researchRenderer.IsActive = activeTab == TabBarRenderer.TabResearch;

        float panelTop = _uiLayout.PanelTopY;
        _playerCivPanel.TopOverride              = panelTop;
        _selectedMonumentPanelRenderer.TopOverride = panelTop;
        _selectedCityPanelRenderer.TopOverride   = panelTop;

        if (_uiLayout.TabsAtBottom)
        {
            bool rightPanelOpen = _gameControllerService.CityBuildingService?.SelectedCity != null
                               || _selectedMonumentPanelRenderer.HasSelection;
            if (rightPanelOpen && !_playerCivPanel.IsCollapsed)
                _playerCivPanel.Collapse();
        }

        // Les onglets encore dessinés en Skia sont ceux dont le contenu est une vue canvas :
        // graphe de recherche, carte de prestige, ascension, historique. Tous les autres sont
        // des contrôles Avalonia posés au-dessus du canevas. L'onglet Île y ajoute un cas
        // conditionnel : tant qu'une Ascension demandée attend son choix de race (voir
        // AscensionController.IsAscensionPending), la page de choix remplace la carte.
        switch (activeTab)
        {
            case TabBarRenderer.TabIsland when IsAscensionPending:
                _ascensionRenderer.RenderPendingRaceChoicePage(canvas, context);
                break;
            case TabBarRenderer.TabResearch:
                _researchRenderer.Render(canvas, context);
                break;
            case TabBarRenderer.TabPrestige:
                _prestigeMapRenderer.RenderPrestigeMap(canvas, context);
                break;
            case TabBarRenderer.TabAscension:
                _ascensionRenderer.RenderAscensionPage(canvas, context);
                break;
            case TabBarRenderer.TabHistory:
                _historyRenderer?.RenderHistory(canvas, context);
                break;
        }
    }

    /// <summary>Vrai entre une demande d'Ascension et le choix de race qui la conclut (voir
    /// AscensionController.IsAscensionPending) : l'onglet Île affiche alors le choix de race plutôt
    /// que la carte (voir RenderPendingRaceChoicePage), qui ne compte donc plus comme un onglet
    /// carte (IsMapViewTab).</summary>
    private bool IsAscensionPending => _gameControllerService.MainGameController.AscensionController.IsAscensionPending;

    /// <summary>
    /// Deuxième passe, dessinée par l'hôte au-dessus des contrôles de l'overlay : les infobulles.
    ///
    /// Elles ne peuvent pas rester dans <see cref="Render"/>, qui remplit le canevas de la carte —
    /// donc le tout premier enfant de l'arbre visuel. Une infobulle qui déborde sur un panneau
    /// Avalonia (celui de la ville au premier chef, dont chaque ligne en produit une) passerait
    /// derrière lui.
    /// </summary>
    public void RenderTooltipLayer(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_isVisible) return;

        // Les onglets plein écran dessinent leurs propres infobulles pendant leur rendu : seul
        // le panneau ville, porté par l'hôte, a encore besoin de cette passe.
        if (IsMapViewTab(_tabBar.ActiveTab))
            _selectedCityPanelRenderer.RenderHostTooltipOnly(canvas, context);
    }

    /// <summary>
    /// Infobulle d'une pastille de la barre de ressources, demandée au survol par la vue
    /// Avalonia qui porte désormais cette barre.
    ///
    /// Calculée à la demande plutôt que placée dans <see cref="GetResourceBarSnapshot"/> :
    /// additionner les sources de production et de consommation coûte cher, et l'instantané
    /// est reconstruit à chaque rafraîchissement pour toutes les ressources alors qu'une
    /// seule pastille est survolée à la fois.
    /// </summary>
    public string? GetResourceTooltip(SettlersOfIdlestan.Model.IslandMap.Resource hoveredResource)
    {
        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return null;

        string resourceName = _localization.Get($"resource_{hoveredResource.ToString().ToLower()}");
        var controller = _gameControllerService.MainGameController;
        int civIndex = worldState.PlayerCivilization.Index;

        System.Collections.Generic.List<(string SourceKey, double Rate)>? gains;
        System.Collections.Generic.List<(string SourceKey, double Rate)>? losses;

        // Le Cristal a des sources/pertes propres à la Magie (rituels, Huttes d'Alchimie, Monuments) : on
        // délègue à MagicController.GetCrystalGainsAndLosses(), seule source de vérité pour ce total —
        // c'est la même méthode qu'utilise la page Rituels, ce qui garantit des chiffres identiques.
        if (hoveredResource == SettlersOfIdlestan.Model.IslandMap.Resource.Crystal)
        {
            (gains, losses) = controller.MagicController.GetCrystalGainsAndLosses();
        }
        else
        {
            var gainsBySource = controller.HarvestController.GetProductionRatesBySource(civIndex);
            var lossesBySource = controller.HarvestController.GetConsumptionRatesBySource(civIndex);

            foreach (var (resource, monumentLosses) in SettlersOfIdlestan.Controller.Expand.MonumentInvestment.GetInvestmentRatesBySource(worldState, worldState.PlayerCivilization))
            {
                if (!lossesBySource.TryGetValue(resource, out var list))
                    lossesBySource[resource] = list = new System.Collections.Generic.List<(string SourceKey, double Rate)>();
                list.AddRange(monumentLosses);
            }

            // Le minerai consommé par la production de soldats (Casernes) est calculé par le MilitaryController
            // (source de vérité pour tout ce qui touche aux Casernes) plutôt que par le HarvestController. On
            // affiche toujours cette ligne dès qu'une Caserne existe, même à 0/s (ville au plafond de soldats) —
            // sinon la ligne apparaît/disparaît sans arrêt dans l'infobulle au fil des cycles de production.
            if (hoveredResource == SettlersOfIdlestan.Model.IslandMap.Resource.Ore
                && controller.MilitaryController.HasAnySoldierProductionBuilding(civIndex))
            {
                double soldierOreRate = controller.MilitaryController.GetSoldierProductionOreRate(civIndex);
                if (!lossesBySource.TryGetValue(SettlersOfIdlestan.Model.IslandMap.Resource.Ore, out var oreLosses))
                    lossesBySource[SettlersOfIdlestan.Model.IslandMap.Resource.Ore] = oreLosses = new System.Collections.Generic.List<(string SourceKey, double Rate)>();
                oreLosses.Add((SettlersOfIdlestan.Controller.Military.MilitaryController.SoldierProductionOreSourceKey, soldierOreRate));
            }

            // Même logique pour l'Acier consommé par la production de soldats des Arsenaux actifs.
            if (hoveredResource == SettlersOfIdlestan.Model.IslandMap.Resource.Steel
                && controller.MilitaryController.HasAnyArsenalProductionBuilding(civIndex))
            {
                double arsenalSteelRate = controller.MilitaryController.GetArsenalProductionSteelRate(civIndex);
                if (!lossesBySource.TryGetValue(SettlersOfIdlestan.Model.IslandMap.Resource.Steel, out var steelLosses))
                    lossesBySource[SettlersOfIdlestan.Model.IslandMap.Resource.Steel] = steelLosses = new System.Collections.Generic.List<(string SourceKey, double Rate)>();
                steelLosses.Add((SettlersOfIdlestan.Controller.Military.MilitaryController.ArsenalProductionSteelSourceKey, arsenalSteelRate));
            }

            // L'or consommé par les Laboratoires actifs est calculé par le ResearchController (source de
            // vérité pour tout ce qui touche aux Laboratoires) plutôt que par le HarvestController, même
            // logique que pour le minerai/l'acier des Casernes/Arsenaux ci-dessus.
            if (hoveredResource == SettlersOfIdlestan.Model.IslandMap.Resource.Gold
                && controller.ResearchController.HasAnyActiveLaboratory(civIndex))
            {
                double laboratoryGoldRate = controller.ResearchController.GetLaboratoryGoldConsumptionRate(civIndex);
                if (!lossesBySource.TryGetValue(SettlersOfIdlestan.Model.IslandMap.Resource.Gold, out var goldLosses))
                    lossesBySource[SettlersOfIdlestan.Model.IslandMap.Resource.Gold] = goldLosses = new System.Collections.Generic.List<(string SourceKey, double Rate)>();
                goldLosses.Add((SettlersOfIdlestan.Controller.Expand.ResearchController.LaboratoryGoldConsumptionSourceKey, laboratoryGoldRate));
            }

            gainsBySource.TryGetValue(hoveredResource, out gains);
            lossesBySource.TryGetValue(hoveredResource, out losses);
        }
        gains ??= new System.Collections.Generic.List<(string SourceKey, double Rate)>();
        losses ??= new System.Collections.Generic.List<(string SourceKey, double Rate)>();

        if (gains.Count == 0 && losses.Count == 0) return resourceName;

        double totalRate = gains.Sum(g => g.Rate) - losses.Sum(l => l.Rate);
        var lines = new System.Collections.Generic.List<string>
        {
            $"{resourceName} {_localization.GetFormated("tooltip_resource_total", totalRate.ToString("F2"))}"
        };
        foreach (var (sourceKey, rate) in gains.OrderByDescending(g => g.Rate))
            lines.Add($"{_localization.Get(sourceKey)} : +{rate:F2}/s");
        foreach (var (sourceKey, rate) in losses.OrderByDescending(l => l.Rate))
            lines.Add($"{_localization.Get(sourceKey)} : -{rate:F2}/s");
        return string.Join('\n', lines);
    }

    public void ConnectTargetSelectionService(TargetSelectionService targetSelectionService)
    {
        _targetSelectionService = targetSelectionService;
        _playerCivPanel.ConnectTargetSelectionService(targetSelectionService);
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen
                                 || _settingsMenu.IsOpen  || _settingsPopupRenderer.IsOpen;

    /// <summary>Ouvre/ferme le menu paramètres depuis l'icône d'engrenage de l'hôte.</summary>
    public void ToggleSettingsMenuFromHost() => _settingsMenu.HandleGearClick();

    /// <summary>Instantané de la barre d'onglets pour une vue portée par l'hôte.</summary>
    public TabBarSnapshot GetTabBarSnapshot() => _tabBar.GetSnapshot();

    /// <summary>
    /// Les panneaux latéraux n'existent que sur les onglets carte, seul cas où <see cref="Render"/>
    /// laisse le canevas à la carte. Leurs instantanés
    /// doivent donc porter la même condition, sinon ils resteraient affichés par-dessus un
    /// onglet plein écran, qui ne les recouvre pas — l'overlay de l'hôte est au-dessus du canevas.
    /// </summary>
    private bool SidePanelsVisible => IsMapViewTab(_tabBar.ActiveTab);

    /// <summary>Instantané du panneau ville pour une vue portée par l'hôte.</summary>
    public CityPanelSnapshot GetCityPanelSnapshot() =>
        SidePanelsVisible ? _selectedCityPanelRenderer.GetSnapshot() : CityPanelSnapshot.Hidden;

    public void CloseCityPanelFromHost() => _selectedCityPanelRenderer.Close();
    public void SetCityShowUniqueFromHost(bool showUnique) => _selectedCityPanelRenderer.SetShowUniqueFromHost(showUnique);
    public void ToggleCityBuildingActivationFromHost(string key) => _selectedCityPanelRenderer.ToggleActivationFromHost(key);
    public void ExecuteCityBuildingActionFromHost(string key) => _selectedCityPanelRenderer.ExecuteActionFromHost(key);
    public void GoToOtherCityFromHost(string key) => _selectedCityPanelRenderer.GoToOtherCityFromHost(key);
    public void SetHoveredCityBuildingFromHost(string? key, float x, float y) => _selectedCityPanelRenderer.SetHoveredBuildingFromHost(key, x, y);
    

    /// <summary>
    /// Instantané de l'onglet Journal pour une vue portée par l'hôte. La visibilité est décidée
    /// ici : c'est cet objet qui détient l'onglet courant.
    /// </summary>
    public EventLogSnapshot GetEventLogSnapshot() =>
        _eventLogRenderer.GetSnapshot(_tabBar.ActiveTab == TabBarRenderer.TabEvents);

    /// <summary>Instantané de l'onglet Stats pour une vue portée par l'hôte.</summary>
    public StatsSnapshot GetStatsSnapshot() =>
        _prestigeHistoryRenderer.GetSnapshot(_tabBar.ActiveTab == TabBarRenderer.TabStats);

    public void SetStatsSubTabFromHost(string key) => _prestigeHistoryRenderer.SetSubTabFromHost(key);

    /// <summary>Instantané de l'onglet Rituels pour une vue portée par l'hôte.</summary>
    public RitualsSnapshot GetRitualsSnapshot() =>
        _ritualsRenderer.GetSnapshot(_tabBar.ActiveTab == TabBarRenderer.TabRituals);

    public void ToggleRitualFromHost(string key) => _ritualsRenderer.ToggleRitualFromHost(key);
    public void ChangeRitualPowerFromHost(string key, bool increase) => _ritualsRenderer.ChangeRitualPowerFromHost(key, increase);
    public void CastSpellFromHost(string key) => _ritualsRenderer.CastSpellFromHost(key);

    /// <summary>Instantané de l'onglet Automatisation pour une vue portée par l'hôte.</summary>
    public AutomationSnapshot GetAutomationSnapshot() =>
        _automationRenderer.GetSnapshot(_tabBar.ActiveTab == TabBarRenderer.TabAutomation);

    public void ToggleAutomationFromHost(string key) => _automationRenderer.ToggleByKey(key);
    public void ToggleAutomationPinFromHost(string key) => _automationRenderer.TogglePinFromHost(key);
    public void ToggleAutomationsGloballyFromHost() => _automationRenderer.ToggleGlobalFromHost();

    /// <summary>Instantané du menu de l'engrenage pour une vue portée par l'hôte.</summary>
    public SettingsMenuSnapshot GetSettingsMenuSnapshot() => _settingsMenu.GetSnapshot();

    public void InvokeSettingsMenuItemFromHost(string key) => _settingsMenu.InvokeItemFromHost(key);
    public void CloseSettingsMenuFromHost() => _settingsMenu.Close();

    /// <summary>Instantané du popup de commerce pour une vue portée par l'hôte.</summary>
    public TradePopupSnapshot GetTradePopupSnapshot() => _tradeRenderer.GetSnapshot();

    public void TradeSellFromHost(string key) => _tradeRenderer.SellFromHost(key);
    public void TradeBuyFromHost(string key) => _tradeRenderer.BuyFromHost(key);
    public void TradeSetMultiplierFromHost(int m) => _tradeRenderer.SetMultiplierFromHost(m);
    public void TradeSetHistoryTabFromHost(bool h) => _tradeRenderer.SetHistoryTabFromHost(h);
    public void TradeSetAutoTabFromHost() => _tradeRenderer.SetAutoTabFromHost();
    public void TradeSetAutoSellThresholdFromHost(string key, int percent) => _tradeRenderer.SetAutoSellThresholdFromHost(key, percent);
    public void TradeSetAutoGoldKeepPercentFromHost(int percent) => _tradeRenderer.SetAutoGoldKeepPercentFromHost(percent);
    public void CloseTradePopupFromHost() => _tradeRenderer.Close();

    /// <summary>Instantané du popup de prestige pour une vue portée par l'hôte.</summary>
    public PrestigePopupSnapshot GetPrestigePopupSnapshot() => _prestigeRenderer.GetSnapshot();

    /// <summary>
    /// Modale portée par l'overlay plutôt que par GameScreen : les confirmations du popup de
    /// prestige (montée de corruption avant la première Ascension, puis perte d'essences), et la
    /// confirmation d'Ascension (hors choix de race). Même forme, donc même vue.
    /// </summary>
    public ModalPopupSnapshot GetOverlayModalSnapshot()
    {
        var prestigeModal = _prestigeRenderer.GetOverlayModalSnapshot();
        if (prestigeModal.IsOpen) return prestigeModal;
        return _ascensionRenderer.GetOverlayModalSnapshot();
    }

    public void InvokeOverlayModalButtonFromHost(string popupId, string key)
    {
        _prestigeRenderer.InvokeOverlayModalButtonFromHost(popupId, key);
        _ascensionRenderer.InvokeOverlayModalButtonFromHost(popupId, key);
    }
    public void InvokePrestigeActionFromHost(string key) => _prestigeRenderer.InvokeActionFromHost(key);
    public void PrestigeSkipWonderTimeFromHost() => _prestigeRenderer.SkipWonderTimeFromHost();
    public void PrestigeChangeTierFromHost(bool increase) => _prestigeRenderer.ChangeTierChoiceFromHost(increase);
    public void ClosePrestigePopupFromHost() => _prestigeRenderer.Close();

    /// <summary>Instantané du popup de réglages pour une vue portée par l'hôte.</summary>
    public SettingsPopupSnapshot GetSettingsPopupSnapshot() => _settingsPopupRenderer.GetSnapshot();

    public void ToggleSettingFromHost(string k) => _settingsPopupRenderer.ToggleSettingFromHost(k);
    public void SetSettingChoiceFromHost(string k, string c) => _settingsPopupRenderer.SetSettingChoiceFromHost(k, c);
    public void SetSettingSliderFromHost(string k, double v) => _settingsPopupRenderer.SetSettingSliderFromHost(k, v);
    public void SetSettingTextFromHost(string k, string v) => _settingsPopupRenderer.SetSettingTextFromHost(k, v);
    public void CloseSettingsPopupFromHost() => _settingsPopupRenderer.Close();

    /// <summary>Instantané du panneau civilisation pour une vue portée par l'hôte.</summary>
    public CivPanelSnapshot GetCivPanelSnapshot() =>
        SidePanelsVisible ? _playerCivPanel.GetSnapshot() : CivPanelSnapshot.Hidden;

    public void ExecuteCivActionFromHost(string key) => _playerCivPanel.ExecuteActionFromHost(key);
    public void ToggleCivPinnedFromHost(string key) => _playerCivPanel.ToggleFromHost(key);
    public void SetCivPanelCollapsedFromHost(bool collapsed) => _playerCivPanel.SetCollapsedFromHost(collapsed);

    /// <summary>Instantané du panneau monument pour une vue portée par l'hôte.</summary>
    public MonumentPanelSnapshot GetMonumentPanelSnapshot() =>
        SidePanelsVisible ? _selectedMonumentPanelRenderer.GetSnapshot() : MonumentPanelSnapshot.Hidden;

    public void CloseMonumentPanelFromHost() => _selectedMonumentPanelRenderer.CloseFromHost();
    public void ToggleMonumentInvestmentFromHost(string rowKey) =>
        _selectedMonumentPanelRenderer.ToggleInvestmentFromHost(rowKey);
    public void EvolveMonumentFromHost() => _selectedMonumentPanelRenderer.EvolveFromHost();
    public void SkipWonderFromHost() => _selectedMonumentPanelRenderer.SkipWonderFromHost();
    public void DestroyMonumentFromHost() => _selectedMonumentPanelRenderer.DestroyFromHost();

    /// <summary>Instantané de la barre de ressources pour une vue portée par l'hôte.</summary>
    public ResourceBarSnapshot GetResourceBarSnapshot() =>
        _playerResourcesOverlayRenderer.GetSnapshot(
            _gameControllerService.PlayerCivilization,
            _gameControllerService.CurrentGameState?.PrestigeState);

    /// <summary>
    /// Sélectionne un onglet depuis l'hôte. Applique aussi le changement de couche
    /// (île/inframonde/abysse), sans quoi cliquer sur ces onglets n'afficherait pas la bonne carte.
    /// </summary>
    public void SetActiveTabFromHost(int tabId)
    {
        _tabBar.SetActiveTab(tabId);
        ApplyLayerForActiveTab();
    }

    /// <summary>
    /// Ce qui reste de l'ancien arbitrage manuel des clics. Il énumérait autrefois chaque
    /// composant de l'overlay Skia, une liste maintenue à la main dont l'oubli d'un élément
    /// laissait passer les clics jusqu'à la carte. L'arbre visuel d'Avalonia s'en charge
    /// désormais : un clic sur un panneau n'atteint tout simplement pas le contrôle carte.
    ///
    /// Ne subsistent que les deux conditions qu'Avalonia ne couvre pas, la molette pouvant
    /// arriver sur le canevas alors que le jeu n'est pas censé y répondre.
    /// </summary>
    public bool IsPointBlockedByUI(SKPoint point) => IsAnyOverlayOpen || !IsIslandTabActive;

    public bool IsIslandTabActive => IsMapViewTab(_tabBar.ActiveTab);

    /// True for the tabs that show the hex map (Island / Underworld / Abyss / Pandemonium) rather than a full-screen panel.
    /// L'onglet Île n'y compte plus tant qu'une Ascension demandée attend son choix de race (voir
    /// IsAscensionPending) : la carte cède alors la place à RenderPendingRaceChoicePage.
    private bool IsMapViewTab(int tabId) =>
        tabId is TabBarRenderer.TabUnderworld or TabBarRenderer.TabAbyss or TabBarRenderer.TabPandemonium
        || (tabId == TabBarRenderer.TabIsland && !IsAscensionPending);

    /// Switches <see cref="WorldState.CurrentViewedLayer"/> to match a click on Island/Underworld/Abyss/Pandemonium.
    private void ApplyLayerForActiveTab()
    {
        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return;

        int? targetLayer = _tabBar.ActiveTab switch
        {
            TabBarRenderer.TabIsland     => IslandMap.SurfaceLayer,
            TabBarRenderer.TabUnderworld => LayerState.UnderworldZ,
            TabBarRenderer.TabAbyss      => LayerState.AbyssZ,
            TabBarRenderer.TabPandemonium => LayerState.PandemoniumZ,
            _ => null,
        };
        if (targetLayer == null || worldState.CurrentViewedLayer == targetLayer.Value) return;

        worldState.CurrentViewedLayer = targetLayer.Value;
        if (targetLayer.Value == LayerState.UnderworldZ) worldState.HasVisitedUnderworld = true;
        DeselectCityAndMonument();
    }

    /// Switches to the tab matching layer <paramref name="z"/> and centers the camera on a world position within it.
    private void CenterCameraOnMapPosition(int z, float worldX, float worldY)
    {
        int? tab = z switch
        {
            IslandMap.SurfaceLayer => TabBarRenderer.TabIsland,
            LayerState.UnderworldZ => TabBarRenderer.TabUnderworld,
            LayerState.AbyssZ      => TabBarRenderer.TabAbyss,
            LayerState.PandemoniumZ => TabBarRenderer.TabPandemonium,
            _ => null,
        };
        if (tab != null) _tabBar.SetActiveTab(tab.Value);
        ApplyLayerForActiveTab();
        _cameraService.CenterOn(worldX, worldY);
    }

    // ── Input ─────────────────────────────────────────────────────────────────
    // Ne reste ici que ce qui est encore dessiné en Skia : les onglets dont le contenu est une
    // vue canvas. Tout le reste de l'overlay est fait de contrôles Avalonia, qui reçoivent
    // leurs propres événements sans passer par ce dispatch.

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)  _prestigeMapRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabAscension) _ascensionRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabIsland && IsAscensionPending) _ascensionRenderer.HandlePendingRaceChoicePointerMoved(e.Position);

        _lastPointerPosition = e.Position;
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;
        if (_suppressNextPress) { _suppressNextPress = false; return; }
        if (e.Button != PointerButton.Left) return;

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)  _prestigeMapRenderer.HandlePointerPressed(e.Position);
        if (activeTab == TabBarRenderer.TabAscension) _ascensionRenderer.HandlePointerPressed(e.Position);
        if (activeTab == TabBarRenderer.TabIsland && IsAscensionPending) _ascensionRenderer.HandlePendingRaceChoicePointerPressed(e.Position);
    }

    private void DeselectCityAndMonument()
    {
        _selectedCityPanelRenderer.Close();
        _selectedMonumentPanelRenderer.Close();
    }

    public void CloseAll()
    {
        _settingsMenu.Close();
        _settingsPopupRenderer.Close();
        _tradeRenderer.Close();
        _prestigeRenderer.Close();
        DeselectCityAndMonument();
        _selectedCityPanelRenderer.IsInputEnabled  = false;
        _selectedMonumentPanelRenderer.IsInputEnabled = false;
    }

    public void Hide()
    {
        CloseAll();
        _isVisible = false;
    }

    public void Show(bool suppressNextPress = false)
    {
        _isVisible = true;
        _selectedCityPanelRenderer.IsInputEnabled  = true;
        _selectedMonumentPanelRenderer.IsInputEnabled = true;
        if (suppressNextPress) _suppressNextPress = true;
    }

    /// <summary>
    /// S'assure qu'un onglet carte (Île/Inframonde/Abysse/Pandemonium) est actif avant d'entrer en
    /// mode ciblage, sans quoi le clic sur la cible serait bloqué par <see cref="IsPointBlockedByUI"/>.
    /// Rejoint la couche actuellement affichée plutôt que de forcer la Surface : sinon l'onglet mis
    /// en évidence se désynchronisait de la couche réellement affichée — le joueur restait
    /// visuellement dans l'Inframonde avec le bouton "Surface" en surbrillance.
    /// </summary>
    public void EnsureMapTabActiveForTargetSelection()
    {
        if (IsIslandTabActive) return;

        int layer = _gameControllerService.CurrentWorldState?.CurrentViewedLayer ?? IslandMap.SurfaceLayer;
        int tab = layer switch
        {
            LayerState.UnderworldZ  => TabBarRenderer.TabUnderworld,
            LayerState.AbyssZ       => TabBarRenderer.TabAbyss,
            LayerState.PandemoniumZ => TabBarRenderer.TabPandemonium,
            _                       => TabBarRenderer.TabIsland,
        };
        _tabBar.SetActiveTab(tab);
        ApplyLayerForActiveTab();
    }

    private void HandlePointerReleased(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        // Un bouton Unknown signale un relâchement synthétique — le recognizer de pincement vient
        // de capturer les pointeurs. Il solde le glissement en cours mais ne vaut pas clic.
        bool isClick = e.Button == PointerButton.Left;

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)  _prestigeMapRenderer.HandlePointerReleased(e.Position, isClick);
        if (activeTab == TabBarRenderer.TabAscension) _ascensionRenderer.HandlePointerReleased(e.Position, isClick);
        if (activeTab == TabBarRenderer.TabIsland && IsAscensionPending) _ascensionRenderer.HandlePendingRaceChoicePointerReleased();
    }

    private void HandleZoomChanged(object? sender, ZoomEventArgs e)
    {
        if (!_isVisible) return;

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)  _prestigeMapRenderer.HandleZoom(e);
        if (activeTab == TabBarRenderer.TabAscension) _ascensionRenderer.HandleZoom(e);
        if (activeTab == TabBarRenderer.TabIsland && IsAscensionPending) _ascensionRenderer.HandlePendingRaceChoiceZoom(e);
    }

    /// Les deux onglets concernés sont des cartes hexagonales : chacune décide elle-même si le
    /// geste la vise (l'Ascension ignore ce qui tombe sur ses barres de boutons).
    private void HandlePinchChanged(object? sender, PinchEventArgs e)
    {
        if (!_isVisible) return;

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)  _prestigeMapRenderer.HandlePinch(e);
        if (activeTab == TabBarRenderer.TabAscension) _ascensionRenderer.HandlePinch(e);
    }

    private void HandleKeyInput(object? sender, KeyEventArgs e)
    {
        if (!_isVisible) return;

        // Popup de commerce ouvert : Ctrl/Maj y imposent un multiplicateur temporaire, et les
        // raccourcis d'onglet n'ont pas cours tant qu'il bloque l'ecran.
        if (_tradeRenderer.IsOpen)
        {
            _tradeRenderer.HandleKeyDown(e.Key);
            return;
        }

        _tabBar.HandleKeyInput(e.Key);
    }

    private void HandleKeyRelease(object? sender, KeyEventArgs e)
    {
        if (!_isVisible) return;
        if (_tradeRenderer.IsOpen) _tradeRenderer.HandleKeyUp(e.Key);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _inputService.PointerPressed  -= HandlePointerPressed;
        _inputService.PointerMoved    -= HandlePointerMoved;
        _inputService.PointerReleased -= HandlePointerReleased;
        _inputService.ZoomChanged     -= HandleZoomChanged;
        _inputService.PinchChanged    -= HandlePinchChanged;
        _inputService.KeyPressed      -= HandleKeyInput;
        _inputService.KeyReleased     -= HandleKeyRelease;

        _selectedCityPanelRenderer.Dispose();
        _selectedMonumentPanelRenderer.Dispose();
        _tradeRenderer.Dispose();
        _prestigeRenderer.Dispose();
        _prestigeMapRenderer.Dispose();
        _prestigeHistoryRenderer.Dispose();
        _researchRenderer.Dispose();
        _eventLogRenderer.Dispose();
        _automationRenderer.Dispose();
        _ritualsRenderer.Dispose();
        _ascensionRenderer.Dispose();
        _historyRenderer?.Dispose();
        _playerCivPanel.Dispose();
        _tabBar.Dispose();

        _disposed = true;
    }
}
