using System;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
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
    private const float TradeButtonWidth = 120;
    private const float PrestigeButtonWidth = 160;
    private const float WonderButtonWidth = 140;
    private const float TradeButtonHeight = 38;
    private const float TradeButtonMargin = 14;
    private const float ButtonSpacing = 10;
    private const float CityPanelReservedBottomHeight = TradeButtonHeight + TradeButtonMargin * 2;

    private const float TabWidth = 62;
    private const float TabHeight = 28;
    private const float TabMarginLeft = 8;
    private const float TabSpacing = 5;

    // Logical tab IDs (stable, independent of visual position)
    private const int TabIsland     = 0;
    private const int TabResearch   = 1;
    private const int TabPrestige   = 2;
    private const int TabStats      = 3;
    private const int TabEvents     = 4;
    private const int TabAutomation = 5;

    private readonly InputHandlingService _inputService;
    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
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

    private readonly SKPaint _buttonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledButtonPaint = new() { Color = new SKColor(90, 90, 96), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _disabledTextPaint = new() { Color = new SKColor(180, 180, 185), IsAntialias = true };
    private readonly SKFont _buttonFont = new() { Size = 14, Typeface = SkiaFonts.Bold };

    private readonly SKPaint _activeTabPaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactiveTabPaint = new() { Color = new SKColor(35, 35, 45), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _blinkTabPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _activeTabBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKFont _tabFont = new() { Size = 12, Typeface = SkiaFonts.Bold };

    private SKSize _canvasSize;
    private SKRect _tradeButtonRect = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _wonderButtonRect = SKRect.Empty;
    private SKPoint _lastPointerPosition;
    private WonderSelectionService? _wonderSelectionService;

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
        ILocalizationService localization,
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
        TooltipRenderer tooltipRenderer)
    {
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
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.PointerReleased += HandlePointerReleased;
        _inputService.ZoomChanged += HandleZoomChanged;
        _inputService.KeyPressed += HandleKeyInput;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _playerResourcesOverlayRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.ReservedBottomHeight = CityPanelReservedBottomHeight;
        _selectedWonderPanelRenderer.Initialize(canvasSize);
        _tradeRenderer.Initialize(canvasSize);
        _prestigeRenderer.Initialize(canvasSize);
        _settingsPopupRenderer.Initialize(canvasSize);
        _prestigeMapRenderer.Initialize(canvasSize);
        _prestigeHistoryRenderer.Initialize(canvasSize);
        _researchRenderer.Initialize(canvasSize);
        _eventLogRenderer.Initialize(canvasSize);
        _automationRenderer.Initialize(canvasSize);

        float gearX = canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;
        float timeControlRight = gearX - 8f;
        _timeControlRenderer.Initialize(canvasSize, timeControlRight);
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

        if (_activeTabs.Count > 1)
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
        else
        {
            _playerResourcesOverlayRenderer.ResourceStartX = PlayerResourcesOverlayRenderer.Padding;
        }

        _playerResourcesOverlayRenderer.Mode = _activeTab switch
        {
            TabPrestige  => BarDisplayMode.Prestige,
            TabResearch  => BarDisplayMode.Research,
            _ => BarDisplayMode.Island,
        };

        bool onResearchTab    = _activeTab == TabResearch    && _hasResearchTab;
        bool onPrestigeTab    = _activeTab == TabPrestige;
        bool onHistoryTab     = _activeTab == TabStats;
        bool onEventsTab      = _activeTab == TabEvents;
        bool onAutomationTab  = _activeTab == TabAutomation  && _hasAutomationTab;

        int currentEventCount = _gameControllerService.CurrentIslandState?.EventLog?.Entries.Count ?? 0;
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

        bool panelsEnabled = !onResearchTab && !onPrestigeTab && !onHistoryTab && !onEventsTab && !onAutomationTab
            && !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
        _selectedCityPanelRenderer.IsInputEnabled = panelsEnabled;
        _selectedWonderPanelRenderer.IsInputEnabled = panelsEnabled;
        _researchRenderer.IsActive = onResearchTab;

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
            _selectedCityPanelRenderer.Render(canvas, context);
            _selectedWonderPanelRenderer.Render(canvas, context);
            DrawActionButtons(canvas, context);
        }

        float gearX = _canvasSize.Width - PlayerResourcesOverlayRenderer.Padding - PlayerResourcesOverlayRenderer.IconSize;
        _timeControlRenderer.Render(canvas, context);
        _settingsMenu.Draw(canvas, gearX, PlayerResourcesOverlayRenderer.BarHeight);

        _tradeRenderer.Render(canvas);
        _prestigeRenderer.Render(canvas);
        _settingsPopupRenderer.Render(canvas);
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
            return false;
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

    private void DrawActionButtons(SKCanvas canvas, GameRenderContext context)
    {
        _tradeButtonRect = SKRect.Empty;
        _prestigeButtonRect = SKRect.Empty;
        _wonderButtonRect = SKRect.Empty;

        bool isTradeVisible = IsTradeAvailable();
        var prestigeController = _gameControllerService.MainGameController.PrestigeController;
        bool isPrestigeVisible = prestigeController.PrestigeIsVisible();

        float right = _canvasSize.Width - TradeButtonMargin;

        if (isTradeVisible)
        {
            _tradeButtonRect = new SKRect(
                right - TradeButtonWidth,
                _canvasSize.Height - TradeButtonMargin - TradeButtonHeight,
                right,
                _canvasSize.Height - TradeButtonMargin);
            canvas.DrawRoundRect(_tradeButtonRect, 7, 7, _buttonPaint);
            canvas.DrawText(_localization.Get("trade_action"), _tradeButtonRect.MidX, _tradeButtonRect.MidY + 6, SKTextAlign.Center, _buttonFont, _buttonTextPaint);
            right = _tradeButtonRect.Left - ButtonSpacing;
        }

        if (isPrestigeVisible)
        {
            _prestigeButtonRect = new SKRect(
                right - PrestigeButtonWidth,
                _canvasSize.Height - TradeButtonMargin - TradeButtonHeight,
                right,
                _canvasSize.Height - TradeButtonMargin);

            bool isAvailable = prestigeController.PrestigeIsAvailable();
            int currentPoints = prestigeController.CalculatePrestigePoints();
            string label = isAvailable
                ? $"{_localization.Get("prestige_action")} ({currentPoints})"
                : $"{_localization.Get("prestige_action")} ({currentPoints}/{PrestigeController.PrestigeRequiredPoints})";
            canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, isAvailable ? _buttonPaint : _disabledButtonPaint);
            canvas.DrawText(label, _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 6, SKTextAlign.Center, _buttonFont, isAvailable ? _buttonTextPaint : _disabledTextPaint);

            if (!prestigeController.HasImperialPort() && _prestigeButtonRect.Contains(_lastPointerPosition.X, _lastPointerPosition.Y))
            {
                _tooltipRenderer.SetTooltipLines(new[]
                {
                    _localization.Get("prestige_requires_imperial_port"),
                    _localization.Get("tooltip_imperial_port_prerequisites"),
                }, _lastPointerPosition);
            }

            right = _prestigeButtonRect.Left - ButtonSpacing;
        }

        if (IsWonderButtonVisible())
        {
            bool canPlace = CanPlaceWonder();
            _wonderButtonRect = new SKRect(
                right - WonderButtonWidth,
                _canvasSize.Height - TradeButtonMargin - TradeButtonHeight,
                right,
                _canvasSize.Height - TradeButtonMargin);

            canvas.DrawRoundRect(_wonderButtonRect, 7, 7, canPlace ? _buttonPaint : _disabledButtonPaint);
            canvas.DrawText(_localization.Get("wonder_action"), _wonderButtonRect.MidX, _wonderButtonRect.MidY + 6, SKTextAlign.Center, _buttonFont, canPlace ? _buttonTextPaint : _disabledTextPaint);

            if (!canPlace && _wonderButtonRect.Contains(_lastPointerPosition.X, _lastPointerPosition.Y))
            {
                string reason = WonderAlreadyExists()
                    ? _localization.Get("wonder_already_placed")
                    : _localization.Get("wonder_requires_architecture");
                _tooltipRenderer.SetTooltipLines(new[] { reason }, _lastPointerPosition);
            }
        }
    }

    private bool IsWonderButtonVisible()
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

    private bool WonderAlreadyExists()
    {
        var islandState = _gameControllerService.CurrentIslandState;
        if (islandState == null) return false;
        return islandState.Features.OfType<SettlersOfIdlestan.Model.IslandFeatures.Wonder>().Any();
    }

    private bool IsTradeAvailable(GameRenderContext? context = null)
    {
        if (_gameControllerService.PlayerCivilization == null) return false;
        try { return _gameControllerService.MainGameController.TradeController.IsTradeAvailable(_gameControllerService.PlayerCivilization.Index); }
        catch { return false; }
    }

    public void ConnectWonderService(WonderSelectionService wonderSelectionService)
    {
        _wonderSelectionService = wonderSelectionService;
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen
                                    || _settingsMenu.IsOpen || _settingsPopupRenderer.IsOpen;
    public bool IsPointBlockedByUI(SKPoint point) =>
        IsAnyOverlayOpen || _selectedCityPanelRenderer.ContainsPoint(point) || _selectedWonderPanelRenderer.ContainsPoint(point);
    public bool IsIslandTabActive => _activeTab == TabIsland;

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;
        if (_tradeRenderer.IsOpen)
            _tradeRenderer.HandlePointerMoved(e.Position);
        if (_activeTab == TabPrestige)
            _prestigeMapRenderer.HandlePointerMoved(e.Position);
        if (_activeTab == TabAutomation)
            _automationRenderer.HandlePointerMoved(e.Position);

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

        if (_playerResourcesOverlayRenderer.GearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
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

        if (_tradeButtonRect.Contains(e.Position.X, e.Position.Y) && IsTradeAvailable())
        {
            _settingsMenu.Close();
            _settingsPopupRenderer.Close();
            _prestigeRenderer.Close();
            _tradeRenderer.Open();
        }

        if (!_prestigeButtonRect.IsEmpty && _prestigeButtonRect.Contains(e.Position.X, e.Position.Y) && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _settingsMenu.Close();
            _settingsPopupRenderer.Close();
            _tradeRenderer.Close();
            _prestigeRenderer.Open();
        }

        if (!_wonderButtonRect.IsEmpty && _wonderButtonRect.Contains(e.Position.X, e.Position.Y) && CanPlaceWonder() && _wonderSelectionService != null)
        {
            CloseAll();
            var hexes = _gameControllerService.MainGameController.WonderController.GetPlaceableHexes();
            _wonderSelectionService.Enter(hexes);
        }
    }

    public void CloseAll()
    {
        _settingsMenu.Close();
        _settingsPopupRenderer.Close();
        _tradeRenderer.Close();
        _prestigeRenderer.Close();
        _selectedCityPanelRenderer.Close();
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
        if (!_isVisible) return;
        if (_activeTab == TabPrestige)
            _prestigeMapRenderer.HandlePointerReleased(e.Position);
    }

    private void HandleZoomChanged(object? sender, ZoomEventArgs e)
    {
        if (!_isVisible) return;
        if (_activeTab == TabPrestige)
            _prestigeMapRenderer.HandleZoom(e);
    }

    private void HandleKeyInput(object? sender, KeyEventArgs e)
    {
        if (!_isVisible) return;
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

    public void Dispose()
    {
        if (_disposed) return;

        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerMoved -= HandlePointerMoved;
        _inputService.PointerReleased -= HandlePointerReleased;
        _inputService.ZoomChanged -= HandleZoomChanged;
        _inputService.KeyPressed -= HandleKeyInput;
        _playerResourcesOverlayRenderer.Dispose();
        _selectedCityPanelRenderer.Dispose();
        _selectedWonderPanelRenderer.Dispose();
        _settingsMenu.Dispose();
        _tradeRenderer.Dispose();
        _prestigeRenderer.Dispose();
        _buttonPaint.Dispose();
        _disabledButtonPaint.Dispose();
        _buttonTextPaint.Dispose();
        _disabledTextPaint.Dispose();
        _buttonFont.Dispose();
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
        _disposed = true;
    }
}
