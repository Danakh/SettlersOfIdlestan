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

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public sealed class OverlayRenderer : IGameRenderer
{
    private const float TradeButtonWidth = 120;
    private const float PrestigeButtonWidth = 160;
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
    private readonly TradeRenderer _tradeRenderer;
    private readonly PrestigeRenderer _prestigeRenderer;
    private readonly PrestigeMapRenderer _prestigeMapRenderer;
    private readonly PrestigeHistoryRenderer _prestigeHistoryRenderer;
    private readonly TimeControlRenderer _timeControlRenderer;
    private readonly ResearchRenderer _researchRenderer;
    private readonly EventLogRenderer _eventLogRenderer;
    private readonly AutomationRenderer _automationRenderer;

    private readonly SKPaint _buttonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledButtonPaint = new() { Color = new SKColor(90, 90, 96), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _disabledTextPaint = new() { Color = new SKColor(180, 180, 185), IsAntialias = true };
    private readonly SKFont _buttonFont = new() { Size = 14, Typeface = SkiaFonts.Bold };

    private readonly SKPaint _activeTabPaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactiveTabPaint = new() { Color = new SKColor(35, 35, 45), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _activeTabBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKFont _tabFont = new() { Size = 12, Typeface = SkiaFonts.Bold };

    private SKSize _canvasSize;
    private SKRect _tradeButtonRect = SKRect.Empty;
    private SKRect _prestigeButtonRect = SKRect.Empty;

    // Dynamic tab list: (tabId, screenRect) computed each frame
    private readonly List<(int tabId, SKRect rect)> _activeTabs = new();

    private int _activeTab = TabIsland;
    private bool _hasResearchTab;
    private bool _hasAutomationTab;
    private bool _disposed;
    private bool _isVisible = true;

    public OverlayRenderer(
        InputHandlingService inputService,
        GameControllerService gameControllerService,
        ILocalizationService localization,
        PlayerResourcesOverlayRenderer playerResourcesOverlayRenderer,
        SettingsMenu settingsMenu,
        SettingsPopupRenderer settingsPopupRenderer,
        SelectedCityPanelRenderer selectedCityPanelRenderer,
        TradeRenderer tradeRenderer,
        PrestigeRenderer prestigeRenderer,
        PrestigeMapRenderer prestigeMapRenderer,
        PrestigeHistoryRenderer prestigeHistoryRenderer,
        TimeControlRenderer timeControlRenderer,
        ResearchRenderer researchRenderer,
        EventLogRenderer eventLogRenderer,
        AutomationRenderer automationRenderer)
    {
        _inputService = inputService;
        _gameControllerService = gameControllerService;
        _localization = localization;
        _playerResourcesOverlayRenderer = playerResourcesOverlayRenderer;
        _settingsMenu = settingsMenu;
        _settingsPopupRenderer = settingsPopupRenderer;
        _selectedCityPanelRenderer = selectedCityPanelRenderer;
        _tradeRenderer = tradeRenderer;
        _prestigeRenderer = prestigeRenderer;
        _prestigeMapRenderer = prestigeMapRenderer;
        _prestigeHistoryRenderer = prestigeHistoryRenderer;
        _timeControlRenderer = timeControlRenderer;
        _researchRenderer = researchRenderer;
        _eventLogRenderer = eventLogRenderer;
        _automationRenderer = automationRenderer;
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.KeyPressed += HandleKeyInput;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _playerResourcesOverlayRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.Initialize(canvasSize);
        _selectedCityPanelRenderer.ReservedBottomHeight = CityPanelReservedBottomHeight;
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

        _selectedCityPanelRenderer.IsInputEnabled = !onResearchTab && !onPrestigeTab && !onHistoryTab && !onEventsTab && !onAutomationTab
            && !_tradeRenderer.IsOpen && !_prestigeRenderer.IsOpen;
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
        return (mainGameState.PrestigeState?.PrestigePoints ?? 0) > 0;
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

    private void DrawTabButtons(SKCanvas canvas)
    {
        foreach (var (tabId, rect) in _activeTabs)
            DrawTab(canvas, rect, GetTabLabel(tabId), _activeTab == tabId);
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

    private void DrawTab(SKCanvas canvas, SKRect rect, string label, bool isActive)
    {
        canvas.DrawRoundRect(rect, 5, 5, isActive ? _activeTabPaint : _inactiveTabPaint);
        if (isActive)
            canvas.DrawRoundRect(rect, 5, 5, _activeTabBorderPaint);
        var textPaint = isActive ? _buttonTextPaint : _disabledTextPaint;
        canvas.DrawText(label, rect.MidX, rect.MidY + 5, SKTextAlign.Center, _tabFont, textPaint);
    }

    private void DrawActionButtons(SKCanvas canvas, GameRenderContext context)
    {
        _tradeButtonRect = SKRect.Empty;
        _prestigeButtonRect = SKRect.Empty;

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
        }
    }

    private bool IsTradeAvailable(GameRenderContext? context = null)
    {
        if (_gameControllerService.PlayerCivilization == null) return false;
        try { return _gameControllerService.MainGameController.TradeController.IsTradeAvailable(_gameControllerService.PlayerCivilization.Index); }
        catch { return false; }
    }

    public bool IsAnyOverlayOpen => _tradeRenderer.IsOpen || _prestigeRenderer.IsOpen
                                    || _settingsMenu.IsOpen || _settingsPopupRenderer.IsOpen;
    public bool IsPointBlockedByUI(SKPoint point) =>
        IsAnyOverlayOpen || _selectedCityPanelRenderer.ContainsPoint(point);
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
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        if (_settingsPopupRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (_prestigeRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (_tradeRenderer.HandlePointerPressed(e.Position, e.Button)) return;
        if (e.Button != PointerButton.Left) return;

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

        if (_playerResourcesOverlayRenderer.GearRect.Contains(e.Position.X, e.Position.Y))
        {
            _settingsMenu.HandleGearClick();
            return;
        }

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
    }

    public void CloseAll()
    {
        _settingsMenu.Close();
        _settingsPopupRenderer.Close();
        _tradeRenderer.Close();
        _prestigeRenderer.Close();
        _selectedCityPanelRenderer.Close();
        _selectedCityPanelRenderer.IsInputEnabled = false;
    }

    public void Hide()
    {
        CloseAll();
        _isVisible = false;
    }

    public void Show()
    {
        _isVisible = true;
        _selectedCityPanelRenderer.IsInputEnabled = true;
    }

    public void SwitchToPrestigeTab()
    {
        _activeTab = TabPrestige;
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
        _inputService.KeyPressed -= HandleKeyInput;
        _playerResourcesOverlayRenderer.Dispose();
        _selectedCityPanelRenderer.Dispose();
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
