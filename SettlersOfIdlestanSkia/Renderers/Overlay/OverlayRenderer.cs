using System;
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
    private readonly TimeControlRenderer _timeControlRenderer;
    private readonly ResearchRenderer _researchRenderer;
    private readonly EventLogRenderer _eventLogRenderer;
    private readonly AutomationRenderer _automationRenderer;
    private readonly RitualsRenderer _ritualsRenderer;
    private readonly AscensionRenderer _ascensionRenderer;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly PlayerCivilizationPanelRenderer _playerCivPanel;
    private readonly SettlersOfIdlestanSkia.Renderers.Debug.HistoryTabRenderer? _historyRenderer;
    private readonly TabBarRenderer _tabBar;
    private readonly ZoomControlRenderer _zoomControl;

    private readonly UILayoutService _uiLayout;
    private SKSize _canvasSize;
    private SKPoint _lastPointerPosition;
    private TargetSelectionService? _targetSelectionService;

    // Ligne dédiée (time controls + gear) quand ils ne sont pas inline avec la barre de ressources
    private SKRect _wrappedGearRect;
    private readonly SKPaint _secondRowBgPaint     = new() { Color = new SKColor(0, 0, 0, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _secondRowBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };

    // Horizontal resource bar drag (mobile)
    private bool _isDraggingResources;
    private float _resourceDragLastX;

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
        TimeControlRenderer timeControlRenderer,
        ResearchRenderer researchRenderer,
        EventLogRenderer eventLogRenderer,
        AutomationRenderer automationRenderer,
        RitualsRenderer ritualsRenderer,
        AscensionRenderer ascensionRenderer,
        TooltipRenderer tooltipRenderer,
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
        _selectedMonumentPanelRenderer    = selectedMonumentPanelRenderer;
        _tradeRenderer                  = tradeRenderer;
        _prestigeRenderer               = prestigeRenderer;
        _prestigeMapRenderer            = prestigeMapRenderer;
        _prestigeHistoryRenderer        = prestigeHistoryRenderer;
        _timeControlRenderer            = timeControlRenderer;
        _researchRenderer               = researchRenderer;
        _eventLogRenderer               = eventLogRenderer;
        _automationRenderer             = automationRenderer;
        _ritualsRenderer                = ritualsRenderer;
        _ascensionRenderer              = ascensionRenderer;
        _tooltipRenderer                = tooltipRenderer;
        _historyRenderer                = historyRenderer;

        _tabBar          = new TabBarRenderer(localization, gameControllerService, uiLayout, allowDebugMode);
        _zoomControl     = new ZoomControlRenderer(inputService, uiLayout);

        _playerCivPanel = new PlayerCivilizationPanelRenderer(
            gameControllerService,
            localization,
            closeAll: CloseAll,
            tradeRenderer,
            prestigeRenderer,
            targetSelectionService: null,
            tooltipRenderer);
        _playerCivPanel.OnExpanded = () => { if (_uiLayout.TabsAtBottom) DeselectCityAndMonument(); };

        _inputService.PointerPressed  += HandlePointerPressed;
        _inputService.PointerMoved    += HandlePointerMoved;
        _inputService.PointerReleased += HandlePointerReleased;
        _inputService.ZoomChanged     += HandleZoomChanged;
        _inputService.KeyPressed      += HandleKeyInput;
        _inputService.KeyReleased     += HandleKeyRelease;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _uiLayout.UpdateCanvasSize(canvasSize);

        _playerResourcesOverlayRenderer.Initialize(canvasSize);
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
        _zoomControl.Initialize(canvasSize, _uiLayout.UiScale);

        _playerResourcesOverlayRenderer.ShowGearInBar = !_uiLayout.TimeSettingsOnSecondRow && !_uiLayout.ResourcesOnOwnRow;

        float scale = _uiLayout.UiScale;
        _timeControlRenderer.Initialize(canvasSize, _uiLayout.GearX - 8f * scale, _uiLayout.TimeControlRowTop, scale);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_isVisible) return;

        if (context.GameState is MainGameState mgs)
            _uiLayout.SetMenuPosition(mgs.Settings.ForceMenuPosition);

        _tabBar.Update(context);
        _playerResourcesOverlayRenderer.ResourceStartX = _tabBar.ResourceStartX;

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

        bool gearInline = !_uiLayout.TimeSettingsOnSecondRow && !_uiLayout.ResourcesOnOwnRow;
        _playerResourcesOverlayRenderer.ShowGearInBar = gearInline;
        _playerResourcesOverlayRenderer.RowTop = _uiLayout.ResourcesRowTop;
        _playerResourcesOverlayRenderer.RightReservedWidth = (_uiLayout.TimeSettingsOnSecondRow || _uiLayout.ResourcesOnOwnRow)
            ? UILayoutService.BarPadding * _uiLayout.UiScale
            : _uiLayout.TimeSettingsBlockWidth;
        _playerResourcesOverlayRenderer.ShowScrollArrows = _uiLayout.ResourcesOverflow;

        // Menu en haut, ressources en débordement : la barre de ressources a migré sur sa propre ligne,
        // il faut donc fournir un fond pour la ligne du haut qui ne contient plus que les tabs (et le gear/temps).
        if (_uiLayout.ResourcesOnOwnRow)
            DrawRowBackground(canvas, 0f);

        _playerResourcesOverlayRenderer.Render(canvas, context);
        _uiLayout.SetResourcesContentWidth(_playerResourcesOverlayRenderer.TotalResourcesContentWidth);

        switch (activeTab)
        {
            case TabBarRenderer.TabResearch:
                _researchRenderer.Render(canvas, context);
                break;
            case TabBarRenderer.TabPrestige:
                _prestigeMapRenderer.RenderPrestigeMap(canvas, context);
                break;
            case TabBarRenderer.TabStats:
                _prestigeHistoryRenderer.RenderHistory(canvas, context);
                break;
            case TabBarRenderer.TabEvents:
                _eventLogRenderer.RenderEvents(canvas, context);
                break;
            case TabBarRenderer.TabAutomation:
                _automationRenderer.RenderAutomationPage(canvas, context);
                break;
            case TabBarRenderer.TabRituals:
                _ritualsRenderer.RenderRitualsPage(canvas, context);
                break;
            case TabBarRenderer.TabAscension:
                _ascensionRenderer.RenderAscensionPage(canvas, context);
                break;
            case TabBarRenderer.TabHistory:
                _historyRenderer?.RenderHistory(canvas, context);
                break;
            default:
                _playerCivPanel.Render(canvas, context);
                _selectedCityPanelRenderer.Render(canvas, context);
                _selectedMonumentPanelRenderer.Render(canvas, context);
                break;
        }

        _tabBar.Render(canvas);

        if (_uiLayout.TimeSettingsOnSecondRow)
            DrawRowBackground(canvas, _uiLayout.TimeControlRowTop);

        float timeControlScale = _uiLayout.UiScale;
        _timeControlRenderer.Initialize(_canvasSize, _uiLayout.GearX - 8f * timeControlScale, _uiLayout.TimeControlRowTop, timeControlScale);
        _timeControlRenderer.Render(canvas, context);

        float gearX = _uiLayout.GearX;
        if (!gearInline)
            DrawWrappedGearIcon(canvas, gearX, _uiLayout.TimeControlRowTop);
        _settingsMenu.Draw(canvas, gearX, _uiLayout.SecondRowBottom);

        _tradeRenderer.Render(canvas, _uiLayout.UiScale);
        _prestigeRenderer.Render(canvas);
        _settingsPopupRenderer.Render(canvas, _uiLayout.UiScale);

        if (IsMapViewTab(activeTab))
        {
            _zoomControl.Initialize(_canvasSize, _uiLayout.UiScale);
            _zoomControl.Render(canvas);
        }

        CheckResourceBarTooltip();
    }

    /// Fond générique pour toute ligne ne bénéficiant pas déjà du fond dessiné par la barre de ressources :
    /// la ligne du haut (rowTop=0) quand les ressources en ont été reléguées ailleurs, ou une ligne secondaire
    /// dédiée au bloc temps+paramètres (rowTop&gt;0, plus basse que la barre principale).
    private void DrawRowBackground(SKCanvas canvas, float rowTop)
    {
        float scale = _uiLayout.UiScale;
        float rowH  = (rowTop > 0f ? UILayoutService.SecondRowHeight : UILayoutService.TopBarHeight) * scale;
        float cr    = 4 * scale;
        var rowRect = new SKRect(0, rowTop, _canvasSize.Width, rowTop + rowH);
        canvas.DrawRoundRect(rowRect, cr, cr, _secondRowBgPaint);
        canvas.DrawRoundRect(rowRect, cr, cr, _secondRowBorderPaint);
    }

    /// Dessine le gear seul quand il n'est pas inline dans la barre de ressources (voir DrawRowBackground pour
    /// le fond de sa ligne).
    private void DrawWrappedGearIcon(SKCanvas canvas, float gearX, float rowTop)
    {
        float scale    = _uiLayout.UiScale;
        float rowH     = (rowTop > 0f ? UILayoutService.SecondRowHeight : UILayoutService.TopBarHeight) * scale;
        float iconSize = UILayoutService.GearIconSize * scale;
        float gearY    = rowTop + (rowH - iconSize) / 2f;
        _wrappedGearRect = new SKRect(gearX, gearY, gearX + iconSize, gearY + iconSize);
        _playerResourcesOverlayRenderer.DrawGearAt(canvas, gearX, gearY, iconSize);
    }

    private void CheckResourceBarTooltip()
    {
        var hoveredResource = _playerResourcesOverlayRenderer.GetResourceAtPoint(_lastPointerPosition);
        if (!hoveredResource.HasValue) return;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return;

        string resourceName = _localization.Get($"resource_{hoveredResource.Value.ToString().ToLower()}");
        var controller = _gameControllerService.MainGameController;
        int civIndex = worldState.PlayerCivilization.Index;

        var gains = controller.HarvestController.GetAverageProductionRatesPerSecond(civIndex);
        var losses = controller.HarvestController.GetAverageConsumptionRatesPerSecond(civIndex);

        if (hoveredResource.Value == SettlersOfIdlestan.Model.IslandMap.Resource.Crystal)
        {
            double crystalUpkeep = controller.MagicController.GetCrystalRateBreakdown().RitualUpkeepPerSecond;
            if (crystalUpkeep > 0.0001)
                losses[SettlersOfIdlestan.Model.IslandMap.Resource.Crystal] =
                    (losses.TryGetValue(SettlersOfIdlestan.Model.IslandMap.Resource.Crystal, out var prev) ? prev : 0.0) + crystalUpkeep;
        }

        bool hasGain = gains.TryGetValue(hoveredResource.Value, out double gain) && gain > 0.0001;
        bool hasLoss = losses.TryGetValue(hoveredResource.Value, out double loss) && loss > 0.0001;

        if (!hasGain && !hasLoss)
        {
            _tooltipRenderer.SetTooltip(resourceName, _lastPointerPosition);
            return;
        }

        var lines = new System.Collections.Generic.List<string> { resourceName };
        if (hasGain) lines.Add($"+{gain:F2}/s");
        if (hasLoss) lines.Add($"-{loss:F2}/s");
        _tooltipRenderer.SetTooltipLines(lines.ToArray(), _lastPointerPosition);
    }

    public void ConnectTargetSelectionService(TargetSelectionService targetSelectionService)
    {
        _targetSelectionService = targetSelectionService;
        _playerCivPanel.ConnectTargetSelectionService(targetSelectionService);
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen
                                 || _settingsMenu.IsOpen  || _settingsPopupRenderer.IsOpen;

    public bool IsPointBlockedByUI(SKPoint point) =>
        IsAnyOverlayOpen
        || !IsIslandTabActive
        || _selectedCityPanelRenderer.ContainsPoint(point)
        || _selectedMonumentPanelRenderer.ContainsPoint(point)
        || _playerCivPanel.ContainsPoint(point)
        || _zoomControl.ContainsPoint(point)
        || _tabBar.ContainsPoint(point)
        || _timeControlRenderer.ContainsPoint(point)
        || GetGearRect().Contains(point.X, point.Y)
        || (_uiLayout.ResourcesOverflow && point.Y < _uiLayout.ResourceBarBottom);

    public bool IsIslandTabActive => IsMapViewTab(_tabBar.ActiveTab);

    /// True for the tabs that show the hex map (Island / Underworld / Abyss) rather than a full-screen panel.
    private static bool IsMapViewTab(int tabId) =>
        tabId is TabBarRenderer.TabIsland or TabBarRenderer.TabUnderworld or TabBarRenderer.TabAbyss;

    /// Switches <see cref="WorldState.CurrentViewedLayer"/> to match a click on Island/Underworld/Abyss.
    private void ApplyLayerForActiveTab()
    {
        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return;

        int? targetLayer = _tabBar.ActiveTab switch
        {
            TabBarRenderer.TabIsland     => IslandMap.SurfaceLayer,
            TabBarRenderer.TabUnderworld => LayerState.UnderworldZ,
            TabBarRenderer.TabAbyss      => LayerState.AbyssZ,
            _ => null,
        };
        if (targetLayer == null || worldState.CurrentViewedLayer == targetLayer.Value) return;

        worldState.CurrentViewedLayer = targetLayer.Value;
        DeselectCityAndMonument();
    }

    private SKRect GetGearRect()
    {
        bool gearInline = !_uiLayout.TimeSettingsOnSecondRow && !_uiLayout.ResourcesOnOwnRow;
        return gearInline ? _playerResourcesOverlayRenderer.GearRect : _wrappedGearRect;
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        if (_isDraggingResources)
        {
            float delta        = _resourceDragLastX - e.Position.X;
            _resourceDragLastX = e.Position.X;
            _playerResourcesOverlayRenderer.ScrollBy(delta);
            return;
        }

        if (_settingsPopupRenderer.IsOpen) _settingsPopupRenderer.HandlePointerMoved(e.Position);
        if (_tradeRenderer.IsOpen)         _tradeRenderer.HandlePointerMoved(e.Position);
        if (_prestigeRenderer.IsOpen)      _prestigeRenderer.HandlePointerMoved(e.Position);

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)   _prestigeMapRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabAutomation) _automationRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabRituals)    _ritualsRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabAscension)  _ascensionRenderer.HandlePointerMoved(e.Position);
        if (IsMapViewTab(activeTab))                   _playerCivPanel.HandlePointerMoved(e.Position);

        _lastPointerPosition = e.Position;
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;
        if (_suppressNextPress) { _suppressNextPress = false; return; }

        if (_settingsPopupRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (_prestigeRenderer.HandlePointerPressed(e.Position, e.Button))      return;
        if (_tradeRenderer.HandlePointerPressed(e.Position, e.Button))         return;
        if (e.Button != PointerButton.Left) return;

        var gearRect = GetGearRect();
        if (gearRect != default && gearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
            return;
        }

        if (_uiLayout.ResourcesOverflow && _playerResourcesOverlayRenderer.HandleArrowClick(e.Position))
            return;

        if (_uiLayout.ResourcesOverflow && e.Position.Y < _uiLayout.ResourceBarBottom)
        {
            _isDraggingResources = true;
            _resourceDragLastX   = e.Position.X;
            return;
        }

        if (_tabBar.HandlePointerPressed(e.Position))
        {
            ApplyLayerForActiveTab();
            return;
        }

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)   { _prestigeMapRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab == TabBarRenderer.TabAutomation) { _automationRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab == TabBarRenderer.TabRituals)    { _ritualsRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab == TabBarRenderer.TabAscension)  { _ascensionRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab == TabBarRenderer.TabStats)      { _prestigeHistoryRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab is TabBarRenderer.TabResearch or TabBarRenderer.TabEvents) return;

        _playerCivPanel.HandlePointerPressed(e.Position);
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

    public void ConnectZoomCallbacks(Action zoomIn, Action zoomOut)
    {
        _zoomControl.OnZoomIn  = zoomIn;
        _zoomControl.OnZoomOut = zoomOut;
    }

    public void SwitchToPrestigeTab() => _tabBar.SetActiveTab(TabBarRenderer.TabPrestige);
    public void SwitchToIslandTab() => _tabBar.SetActiveTab(TabBarRenderer.TabIsland);
    public void SwitchToResearchTab() => _tabBar.SetActiveTab(TabBarRenderer.TabResearch);

    private void HandlePointerReleased(object? sender, PointerEventArgs e)
    {
        _isDraggingResources = false;
        if (!_isVisible) return;
        if (_settingsPopupRenderer.IsOpen) _settingsPopupRenderer.HandlePointerReleased(e.Position);
        if (_tradeRenderer.IsOpen) _tradeRenderer.HandlePointerReleased(e.Position);
        if (_tabBar.ActiveTab == TabBarRenderer.TabPrestige)
            _prestigeMapRenderer.HandlePointerReleased(e.Position);
        if (_tabBar.ActiveTab == TabBarRenderer.TabAutomation)
            _automationRenderer.HandlePointerReleased(e.Position);
        if (_tabBar.ActiveTab == TabBarRenderer.TabRituals)
            _ritualsRenderer.HandlePointerReleased(e.Position);
    }

    private void HandleZoomChanged(object? sender, ZoomEventArgs e)
    {
        if (!_isVisible) return;
        if (_settingsPopupRenderer.IsOpen)
        {
            _settingsPopupRenderer.HandleScroll(e.ZoomDelta);
            return;
        }
        if (_tradeRenderer.IsOpen)
        {
            _tradeRenderer.HandleScroll(e.ZoomDelta);
            return;
        }
        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)
        {
            _prestigeMapRenderer.HandleZoom(e);
            return;
        }
        if (IsMapViewTab(activeTab))
        {
            if (_selectedCityPanelRenderer.ContainsPoint(e.Center))
                _selectedCityPanelRenderer.HandleScroll(e.ZoomDelta);
            else if (_selectedMonumentPanelRenderer.ContainsPoint(e.Center))
                _selectedMonumentPanelRenderer.HandleScroll(e.ZoomDelta);
        }
        if (activeTab == TabBarRenderer.TabAutomation)
            _automationRenderer.HandleScroll(e.ZoomDelta);
        if (activeTab == TabBarRenderer.TabRituals)
            _ritualsRenderer.HandleScroll(e.ZoomDelta);
    }

    private void HandleKeyInput(object? sender, KeyEventArgs e)
    {
        if (!_isVisible) return;
        if (_tradeRenderer.IsOpen)
        {
            _tradeRenderer.HandleKeyDown(e.Key);
            return;
        }
        if (_settingsPopupRenderer.IsOpen && _settingsPopupRenderer.HandleKeyPressed(e.Key)) return;
        _tabBar.HandleKeyInput(e.Key);
    }

    private void HandleKeyRelease(object? sender, KeyEventArgs e)
    {
        if (!_isVisible) return;
        if (_tradeRenderer.IsOpen)
            _tradeRenderer.HandleKeyUp(e.Key);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _inputService.PointerPressed  -= HandlePointerPressed;
        _inputService.PointerMoved    -= HandlePointerMoved;
        _inputService.PointerReleased -= HandlePointerReleased;
        _inputService.ZoomChanged     -= HandleZoomChanged;
        _inputService.KeyPressed      -= HandleKeyInput;
        _inputService.KeyReleased     -= HandleKeyRelease;

        _playerResourcesOverlayRenderer.Dispose();
        _selectedCityPanelRenderer.Dispose();
        _selectedMonumentPanelRenderer.Dispose();
        _settingsMenu.Dispose();
        _tradeRenderer.Dispose();
        _prestigeRenderer.Dispose();
        _prestigeMapRenderer.Dispose();
        _prestigeHistoryRenderer.Dispose();
        _timeControlRenderer.Dispose();
        _researchRenderer.Dispose();
        _eventLogRenderer.Dispose();
        _automationRenderer.Dispose();
        _ritualsRenderer.Dispose();
        _ascensionRenderer.Dispose();
        _historyRenderer?.Dispose();
        _playerCivPanel.Dispose();
        _tabBar.Dispose();
        _zoomControl.Dispose();
        _secondRowBgPaint.Dispose();
        _secondRowBorderPaint.Dispose();

        _disposed = true;
    }
}
