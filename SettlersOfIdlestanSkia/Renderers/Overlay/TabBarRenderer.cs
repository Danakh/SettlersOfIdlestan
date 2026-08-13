using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
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
    public const int TabHistory    = 8;
    public const int TabUnderworld = 9;
    public const int TabAbyss      = 10;

    private const float TabWidth      = 62;
    private const float TabHeight     = 28;
    private const float TabMarginLeft = 8;
    private const float TabSpacing    = 5;
    private const float MobileTabHeight = UILayoutService.MobileTabBarHeight;

    private readonly LocalizationService _localization;
    private readonly GameControllerService _gameControllerService;
    private readonly UILayoutService _uiLayout;
    private readonly bool _allowDebugMode;

    private readonly List<(int tabId, SKRect rect)> _activeTabs = new();
    private int _activeTab = TabIsland;
    private bool _hasResearchTab;
    private bool _hasAutomationTab;
    private bool _hasRitualsTab;
    private bool _hasAscensionTab;
    private bool _hasUnderworldTab;
    private bool _hasAbyssTab;
    private bool _hasNewEvent;
    private int? _seenEventCount;
    private bool _prestigeGlowing;
    private bool _lastCanBuyPrestige;
    private bool _researchGlowing;
    private bool _lastResearchIdleWithPoints;
    private bool _underworldGlowing;
    private SKSize _canvasSize;

    /// X offset from which the resource bar content should start (after the tabs).
    public float ResourceStartX { get; private set; }

    /// True when the tab bar has at least two tabs and is being drawn.
    public bool IsVisible { get; private set; }

    public int ActiveTab => _activeTab;

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
    }

    /// Update visibility/rect state.
    /// Sets <see cref="ResourceStartX"/> so callers can position the resource bar.
    public void Update(GameRenderContext context)
    {
        bool showPrestigeTabs = HasPrestigePoints(context);
        _hasResearchTab   = IsResearchUnlocked();
        _hasAutomationTab = HasAnyAutomation();
        _hasRitualsTab    = IsMagicUnlocked();
        _hasAscensionTab  = HasGodPoints(context);
        _hasUnderworldTab = IsLayerAccessible(LayerState.UnderworldZ);
        _underworldGlowing = _hasUnderworldTab && !(_gameControllerService.CurrentWorldState?.HasVisitedUnderworld ?? true);
        _hasAbyssTab      = IsLayerAccessible(LayerState.AbyssZ);
        bool showEventsTab = showPrestigeTabs || HasEventLogEntries();

        if (!_hasResearchTab   && _activeTab == TabResearch)   _activeTab = TabIsland;
        if (!showPrestigeTabs  && _activeTab is TabPrestige or TabStats) _activeTab = TabIsland;
        if (!showEventsTab     && _activeTab == TabEvents)     _activeTab = TabIsland;
        if (!_hasAutomationTab && _activeTab == TabAutomation) _activeTab = TabIsland;
        if (!_hasRitualsTab    && _activeTab == TabRituals)    _activeTab = TabIsland;
        if (!_hasAscensionTab  && _activeTab == TabAscension)  _activeTab = TabIsland;
        if (!_allowDebugMode   && _activeTab == TabHistory)    _activeTab = TabIsland;
        if (!_hasUnderworldTab && _activeTab == TabUnderworld) _activeTab = TabIsland;
        if (!_hasAbyssTab      && _activeTab == TabAbyss)      _activeTab = TabIsland;

        _activeTabs.Clear();
        _activeTabs.Add((TabIsland, default));
        if (_hasUnderworldTab) _activeTabs.Add((TabUnderworld, default));
        if (_hasAbyssTab)      _activeTabs.Add((TabAbyss, default));
        if (_hasResearchTab)   _activeTabs.Add((TabResearch, default));
        if (_hasRitualsTab)    _activeTabs.Add((TabRituals, default));
        if (showPrestigeTabs)  _activeTabs.Add((TabPrestige, default));
        if (_hasAscensionTab)  _activeTabs.Add((TabAscension, default));
        if (showPrestigeTabs)  _activeTabs.Add((TabStats, default));
        if (showEventsTab)     _activeTabs.Add((TabEvents, default));
        if (_hasAutomationTab) _activeTabs.Add((TabAutomation, default));
        if (_allowDebugMode)   _activeTabs.Add((TabHistory, default));

        float uiScale = _uiLayout.UiScale;

        bool tabsAtBottom = _uiLayout.TabsAtBottom;
        bool showTabBar   = tabsAtBottom || _activeTabs.Count > 1;
        IsVisible = showTabBar;

        if (showTabBar)
        {
            ComputeTabRects(tabsAtBottom, uiScale);
            UpdateEventNotification();
            UpdatePrestigeNotification();
            UpdateResearchNotification();
        }
        else
        {
            ResourceStartX = UILayoutService.BarPadding * uiScale;
        }
    }

    /// Largeur qu'occuperaient les tabs si affichés en ligne (indépendant du mode bas/inline choisi ensuite).
    private float ComputeInlineWidth(float uiScale) =>
        TabMarginLeft * uiScale * 2f
        + _activeTabs.Count * TabWidth * uiScale
        + Math.Max(0, _activeTabs.Count - 1) * TabSpacing * uiScale;

    private void ComputeTabRects(bool tabsAtBottom, float uiScale)
    {
        if (tabsAtBottom)
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
        TabHistory    => _localization.Get("tab_history"),
        TabUnderworld => _localization.Get("tab_underworld"),
        TabAbyss      => _localization.Get("tab_abyss"),
        _             => "?"
    };

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

    /// <summary>
    /// Instantané pour une barre d'onglets portée par l'hôte. Les règles de déblocage, de
    /// notification et de repli restent ici : seul le dessin passe côté Avalonia.
    /// </summary>
    public TabBarSnapshot GetSnapshot()
    {
        var tabs = new List<TabSnapshot>(_activeTabs.Count);
        foreach (var (tabId, _) in _activeTabs)
        {
            bool glowing = (_prestigeGlowing  && tabId == TabPrestige)
                        || (_researchGlowing  && tabId == TabResearch)
                        || (_hasNewEvent      && tabId == TabEvents)
                        || (_underworldGlowing && tabId == TabUnderworld);

            tabs.Add(new TabSnapshot(tabId, GetTabLabel(tabId), tabId == _activeTab, glowing));
        }

        return new TabBarSnapshot(IsVisible, _uiLayout.TabsAtBottom, tabs);
    }

    private bool HasPrestigePoints(GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return false;
        return (mgs.PrestigeState?.TotalPrestigePointsEarned ?? 0) > 0;
    }

    private bool HasGodPoints(GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return false;
        // Chaque Purification d'Os Divins octroie directement 1 essence divine (voir
        // DivineBonesController) : l'onglet apparaît dès qu'on en détient une, ou dès la première
        // Ascension (points divins gagnés), même si l'essence courante est retombée à zéro depuis.
        return mgs.GodState.DivineEssence > 0 || mgs.GodState.TotalGodPointsEarned > 0;
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

    private bool IsLayerAccessible(int z)
    {
        try
        {
            var worldState = _gameControllerService.CurrentWorldState;
            var map = worldState?.GetMapForZ(z);
            return map != null && map.Tiles.Count > 0;
        }
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
            return completed.Contains(TechnologyId.AdvancedTactics);
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
    }
}
