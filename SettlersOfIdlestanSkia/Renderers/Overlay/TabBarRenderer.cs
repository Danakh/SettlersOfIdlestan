using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public sealed class TabBarRenderer : IDisposable
{
    public const int TabIsland     = 0;
    public const int TabResearch   = 1;
    public const int TabPrestige   = 2;
    public const int TabStats      = 3;
    public const int TabEvents     = 4;
    public const int TabAutomation = 5;
    public const int TabRituals    = 6;
    public const int TabAscension  = 7;

    private const float TabWidth      = 62;
    private const float TabHeight     = 28;
    private const float TabMarginLeft = 8;
    private const float TabSpacing    = 5;
    private const float MobileTabHeight = UILayoutService.MobileTabBarHeight;

    private readonly LocalizationService _localization;
    private readonly GameControllerService _gameControllerService;
    private readonly UILayoutService _uiLayout;
    private readonly bool _allowDebugMode;

    private readonly SKPaint _buttonTextPaint      = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _disabledTextPaint    = new() { Color = new SKColor(180, 180, 185), IsAntialias = true };
    private readonly SKPaint _activeTabPaint       = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactiveTabPaint     = new() { Color = new SKColor(35, 35, 45), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _blinkTabPaint        = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _activeTabBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private SKFont _tabFont = new() { Size = 12, Typeface = SkiaFonts.Bold };

    private readonly List<(int tabId, SKRect rect)> _activeTabs = new();
    private int _activeTab = TabIsland;
    private bool _hasResearchTab;
    private bool _hasAutomationTab;
    private bool _hasRitualsTab;
    private bool _hasAscensionTab;
    private bool _hasNewEvent;
    private int? _seenEventCount;
    private bool _prestigeGlowing;
    private bool _lastCanBuyPrestige;
    private bool _researchGlowing;
    private bool _lastResearchIdleWithPoints;
    private SKSize _canvasSize;

    /// X offset from which the resource bar content should start (after the tabs).
    public float ResourceStartX { get; private set; }

    /// True when the tab bar has at least two tabs and is being drawn.
    public bool IsVisible { get; private set; }

    public int ActiveTab => _activeTab;

    public IReadOnlyList<(int tabId, SKRect rect)> ActiveTabs => _activeTabs;

    public TabBarRenderer(
        LocalizationService localization,
        GameControllerService gameControllerService,
        UILayoutService uiLayout,
        bool allowDebugMode = false)
    {
        _localization = localization;
        _gameControllerService = gameControllerService;
        _uiLayout = uiLayout;
        _allowDebugMode = allowDebugMode;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        float scale = _uiLayout.UiScale;
        _tabFont.Dispose();
        _tabFont = new SKFont { Size = 12 * scale, Typeface = SkiaFonts.Bold };
    }

    /// Update visibility/rect state. Must be called before <see cref="Render"/> each frame.
    /// Sets <see cref="ResourceStartX"/> so callers can position the resource bar before drawing tabs on top.
    public void Update(GameRenderContext context)
    {
        bool showPrestigeTabs = HasPrestigePoints(context);
        _hasResearchTab   = IsResearchUnlocked();
        _hasAutomationTab = HasAnyAutomation();
        _hasRitualsTab    = IsMagicUnlocked();
        _hasAscensionTab  = HasGodPoints(context);
        bool showEventsTab = showPrestigeTabs || HasEventLogEntries();

        if (!_hasResearchTab   && _activeTab == TabResearch)   _activeTab = TabIsland;
        if (!showPrestigeTabs  && _activeTab is TabPrestige or TabStats) _activeTab = TabIsland;
        if (!showEventsTab     && _activeTab == TabEvents)     _activeTab = TabIsland;
        if (!_hasAutomationTab && _activeTab == TabAutomation) _activeTab = TabIsland;
        if (!_hasRitualsTab    && _activeTab == TabRituals)    _activeTab = TabIsland;
        if (!_hasAscensionTab  && _activeTab == TabAscension)  _activeTab = TabIsland;

        _activeTabs.Clear();
        _activeTabs.Add((TabIsland, default));
        if (_hasResearchTab)   _activeTabs.Add((TabResearch, default));
        if (_hasRitualsTab)    _activeTabs.Add((TabRituals, default));
        if (showPrestigeTabs)  { _activeTabs.Add((TabPrestige, default)); _activeTabs.Add((TabStats, default)); }
        if (showEventsTab)     _activeTabs.Add((TabEvents, default));
        if (_hasAutomationTab) _activeTabs.Add((TabAutomation, default));
        if (_hasAscensionTab)  _activeTabs.Add((TabAscension, default));

        bool isMobile   = _uiLayout.IsMobile;
        float uiScale   = _uiLayout.UiScale;
        bool showTabBar = isMobile || _activeTabs.Count > 1;
        IsVisible = showTabBar;

        if (showTabBar)
        {
            ComputeTabRects(isMobile, uiScale);
            UpdateEventNotification();
            UpdatePrestigeNotification();
            UpdateResearchNotification();
        }
        else
        {
            ResourceStartX = UILayoutService.BarPadding * uiScale;
        }
    }

    /// Draw the tab buttons. Call after the resource bar has been rendered so tabs appear on top.
    public void Render(SKCanvas canvas)
    {
        if (!IsVisible) return;
        DrawTabButtons(canvas);
    }

    private void ComputeTabRects(bool isMobile, float uiScale)
    {
        if (isMobile)
        {
            float mobileTabH = MobileTabHeight * uiScale;
            float tabY = _canvasSize.Height - mobileTabH - 2;
            float tabW = _canvasSize.Width / _activeTabs.Count;
            for (int i = 0; i < _activeTabs.Count; i++)
            {
                float x = i * tabW;
                _activeTabs[i] = (_activeTabs[i].tabId, new SKRect(x, tabY, x + tabW, tabY + mobileTabH));
            }
            ResourceStartX = UILayoutService.BarPadding * uiScale;
        }
        else
        {
            float scaledTabH = TabHeight * uiScale;
            float scaledTabW = TabWidth  * uiScale;
            float tabY = (_uiLayout.ResourceBarBottom - scaledTabH) / 2;
            float tabX = TabMarginLeft * uiScale;
            for (int i = 0; i < _activeTabs.Count; i++)
            {
                _activeTabs[i] = (_activeTabs[i].tabId, new SKRect(tabX, tabY, tabX + scaledTabW, tabY + scaledTabH));
                tabX += scaledTabW + TabSpacing * uiScale;
            }
            ResourceStartX = tabX + TabMarginLeft * uiScale;
        }
    }

    private void UpdateEventNotification()
    {
        int currentEventCount = _gameControllerService.CurrentWorldState?.EventLog?.Entries.Count ?? 0;
        if (_seenEventCount == null || _seenEventCount > currentEventCount)
        {
            _seenEventCount = currentEventCount;
            _hasNewEvent = false;
        }
        else if (_activeTab == TabEvents)
        {
            _seenEventCount = currentEventCount;
            _hasNewEvent = false;
        }
        else if (currentEventCount > _seenEventCount)
        {
            _hasNewEvent = true;
        }
    }

    private void DrawTabButtons(SKCanvas canvas)
    {
        float blinkT = (float)(Math.Sin(Environment.TickCount64 / 500.0) * 0.5 + 0.5);

        foreach (var (tabId, rect) in _activeTabs)
        {
            bool blink = (_prestigeGlowing && tabId == TabPrestige)
                      || (_researchGlowing  && tabId == TabResearch)
                      || (_hasNewEvent      && tabId == TabEvents);
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
        TabRituals    => _localization.Get("tab_rituals"),
        TabAscension  => _localization.Get("tab_ascension"),
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

        float cr = 5 * _uiLayout.UiScale;
        canvas.DrawRoundRect(rect, cr, cr, bgPaint);
        if (isActive)
            canvas.DrawRoundRect(rect, cr, cr, _activeTabBorderPaint);
        var textPaint = isActive ? _buttonTextPaint : _disabledTextPaint;
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 5 * _uiLayout.UiScale, SKTextAlign.Center, _tabFont, textPaint);
    }

    /// Returns true if the click was on a tab (and consumed).
    public bool HandlePointerPressed(SKPoint point)
    {
        foreach (var (tabId, rect) in _activeTabs)
        {
            if (rect.Contains(point.X, point.Y))
            {
                _activeTab = tabId;
                return true;
            }
        }
        return false;
    }

    /// Returns true if the key was a tab shortcut (and consumed).
    public bool HandleKeyInput(string key)
    {
        switch (key)
        {
            case "I": _activeTab = TabIsland;   return true;
            case "R": _activeTab = TabResearch; return true;
            case "P": _activeTab = TabPrestige; return true;
            case "S": _activeTab = TabStats;    return true;
            case "E": _activeTab = TabEvents;   return true;
            case "A":
                if (_hasAutomationTab) { _activeTab = TabAutomation; return true; }
                break;
            case "M":
                if (_hasRitualsTab) { _activeTab = TabRituals; return true; }
                break;
        }
        return false;
    }

    public void SetActiveTab(int tabId) => _activeTab = tabId;

    private bool HasPrestigePoints(GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return false;
        return (mgs.PrestigeState?.TotalPrestigePointsEarned ?? 0) > 0;
    }

    private bool HasGodPoints(GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return false;
        return mgs.GodState.TotalGodPointsEarned > 0;
    }

    private bool HasEventLogEntries()
    {
        try { return _gameControllerService.CurrentWorldState?.EventLog?.HasEntries == true; }
        catch { return false; }
    }

    private bool IsResearchUnlocked()
    {
        try { return _gameControllerService.MainGameController.ResearchController.IsResearchUnlocked(); }
        catch { return false; }
    }

    private bool IsMagicUnlocked()
    {
        try { return _gameControllerService.MainGameController.MagicController.IsMagicUnlocked(); }
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
                    if (b.Level > 0 && (b.ProvidesAutomation || b.ActivationStatus != ActivationStatus.NON_ACTIVABLE)) return true;
            var completed = civ.TechnologyTree.CompletedTechnologies;
            return completed.Contains(TechnologyId.AdvancedTactics) || completed.Contains(TechnologyId.AdvancedStrategy);
        }
        catch { return false; }
    }

    private void UpdatePrestigeNotification()
    {
        bool canBuyNow = CanBuyAnyPrestigeVertex();
        if (_activeTab == TabPrestige)
        {
            _prestigeGlowing = false;
            _lastCanBuyPrestige = canBuyNow;
        }
        else if (canBuyNow && !_lastCanBuyPrestige)
        {
            _prestigeGlowing = true;
            _lastCanBuyPrestige = true;
        }
        else if (!canBuyNow)
        {
            _lastCanBuyPrestige = false;
            _prestigeGlowing = false;
        }
    }

    private bool CanBuyAnyPrestigeVertex()
    {
        try
        {
            var prestigeState = _gameControllerService.CurrentGameState?.PrestigeState;
            if (prestigeState == null) return false;
            var controller = _gameControllerService.MainGameController.PrestigeMapController;
            return PrestigeMapController.DefaultMap.Vertices.Any(v => controller.CanPurchaseVertex(prestigeState, v.Coord));
        }
        catch { return false; }
    }

    private void UpdateResearchNotification()
    {
        bool idleWithPoints = IsResearchIdleWithPoints();
        if (_activeTab == TabResearch)
        {
            _researchGlowing = false;
            _lastResearchIdleWithPoints = idleWithPoints;
        }
        else if (idleWithPoints && !_lastResearchIdleWithPoints)
        {
            _researchGlowing = true;
            _lastResearchIdleWithPoints = true;
        }
        else if (!idleWithPoints)
        {
            _lastResearchIdleWithPoints = false;
            _researchGlowing = false;
        }
    }

    private bool IsResearchIdleWithPoints()
    {
        if (!_hasResearchTab) return false;
        try
        {
            var rc = _gameControllerService.MainGameController.ResearchController;
            return rc.ActiveResearch == null && rc.ResearchPoints > 0;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        _buttonTextPaint.Dispose();
        _disabledTextPaint.Dispose();
        _activeTabPaint.Dispose();
        _inactiveTabPaint.Dispose();
        _blinkTabPaint.Dispose();
        _activeTabBorderPaint.Dispose();
        _tabFont.Dispose();
    }
}
