using System;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public sealed class OverlayRenderer : IGameRenderer
{
    private const float TabWidth = 62;
    private const float TabHeight = 28;
    private const float TabMarginLeft = 8;
    private const float TabSpacing = 5;
    private const float MobileTabHeight = UILayoutService.MobileTabBarHeight;

    // Logical tab IDs (stable, independent of visual position)
    private const int TabIsland     = 0;
    private const int TabResearch   = 1;
    private const int TabPrestige   = 2;
    private const int TabStats      = 3;
    private const int TabEvents     = 4;
    private const int TabAutomation = 5;

    private readonly InputHandlingService _inputService;
    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly PlayerResourcesOverlayRenderer _playerResourcesOverlayRenderer;
    private readonly SettingsMenu _settingsMenu;
    private readonly SettingsPopupRenderer _settingsPopupRenderer;
    private readonly SelectedCityPanelRenderer _selectedCityPanelRenderer;
    private readonly SelectedWonderPanelRenderer _selectedWonderPanelRenderer;
    private readonly TradeRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private readonly PrestigeMapRenderer _prestigeMapRenderer;
    private readonly PrestigeHistoryRenderer _prestigeHistoryRenderer;
    private readonly TimeControlRenderer _timeControlRenderer;
    private readonly ResearchRenderer _researchRenderer;
    private readonly EventLogRenderer _eventLogRenderer;
    private readonly AutomationRenderer _automationRenderer;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly PlayerCivilizationPanelRenderer _playerCivPanel;

    private readonly SKPaint _buttonTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _disabledTextPaint = new() { Color = new SKColor(180, 180, 185), IsAntialias = true };

    private readonly SKPaint _activeTabPaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactiveTabPaint = new() { Color = new SKColor(35, 35, 45), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _blinkTabPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _activeTabBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKFont _tabFont = new() { Size = 12, Typeface = SkiaFonts.Bold };

    private readonly UILayoutService _uiLayout;
    private SKSize _canvasSize;
    private SKPoint _lastPointerPosition;
    private WonderSelectionService? _wonderSelectionService;

    // Deuxième ligne mobile (horloge + gear)
    private SKRect _mobileGearRect;
    private readonly SKPaint _secondRowBgPaint = new() { Color = new SKColor(0, 0, 0, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _secondRowBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };

    // Drag horizontal des ressources (mode mobile)
    private bool _isDraggingResources;
    private float _resourceDragLastX;

    // Map switch button (surface ↔ underworld)
    private SKRect _mapSwitchRect;
    private readonly SKPaint _mapSwitchActivePaint = new() { Color = new SKColor(40, 25, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _mapSwitchBorderPaint = new() { Color = new SKColor(160, 100, 220), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKFont _mapSwitchFont = new() { Size = 11, Typeface = SkiaFonts.Bold };

    // Dynamic tab list: (tabId, screenRect) computed each frame
    private readonly List<(int tabId, SKRect rect)> _activeTabs = new();

    private int _activeTab = TabIsland;
    private bool _hasResearchTab;
    private bool _hasAutomationTab;
    private bool _hasNewEvent;
    private int? _seenEventCount;
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
        TradeRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        PrestigeMapRenderer prestigeMapRenderer,
        PrestigeHistoryRenderer prestigeHistoryRenderer,
        TimeControlRenderer timeControlRenderer,
        ResearchRenderer researchRenderer,
        EventLogRenderer eventLogRenderer,
        AutomationRenderer automationRenderer,
        TooltipRenderer tooltipRenderer,
        UILayoutService uiLayout)
    {
        _uiLayout = uiLayout;
        _inputService = inputService;
        _gameControllerService = gameControllerService;
        _localization = localization;
        _playerResourcesOverlayRenderer = playerResourcesOverlayRenderer;
        _settingsMenu = settingsMenu;
        _settingsPopupRenderer = settingsPopupRenderer;
        _selectedCityPanelRenderer = selectedCityPanelRenderer;
        _selectedWonderPanelRenderer = selectedWonderPanelRenderer;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _prestigeMapRenderer = prestigeMapRenderer;
        _prestigeHistoryRenderer = prestigeHistoryRenderer;
        _timeControlRenderer = timeControlRenderer;
        _researchRenderer = researchRenderer;
        _eventLogRenderer = eventLogRenderer;
        _automationRenderer = automationRenderer;
        _tooltipRenderer = tooltipRenderer;
        _playerCivPanel = new PlayerCivilizationPanelRenderer(
            gameControllerService,
            localization,
            closeAll: CloseAll,
            tradeRenderer,
            prestigeRenderer,
            wonderSelectionService: null,
            tooltipRenderer);
        _playerCivPanel.OnExpanded = () => { if (_uiLayout.IsMobile) DeselectCityAndWonder(); };
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.PointerReleased += HandlePointerReleased;
        _inputService.ZoomChanged += HandleZoomChanged;
        _inputService.KeyPressed += HandleKeyInput;
        _inputService.KeyReleased += HandleKeyRelease;
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
        _playerCivPanel.Initialize(canvasSize);

        bool isMobile = _uiLayout.IsMobile;
        _playerResourcesOverlayRenderer.ShowGearInBar = !isMobile;

        float gearX = canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;
        float timeControlRight = gearX - 8f;
        float rowTop = isMobile ? PlayerResourcesOverlayRenderer.BarHeight : 0f;
        _timeControlRenderer.Initialize(canvasSize, timeControlRight, rowTop);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (!_isVisible) return;

        bool showTabs = HasPrestigePoints(context);
        _hasResearchTab = IsResearchUnlocked();
        _hasAutomationTab = HasAnyAutomation();

        // Sanitize active tab if it's no longer available
        if (!_hasResearchTab && _activeTab == TabResearch) _activeTab = TabIsland;
        if (!showTabs && (_activeTab == TabPrestige || _activeTab == TabStats || _activeTab == TabEvents)) _activeTab = TabIsland;
        if (!_hasAutomationTab && _activeTab == TabAutomation) _activeTab = TabIsland;

        // Build the ordered list of visible tabs for this frame
        _activeTabs.Clear();
        _activeTabs.Add((TabIsland, default));
        if (_hasResearchTab) _activeTabs.Add((TabResearch, default));
        if (showTabs) { _activeTabs.Add((TabPrestige, default)); _activeTabs.Add((TabStats, default)); _activeTabs.Add((TabEvents, default)); }
        if (_hasAutomationTab) _activeTabs.Add((TabAutomation, default));

        bool isMobile = _uiLayout.IsMobile;
        if (_activeTabs.Count > 1)
        {
            if (isMobile)
            {
                // Tabs en bas, pleine largeur, plus hautes pour le tactile
                float tabY = _canvasSize.Height - MobileTabHeight - 2;
                float tabW = _canvasSize.Width / _activeTabs.Count;
                for (int i = 0; i < _activeTabs.Count; i++)
                {
                    float x = i * tabW;
                    _activeTabs[i] = (_activeTabs[i].tabId, new SKRect(x, tabY, x + tabW, tabY + MobileTabHeight));
                }
                _playerResourcesOverlayRenderer.ResourceStartX = PlayerResourcesOverlayRenderer.Padding;
            }
            else
            {
                float tabY = (PlayerResourcesOverlayRenderer.BarHeight - TabHeight) / 2;
                float tabX = TabMarginLeft;
                for (int i = 0; i < _activeTabs.Count; i++)
                {
                    var rect = new SKRect(tabX, tabY, tabX + TabWidth, tabY + TabHeight);
                    _activeTabs[i] = (_activeTabs[i].tabId, rect);
                    tabX += TabWidth + TabSpacing;
                }
                _playerResourcesOverlayRenderer.ResourceStartX = tabX + TabMarginLeft;
            }
        }
        else
        {
            _playerResourcesOverlayRenderer.ResourceStartX = PlayerResourcesOverlayRenderer.Padding;
        }

        bool onResearchTab    = _activeTab == TabResearch    && _hasResearchTab;
        bool onPrestigeTab    = _activeTab == TabPrestige;
        bool onHistoryTab     = _activeTab == TabStats;
        bool onEventsTab      = _activeTab == TabEvents;
        bool onAutomationTab  = _activeTab == TabAutomation  && _hasAutomationTab;

        int currentEventCount = _gameControllerService.CurrentWorldState?.EventLog?.Entries.Count ?? 0;
        if (_seenEventCount == null || _seenEventCount > currentEventCount)
        {
            _seenEventCount = currentEventCount;
            _hasNewEvent = false;
        }
        else if (onEventsTab)
        {
            _seenEventCount = currentEventCount;
            _hasNewEvent = false;
        }
        else if (currentEventCount > _seenEventCount)
        {
            _hasNewEvent = true;
        }

        bool isUnderworld = _gameControllerService.CurrentWorldState?.CurrentViewedLayer == LayerState.UnderworldZ;
        bool panelsEnabled = !onResearchTab && !onPrestigeTab && !onHistoryTab && !onEventsTab && !onAutomationTab
            && !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
        _selectedCityPanelRenderer.IsInputEnabled = panelsEnabled;
        _selectedWonderPanelRenderer.IsInputEnabled = panelsEnabled;
        _researchRenderer.IsActive = onResearchTab;

        // En mode mobile, les panneaux latéraux démarrent sous la 2e ligne
        float mobileTop = PlayerResourcesOverlayRenderer.BarHeight + PlayerResourcesOverlayRenderer.SecondRowHeight;
        _playerCivPanel.TopOverride    = isMobile ? mobileTop : 0f;
        _selectedWonderPanelRenderer.TopOverride = isMobile ? mobileTop : 0f;
        _selectedCityPanelRenderer.TopOverride   = isMobile ? mobileTop : 0f;

        // En mode mobile : exclusion mutuelle entre panneaux gauche et droit
        if (isMobile)
        {
            bool rightPanelOpen = _gameControllerService.CityBuildingService?.SelectedCity != null
                               || _selectedWonderPanelRenderer.HasSelection;
            if (rightPanelOpen && !_playerCivPanel.IsCollapsed)
                _playerCivPanel.Collapse();
        }

        _playerResourcesOverlayRenderer.Render(canvas, context);

        if (_activeTabs.Count > 1)
            DrawTabButtons(canvas);

        if (onResearchTab)
        {
            _researchRenderer.Render(canvas, context);
        }
        else if (onPrestigeTab)
        {
            _prestigeMapRenderer.RenderPrestigeMap(canvas, context);
        }
        else if (onHistoryTab)
        {
            _prestigeHistoryRenderer.RenderHistory(canvas, context);
        }
        else if (onEventsTab)
        {
            _eventLogRenderer.RenderEvents(canvas, context);
        }
        else if (onAutomationTab)
        {
            _automationRenderer.RenderAutomationPage(canvas, context);
        }
        else
        {
            _playerCivPanel.Render(canvas, context);
            _selectedCityPanelRenderer.Render(canvas, context);
            _selectedWonderPanelRenderer.Render(canvas, context);
        }

        float gearX = _canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;

        // Ordre : fond 2e ligne → time controls → gear (pour que les boutons soient visibles)
        if (isMobile)
            DrawMobileSecondRowBackground(canvas);

        _timeControlRenderer.Render(canvas, context);

        if (isMobile)
        {
            DrawMobileGearIcon(canvas, gearX);
            _settingsMenu.Draw(canvas, gearX, PlayerResourcesOverlayRenderer.BarHeight + PlayerResourcesOverlayRenderer.SecondRowHeight);
        }
        else
        {
            _settingsMenu.Draw(canvas, gearX, PlayerResourcesOverlayRenderer.BarHeight);
        }

        _tradeRenderer.Render(canvas);
        _prestigeRenderer.Render(canvas);
        _settingsPopupRenderer.Render(canvas);

        DrawMapSwitchButton(canvas, context);
        CheckResourceBarTooltip();
    }

    private void DrawMobileSecondRowBackground(SKCanvas canvas)
    {
        float rowTop = PlayerResourcesOverlayRenderer.BarHeight;
        float rowH = PlayerResourcesOverlayRenderer.SecondRowHeight;
        var rowRect = new SKRect(0, rowTop, _canvasSize.Width, rowTop + rowH);
        canvas.DrawRoundRect(rowRect, 4, 4, _secondRowBgPaint);
        canvas.DrawRoundRect(rowRect, 4, 4, _secondRowBorderPaint);
    }

    private void DrawMobileGearIcon(SKCanvas canvas, float gearX)
    {
        float rowTop = PlayerResourcesOverlayRenderer.BarHeight;
        float rowH = PlayerResourcesOverlayRenderer.SecondRowHeight;
        float iconSize = PlayerResourcesOverlayRenderer.IconSize;
        float gearY = rowTop + (rowH - iconSize) / 2f;
        _mobileGearRect = new SKRect(gearX, gearY, gearX + iconSize, gearY + iconSize);
        _playerResourcesOverlayRenderer.DrawGearAt(canvas, gearX, gearY, iconSize);
    }

    private void DrawMapSwitchButton(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return;
        var worldState = mgs.CurrentWorldState;
        if (worldState == null || !worldState.Layers.ContainsKey(LayerState.UnderworldZ)) return;

        const float btnW = 130f;
        const float btnH = 22f;
        float btnX = (_canvasSize.Width - btnW) / 2f;
        float btnY = PlayerResourcesOverlayRenderer.BarHeight + 3f;
        _mapSwitchRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

        canvas.DrawRoundRect(_mapSwitchRect, 5, 5, _mapSwitchActivePaint);
        canvas.DrawRoundRect(_mapSwitchRect, 5, 5, _mapSwitchBorderPaint);

        string label = worldState.CurrentViewedLayer == LayerState.UnderworldZ
            ? _localization.Get("btn_map_surface")
            : _localization.Get("btn_map_underworld");
        canvas.DrawText(label, _mapSwitchRect.MidX, _mapSwitchRect.MidY + 4f, SKTextAlign.Center, _mapSwitchFont, _buttonTextPaint);
    }

    private void CheckResourceBarTooltip()
    {
        var hoveredResource = _playerResourcesOverlayRenderer.GetResourceAtPoint(_lastPointerPosition);
        if (!hoveredResource.HasValue) return;

        var WorldState = _gameControllerService.CurrentWorldState;
        if (WorldState == null) return;

        string resourceName = _localization.Get($"resource_{hoveredResource.Value.ToString().ToLower()}");

        var rates = _gameControllerService.MainGameController.HarvestController
            .GetAverageProductionRatesPerSecond(WorldState.PlayerCivilization.Index);

        if (rates.TryGetValue(hoveredResource.Value, out double rate) && rate > 0.0001)
            _tooltipRenderer.SetTooltipLines(new[] { resourceName, $"+{rate:F2}/s" }, _lastPointerPosition);
        else
            _tooltipRenderer.SetTooltip(resourceName, _lastPointerPosition);
    }

    private bool HasPrestigePoints(GameRenderContext context)
    {
        if (context.GameState is not MainGameState mainGameState) return false;
        return (mainGameState.PrestigeState?.TotalPrestigePointsEarned ?? 0) > 0;
    }

    private bool IsResearchUnlocked()
    {
        try { return _gameControllerService.MainGameController.ResearchController.IsResearchUnlocked(); }
        catch { return false; }
    }

    private bool HasAnyAutomation()
    {
        try
        {
            var civ = _gameControllerService.PlayerCivilization;
            if (civ == null) return false;
            foreach (var city in civ.Cities)
                foreach (var b in city.Buildings)
                    if (b.ProvidesAutomation && b.Level > 0) return true;
            var completed = civ.TechnologyTree.CompletedTechnologies;
            return completed.Contains(TechnologyId.AdvancedTactics) || completed.Contains(TechnologyId.AdvancedStrategy);
        }
        catch { return false; }
    }

    private bool ShouldBlinkResearchTab()
    {
        if (!_hasResearchTab || _activeTab == TabResearch) return false;
        try { return _gameControllerService.MainGameController.ResearchController.ActiveResearch == null; }
        catch { return false; }
    }

    private void DrawTabButtons(SKCanvas canvas)
    {
        bool blinkResearch = ShouldBlinkResearchTab();
        float blinkT = (float)(Math.Sin(Environment.TickCount64 / 500.0) * 0.5 + 0.5);

        foreach (var (tabId, rect) in _activeTabs)
        {
            bool blink = (blinkResearch && tabId == TabResearch) || (_hasNewEvent && tabId == TabEvents);
            DrawTab(canvas, rect, GetTabLabel(tabId), _activeTab == tabId, blink ? blinkT : -1f);
        }
    }

    private string GetTabLabel(int tabId) => tabId switch
    {
        TabIsland     => _localization.Get("tab_island"),
        TabResearch   => _localization.Get("tab_research"),
        TabPrestige   => _localization.Get("tab_prestige_map"),
        TabStats      => _localization.Get("tab_stats"),
        TabEvents     => _localization.Get("tab_events"),
        TabAutomation => _localization.Get("tab_automation"),
        _             => "?"
    };

    private void DrawTab(SKCanvas canvas, SKRect rect, string label, bool isActive, float blinkT = -1f)
    {
        SKPaint bgPaint;
        if (isActive)
        {
            bgPaint = _activeTabPaint;
        }
        else if (blinkT >= 0f)
        {
            const byte r0 = 35, g0 = 35, b0 = 45;
            const byte r1 = 160, g1 = 100, b1 = 10;
            _blinkTabPaint.Color = new SKColor(
                (byte)(r0 + (r1 - r0) * blinkT),
                (byte)(g0 + (g1 - g0) * blinkT),
                (byte)(b0 + (b1 - b0) * blinkT));
            bgPaint = _blinkTabPaint;
        }
        else
        {
            bgPaint = _inactiveTabPaint;
        }

        canvas.DrawRoundRect(rect, 5, 5, bgPaint);
        if (isActive)
            canvas.DrawRoundRect(rect, 5, 5, _activeTabBorderPaint);
        var textPaint = isActive ? _buttonTextPaint : _disabledTextPaint;
        canvas.DrawText(label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, _tabFont, textPaint);
    }

    public void ConnectWonderService(WonderSelectionService wonderSelectionService)
    {
        _wonderSelectionService = wonderSelectionService;
        _playerCivPanel.ConnectWonderSelectionService(wonderSelectionService);
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen
                                    || _settingsMenu.IsOpen || _settingsPopupRenderer.IsOpen;
    public bool IsPointBlockedByUI(SKPoint point) =>
        IsAnyOverlayOpen || _selectedCityPanelRenderer.ContainsPoint(point) || _selectedWonderPanelRenderer.ContainsPoint(point)
        || _playerCivPanel.ContainsPoint(point);
    public bool IsIslandTabActive => _activeTab == TabIsland;

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        // Drag horizontal des ressources (mode mobile)
        if (_isDraggingResources)
        {
            float delta = _resourceDragLastX - e.Position.X;
            _resourceDragLastX = e.Position.X;
            float visibleW = _canvasSize.Width - PlayerResourcesOverlayRenderer.Padding;
            float maxScroll = Math.Max(0f, _playerResourcesOverlayRenderer.TotalResourcesContentWidth - visibleW);
            _playerResourcesOverlayRenderer.ScrollOffset =
                Math.Clamp(_playerResourcesOverlayRenderer.ScrollOffset + delta, 0f, maxScroll);
            return;
        }

        if (_settingsPopupRenderer.IsOpen)
            _settingsPopupRenderer.HandlePointerMoved(e.Position);
        if (_tradeRenderer.IsOpen)
            _tradeRenderer.HandlePointerMoved(e.Position);
        if (_prestigeRenderer.IsOpen)
            _prestigeRenderer.HandlePointerMoved(e.Position);
        if (_activeTab == TabPrestige)
            _prestigeMapRenderer.HandlePointerMoved(e.Position);
        if (_activeTab == TabAutomation)
            _automationRenderer.HandlePointerMoved(e.Position);
        if (_activeTab == TabIsland)
            _playerCivPanel.HandlePointerMoved(e.Position);

        _lastPointerPosition = e.Position;
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;
        if (_suppressNextPress) { _suppressNextPress = false; return; }

        if (_settingsPopupRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (_prestigeRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (_tradeRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (e.Button != PointerButton.Left) return;

        bool isMobile = _uiLayout.IsMobile;

        // Gear : en mode mobile il est dans la 2e ligne, sinon dans la barre ressources
        var gearRect = isMobile ? _mobileGearRect : _playerResourcesOverlayRenderer.GearRect;
        if (gearRect != default && gearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
            return;
        }

        // Drag des ressources (mode mobile, zone barre du haut)
        if (isMobile && e.Position.Y < PlayerResourcesOverlayRenderer.BarHeight)
        {
            _isDraggingResources = true;
            _resourceDragLastX = e.Position.X;
            return;
        }

        // Map switch button
        if (_mapSwitchRect != default && _mapSwitchRect.Contains(e.Position.X, e.Position.Y))
        {
            var worldState = _gameControllerService.CurrentWorldState;
            if (worldState?.Layers.ContainsKey(LayerState.UnderworldZ) == true)
            {
                worldState.CurrentViewedLayer = worldState.CurrentViewedLayer == LayerState.UnderworldZ
                    ? IslandMap.SurfaceLayer
                    : LayerState.UnderworldZ;
                if (worldState.CurrentViewedLayer == LayerState.UnderworldZ)
                    _activeTab = TabIsland;
                DeselectCityAndWonder();
            }
            return;
        }

        // Tab clicks
        foreach (var (tabId, rect) in _activeTabs)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _activeTab = tabId;
                return;
            }
        }

        if (_activeTab == TabPrestige)
        {
            _prestigeMapRenderer.HandlePointerPressed(e.Position);
            return;
        }

        if (_activeTab == TabAutomation)
        {
            _automationRenderer.HandlePointerPressed(e.Position);
            return;
        }

        if (_activeTab == TabStats || _activeTab == TabResearch || _activeTab == TabEvents) return;

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
        _selectedCityPanelRenderer.IsInputEnabled = false;
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
        _selectedCityPanelRenderer.IsInputEnabled = true;
        _selectedWonderPanelRenderer.IsInputEnabled = true;
        if (suppressNextPress) _suppressNextPress = true;
    }

    public void SwitchToPrestigeTab()
    {
        _activeTab = TabPrestige;
    }

    private void HandlePointerReleased(object? sender, PointerEventArgs e)
    {
        _isDraggingResources = false;
        if (!_isVisible) return;
        if (_activeTab == TabPrestige)
            _prestigeMapRenderer.HandlePointerReleased(e.Position);
    }

    private void HandleZoomChanged(object? sender, ZoomEventArgs e)
    {
        if (!_isVisible) return;
        if (_activeTab == TabPrestige)
        {
            _prestigeMapRenderer.HandleZoom(e);
            return;
        }
        if (_activeTab == TabIsland)
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
        switch (e.Key)
        {
            case "I": _activeTab = TabIsland;     break;
            case "R": _activeTab = TabResearch;   break;
            case "P": _activeTab = TabPrestige;   break;
            case "S": _activeTab = TabStats;      break;
            case "E": _activeTab = TabEvents;     break;
            case "A": if (_hasAutomationTab) _activeTab = TabAutomation; break;
        }
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

        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerMoved -= HandlePointerMoved;
        _inputService.PointerReleased -= HandlePointerReleased;
        _inputService.ZoomChanged -= HandleZoomChanged;
        _inputService.KeyPressed -= HandleKeyInput;
        _inputService.KeyReleased -= HandleKeyRelease;
        _playerResourcesOverlayRenderer.Dispose();
        _selectedCityPanelRenderer.Dispose();
        _selectedWonderPanelRenderer.Dispose();
        _settingsMenu.Dispose();
        _tradeRenderer.Dispose();
        _prestigeRenderer.Dispose();
        _buttonTextPaint.Dispose();
        _disabledTextPaint.Dispose();
        _activeTabPaint.Dispose();
        _inactiveTabPaint.Dispose();
        _blinkTabPaint.Dispose();
        _activeTabBorderPaint.Dispose();
        _tabFont.Dispose();
        _prestigeMapRenderer.Dispose();
        _prestigeHistoryRenderer.Dispose();
        _timeControlRenderer.Dispose();
        _researchRenderer.Dispose();
        _eventLogRenderer.Dispose();
        _automationRenderer.Dispose();
        _playerCivPanel.Dispose();
        _mapSwitchActivePaint.Dispose();
        _mapSwitchBorderPaint.Dispose();
        _mapSwitchFont.Dispose();
        _secondRowBgPaint.Dispose();
        _secondRowBorderPaint.Dispose();
        _disposed = true;
    }
}
