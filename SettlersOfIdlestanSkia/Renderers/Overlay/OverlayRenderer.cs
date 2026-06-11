using System;
using SettlersOfIdlestan.Model.Game;
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
    private readonly SelectedWonderPanelRenderer _selectedWonderPanelRenderer;
    private readonly TradePopupRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private readonly PrestigeMapRenderer _prestigeMapRenderer;
    private readonly PrestigeHistoryRenderer _prestigeHistoryRenderer;
    private readonly TimeControlRenderer _timeControlRenderer;
    private readonly ResearchRenderer _researchRenderer;
    private readonly EventLogRenderer _eventLogRenderer;
    private readonly AutomationRenderer _automationRenderer;
    private readonly RitualsRenderer _ritualsRenderer;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly PlayerCivilizationPanelRenderer _playerCivPanel;
    private readonly TabBarRenderer _tabBar;
    private readonly MapSwitchButtonRenderer _mapSwitchButton;

    private readonly UILayoutService _uiLayout;
    private SKSize _canvasSize;
    private SKPoint _lastPointerPosition;
    private WonderSelectionService? _wonderSelectionService;

    // Mobile second row (time controls + gear background)
    private SKRect _mobileGearRect;
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
        SelectedWonderPanelRenderer selectedWonderPanelRenderer,
        TradePopupRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        PrestigeMapRenderer prestigeMapRenderer,
        PrestigeHistoryRenderer prestigeHistoryRenderer,
        TimeControlRenderer timeControlRenderer,
        ResearchRenderer researchRenderer,
        EventLogRenderer eventLogRenderer,
        AutomationRenderer automationRenderer,
        RitualsRenderer ritualsRenderer,
        TooltipRenderer tooltipRenderer,
        UILayoutService uiLayout)
    {
        _uiLayout                       = uiLayout;
        _inputService                   = inputService;
        _gameControllerService          = gameControllerService;
        _localization                   = localization;
        _playerResourcesOverlayRenderer = playerResourcesOverlayRenderer;
        _settingsMenu                   = settingsMenu;
        _settingsPopupRenderer          = settingsPopupRenderer;
        _selectedCityPanelRenderer      = selectedCityPanelRenderer;
        _selectedWonderPanelRenderer    = selectedWonderPanelRenderer;
        _tradeRenderer                  = tradeRenderer;
        _prestigeRenderer               = prestigeRenderer;
        _prestigeMapRenderer            = prestigeMapRenderer;
        _prestigeHistoryRenderer        = prestigeHistoryRenderer;
        _timeControlRenderer            = timeControlRenderer;
        _researchRenderer               = researchRenderer;
        _eventLogRenderer               = eventLogRenderer;
        _automationRenderer             = automationRenderer;
        _ritualsRenderer                = ritualsRenderer;
        _tooltipRenderer                = tooltipRenderer;

        _tabBar          = new TabBarRenderer(localization, gameControllerService, uiLayout);
        _mapSwitchButton = new MapSwitchButtonRenderer(localization, uiLayout, gameControllerService);

        _playerCivPanel = new PlayerCivilizationPanelRenderer(
            gameControllerService,
            localization,
            closeAll: CloseAll,
            tradeRenderer,
            prestigeRenderer,
            wonderSelectionService: null,
            tooltipRenderer);
        _playerCivPanel.OnExpanded = () => { if (_uiLayout.IsMobile) DeselectCityAndWonder(); };

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
        _selectedWonderPanelRenderer.Initialize(canvasSize);
        _tradeRenderer.Initialize(canvasSize);
        _prestigeRenderer.Initialize(canvasSize);
        _settingsPopupRenderer.Initialize(canvasSize);
        _prestigeMapRenderer.Initialize(canvasSize);
        _prestigeHistoryRenderer.Initialize(canvasSize);
        _researchRenderer.Initialize(canvasSize);
        _eventLogRenderer.Initialize(canvasSize);
        _automationRenderer.Initialize(canvasSize);
        _ritualsRenderer.Initialize(canvasSize);
        _playerCivPanel.Initialize(canvasSize);
        _tabBar.Initialize(canvasSize);
        _mapSwitchButton.Initialize(canvasSize);

        _playerResourcesOverlayRenderer.ShowGearInBar = !_uiLayout.IsMobile;

        float scale = _uiLayout.UiScale;
        _timeControlRenderer.Initialize(canvasSize, _uiLayout.GearX - 8f * scale, _uiLayout.TimeControlRowTop, scale);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_isVisible) return;

        _tabBar.Update(context);
        _playerResourcesOverlayRenderer.ResourceStartX = _tabBar.ResourceStartX;

        int activeTab      = _tabBar.ActiveTab;
        bool isMobile      = _uiLayout.IsMobile;
        bool panelsEnabled = activeTab == TabBarRenderer.TabIsland
                          && !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
        _selectedCityPanelRenderer.IsInputEnabled  = panelsEnabled;
        _selectedWonderPanelRenderer.IsInputEnabled = panelsEnabled;
        _researchRenderer.IsActive = activeTab == TabBarRenderer.TabResearch;

        float panelTop = _uiLayout.PanelTopY;
        _playerCivPanel.TopOverride              = panelTop;
        _selectedWonderPanelRenderer.TopOverride = panelTop;
        _selectedCityPanelRenderer.TopOverride   = panelTop;

        if (isMobile)
        {
            bool rightPanelOpen = _gameControllerService.CityBuildingService?.SelectedCity != null
                               || _selectedWonderPanelRenderer.HasSelection;
            if (rightPanelOpen && !_playerCivPanel.IsCollapsed)
                _playerCivPanel.Collapse();
        }

        _playerResourcesOverlayRenderer.Render(canvas, context);
        _tabBar.Render(canvas);

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
            default:
                _playerCivPanel.Render(canvas, context);
                _selectedCityPanelRenderer.Render(canvas, context);
                _selectedWonderPanelRenderer.Render(canvas, context);
                break;
        }

        if (isMobile)
            DrawMobileSecondRowBackground(canvas);

        _timeControlRenderer.Render(canvas, context);

        float gearX = _uiLayout.GearX;
        if (isMobile)
            DrawMobileGearIcon(canvas, gearX);
        _settingsMenu.Draw(canvas, gearX, _uiLayout.SecondRowBottom);

        _tradeRenderer.Render(canvas, _uiLayout.UiScale);
        _prestigeRenderer.Render(canvas);
        _settingsPopupRenderer.Render(canvas, _uiLayout.UiScale);

        _mapSwitchButton.Render(canvas);
        CheckResourceBarTooltip();
    }

    private void DrawMobileSecondRowBackground(SKCanvas canvas)
    {
        float scale  = _uiLayout.UiScale;
        float rowTop = _uiLayout.ResourceBarBottom;
        float rowH   = UILayoutService.SecondRowHeight * scale;
        float cr     = 4 * scale;
        var rowRect  = new SKRect(0, rowTop, _canvasSize.Width, rowTop + rowH);
        canvas.DrawRoundRect(rowRect, cr, cr, _secondRowBgPaint);
        canvas.DrawRoundRect(rowRect, cr, cr, _secondRowBorderPaint);
    }

    private void DrawMobileGearIcon(SKCanvas canvas, float gearX)
    {
        float scale    = _uiLayout.UiScale;
        float rowTop   = _uiLayout.ResourceBarBottom;
        float rowH     = UILayoutService.SecondRowHeight * scale;
        float iconSize = UILayoutService.GearIconSize * scale;
        float gearY    = rowTop + (rowH - iconSize) / 2f;
        _mobileGearRect = new SKRect(gearX, gearY, gearX + iconSize, gearY + iconSize);
        _playerResourcesOverlayRenderer.DrawGearAt(canvas, gearX, gearY, iconSize);
    }

    private void CheckResourceBarTooltip()
    {
        var hoveredResource = _playerResourcesOverlayRenderer.GetResourceAtPoint(_lastPointerPosition);
        if (!hoveredResource.HasValue) return;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null) return;

        string resourceName = _localization.Get($"resource_{hoveredResource.Value.ToString().ToLower()}");
        var rates = _gameControllerService.MainGameController.HarvestController
            .GetAverageProductionRatesPerSecond(worldState.PlayerCivilization.Index);

        if (rates.TryGetValue(hoveredResource.Value, out double rate) && rate > 0.0001)
            _tooltipRenderer.SetTooltipLines(new[] { resourceName, $"+{rate:F2}/s" }, _lastPointerPosition);
        else
            _tooltipRenderer.SetTooltip(resourceName, _lastPointerPosition);
    }

    public void ConnectWonderService(WonderSelectionService wonderSelectionService)
    {
        _wonderSelectionService = wonderSelectionService;
        _playerCivPanel.ConnectWonderSelectionService(wonderSelectionService);
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen
                                 || _settingsMenu.IsOpen  || _settingsPopupRenderer.IsOpen;

    public bool IsPointBlockedByUI(SKPoint point) =>
        IsAnyOverlayOpen
        || _selectedCityPanelRenderer.ContainsPoint(point)
        || _selectedWonderPanelRenderer.ContainsPoint(point)
        || _playerCivPanel.ContainsPoint(point)
        || (_uiLayout.IsMobile && point.Y < _uiLayout.ResourceBarBottom);

    public bool IsIslandTabActive => _tabBar.ActiveTab == TabBarRenderer.TabIsland;

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        if (_isDraggingResources)
        {
            float delta        = _resourceDragLastX - e.Position.X;
            _resourceDragLastX = e.Position.X;
            float visibleW     = _canvasSize.Width - UILayoutService.BarPadding * _uiLayout.UiScale;
            float maxScroll    = Math.Max(0f, _playerResourcesOverlayRenderer.TotalResourcesContentWidth - visibleW);
            _playerResourcesOverlayRenderer.ScrollOffset =
                Math.Clamp(_playerResourcesOverlayRenderer.ScrollOffset + delta, 0f, maxScroll);
            return;
        }

        if (_settingsPopupRenderer.IsOpen) _settingsPopupRenderer.HandlePointerMoved(e.Position);
        if (_tradeRenderer.IsOpen)         _tradeRenderer.HandlePointerMoved(e.Position);
        if (_prestigeRenderer.IsOpen)      _prestigeRenderer.HandlePointerMoved(e.Position);

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)   _prestigeMapRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabAutomation) _automationRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabRituals)    _ritualsRenderer.HandlePointerMoved(e.Position);
        if (activeTab == TabBarRenderer.TabIsland)     _playerCivPanel.HandlePointerMoved(e.Position);

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

        bool isMobile = _uiLayout.IsMobile;

        var gearRect = isMobile ? _mobileGearRect : _playerResourcesOverlayRenderer.GearRect;
        if (gearRect != default && gearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
            return;
        }

        if (isMobile && e.Position.Y < _uiLayout.ResourceBarBottom)
        {
            _isDraggingResources = true;
            _resourceDragLastX   = e.Position.X;
            return;
        }

        if (_mapSwitchButton.HandlePointerPressed(e.Position, onSwitchedToUnderworld: () =>
        {
            _tabBar.SetActiveTab(TabBarRenderer.TabIsland);
            DeselectCityAndWonder();
        }))
        {
            DeselectCityAndWonder();
            return;
        }

        if (_tabBar.HandlePointerPressed(e.Position)) return;

        int activeTab = _tabBar.ActiveTab;
        if (activeTab == TabBarRenderer.TabPrestige)   { _prestigeMapRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab == TabBarRenderer.TabAutomation) { _automationRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab == TabBarRenderer.TabRituals)    { _ritualsRenderer.HandlePointerPressed(e.Position); return; }
        if (activeTab is TabBarRenderer.TabStats or TabBarRenderer.TabResearch or TabBarRenderer.TabEvents) return;

        _playerCivPanel.HandlePointerPressed(e.Position);
    }

    private void DeselectCityAndWonder()
    {
        _selectedCityPanelRenderer.Close();
        _selectedWonderPanelRenderer.Close();
    }

    public void CloseAll()
    {
        _settingsMenu.Close();
        _settingsPopupRenderer.Close();
        _tradeRenderer.Close();
        _prestigeRenderer.Close();
        DeselectCityAndWonder();
        _selectedCityPanelRenderer.IsInputEnabled  = false;
        _selectedWonderPanelRenderer.IsInputEnabled = false;
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
        _selectedWonderPanelRenderer.IsInputEnabled = true;
        if (suppressNextPress) _suppressNextPress = true;
    }

    public void SwitchToPrestigeTab() => _tabBar.SetActiveTab(TabBarRenderer.TabPrestige);

    private void HandlePointerReleased(object? sender, PointerEventArgs e)
    {
        _isDraggingResources = false;
        if (!_isVisible) return;
        if (_tradeRenderer.IsOpen) _tradeRenderer.HandlePointerReleased(e.Position);
        if (_tabBar.ActiveTab == TabBarRenderer.TabPrestige)
            _prestigeMapRenderer.HandlePointerReleased(e.Position);
    }

    private void HandleZoomChanged(object? sender, ZoomEventArgs e)
    {
        if (!_isVisible) return;
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
        if (activeTab == TabBarRenderer.TabIsland)
        {
            if (_selectedCityPanelRenderer.ContainsPoint(e.Center))
                _selectedCityPanelRenderer.HandleScroll(e.ZoomDelta);
            else if (_selectedWonderPanelRenderer.ContainsPoint(e.Center))
                _selectedWonderPanelRenderer.HandleScroll(e.ZoomDelta);
        }
    }

    private void HandleKeyInput(object? sender, KeyEventArgs e)
    {
        if (!_isVisible) return;
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
        _selectedWonderPanelRenderer.Dispose();
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
        _playerCivPanel.Dispose();
        _tabBar.Dispose();
        _mapSwitchButton.Dispose();
        _secondRowBgPaint.Dispose();
        _secondRowBorderPaint.Dispose();

        _disposed = true;
    }
}
