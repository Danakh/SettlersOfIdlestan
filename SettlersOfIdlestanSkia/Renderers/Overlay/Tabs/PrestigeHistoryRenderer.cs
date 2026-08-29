using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Races;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class PrestigeHistoryRenderer : IDisposable
{
    private const float Padding = 20;
    private const float SectionSpacing = 18;
    private const float RowHeight = 26;
    private const float CardPadding = 12;
    private const float CardRadius = 8;
    private const float InnerTabHeight = 28f;
    private const float InnerTabWidth = 140f;
    private const float InnerTabGap = 8f;

    private enum SubTab { Prestige, Ascension, Partie }

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayout;

    private SKSize _canvasSize;
    private bool _disposed;
    private SubTab _activeSubTab = SubTab.Prestige;
    private readonly List<(SubTab tab, SKRect rect)> _innerTabs = new();

    private float _scrollOffsetPx = 0f;
    private float _viewportH = 0f;
    private float _totalContentH = 0f;
    private bool _isDraggingScrollbar = false;
    private float _scrollDragStartY = 0f;
    private float _scrollDragStartOffset = 0f;
    private SKRect _scrollTrackRect = SKRect.Empty;
    private SKRect _scrollThumbRect = SKRect.Empty;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint = new() { Color = new SKColor(80, 80, 100), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _currentCardBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _innerTabActivePaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollTrackPaint = new() { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedPaint = new() { Color = new SKColor(180, 180, 190), IsAntialias = true };
    private readonly SKPaint _accentPaint = new() { Color = new SKColor(255, 215, 0), IsAntialias = true };
    private readonly SKPaint _labelPaint = new() { Color = new SKColor(140, 180, 220), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _boldFont = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _font = new() { Size = 13, Typeface = SkiaFonts.Regular };
    private readonly SKFont _smallFont = new() { Size = 11, Typeface = SkiaFonts.Regular };

    public PrestigeHistoryRenderer(GameControllerService gameControllerService, LocalizationService localization, UILayoutService uiLayout)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _uiLayout = uiLayout;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderHistory(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mainGameState) return;

        float topBarHeight = _uiLayout.SecondRowBottom;
        var area = new SKRect(0, topBarHeight, _canvasSize.Width, _canvasSize.Height);
        canvas.DrawRect(area, _bgPaint);

        float contentWidth = Math.Min(700, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBarHeight + Padding;

        bool hasAscensionTab = HasGodPoints(mainGameState);
        if (!hasAscensionTab && _activeSubTab == SubTab.Ascension) _activeSubTab = SubTab.Prestige;

        y = DrawInnerTabBar(canvas, x, y, contentWidth, hasAscensionTab);
        y += 10;

        float contentTop = y;
        _viewportH = _canvasSize.Height - contentTop;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx, 0, maxScroll);
        bool needsScroll = _totalContentH > _viewportH + 1f;

        canvas.Save();
        canvas.ClipRect(new SKRect(0, contentTop, _canvasSize.Width, _canvasSize.Height));
        canvas.Translate(0, -_scrollOffsetPx);

        switch (_activeSubTab)
        {
            case SubTab.Ascension when hasAscensionTab:
                y = DrawAscensionTab(canvas, mainGameState, x, y, contentWidth);
                break;
            case SubTab.Partie:
                y = DrawPartieTab(canvas, mainGameState, x, y, contentWidth);
                break;
            default:
                y = DrawCurrentRun(canvas, mainGameState, x, y, contentWidth);
                y += SectionSpacing;
                y = DrawHistory(canvas, mainGameState, x, y, contentWidth);
                break;
        }

        canvas.Restore();

        _totalContentH = y + Padding - contentTop;

        if (needsScroll)
            DrawScrollbar(canvas, contentTop, _viewportH);
    }

    private static bool HasGodPoints(MainGameState mainGameState) =>
        mainGameState.GodState.TotalGodPointsEarned > 0 || mainGameState.GodState.TotalDivineEssenceEarned > 0;

    private float DrawInnerTabBar(SKCanvas canvas, float x, float y, float contentWidth, bool hasAscensionTab)
    {
        var tabs = new List<(SubTab tab, string label)> { (SubTab.Prestige, _localization.Get("stats_tab_prestige")) };
        if (hasAscensionTab) tabs.Add((SubTab.Ascension, _localization.Get("stats_tab_ascension")));
        tabs.Add((SubTab.Partie, _localization.Get("stats_tab_run")));

        _innerTabs.Clear();
        float totalWidth = tabs.Count * InnerTabWidth + (tabs.Count - 1) * InnerTabGap;
        float tabX = x + (contentWidth - totalWidth) / 2f;

        foreach (var (tab, label) in tabs)
        {
            var rect = new SKRect(tabX, y, tabX + InnerTabWidth, y + InnerTabHeight);
            _innerTabs.Add((tab, rect));

            bool active = _activeSubTab == tab;
            canvas.DrawRoundRect(rect, 5, 5, active ? _innerTabActivePaint : _cardPaint);
            canvas.DrawRoundRect(rect, 5, 5, active ? _currentCardBorderPaint : _cardBorderPaint);
            SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 4f, SKTextAlign.Center, _boldFont, _textPaint);

            tabX += InnerTabWidth + InnerTabGap;
        }

        return y + InnerTabHeight;
    }

    /// Returns true if the click was on one of the inner sub-tabs or the scrollbar (and consumed).
    public bool HandlePointerPressed(SKPoint position)
    {
        foreach (var (tab, rect) in _innerTabs)
        {
            if (rect.Contains(position.X, position.Y))
            {
                if (_activeSubTab != tab) _scrollOffsetPx = 0f;
                _activeSubTab = tab;
                return true;
            }
        }

        if (!_scrollThumbRect.IsEmpty && _scrollThumbRect.Contains(position.X, position.Y))
        {
            _isDraggingScrollbar = true;
            _scrollDragStartY = position.Y;
            _scrollDragStartOffset = _scrollOffsetPx;
            return true;
        }
        if (!_scrollTrackRect.IsEmpty && _scrollTrackRect.Contains(position.X, position.Y))
        {
            float relY = position.Y - _scrollTrackRect.Top;
            float maxScroll = Math.Max(0, _totalContentH - _viewportH);
            _scrollOffsetPx = Math.Clamp(relY / _scrollTrackRect.Height * maxScroll, 0, maxScroll);
            return true;
        }

        return false;
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!_isDraggingScrollbar) return;
        float dy = position.Y - _scrollDragStartY;
        float thumbRange = _scrollTrackRect.Height - _scrollThumbRect.Height;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        float scrollPerPx = thumbRange > 0 ? maxScroll / thumbRange : 0;
        _scrollOffsetPx = Math.Clamp(_scrollDragStartOffset + dy * scrollPerPx, 0, maxScroll);
    }

    public void HandlePointerReleased(SKPoint position)
    {
        _isDraggingScrollbar = false;
    }

    public void HandleScroll(float delta)
    {
        const float step = 60f;
        float dir = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0, _totalContentH - _viewportH);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step, 0, maxScroll);
    }

    private void DrawScrollbar(SKCanvas canvas, float trackTop, float trackH)
    {
        const float scrollW = 6f;
        const float scrollMargin = 4f;
        float trackX = _canvasSize.Width - scrollW - scrollMargin;

        _scrollTrackRect = new SKRect(trackX, trackTop, trackX + scrollW, trackTop + trackH);
        canvas.DrawRoundRect(_scrollTrackRect, 3, 3, _scrollTrackPaint);

        float thumbRatio = _viewportH / _totalContentH;
        float thumbH = Math.Max(24f, thumbRatio * trackH);
        float maxScroll = Math.Max(1, _totalContentH - _viewportH);
        float thumbTop = trackTop + (_scrollOffsetPx / maxScroll) * (trackH - thumbH);
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);
        canvas.DrawRoundRect(_scrollThumbRect, 3, 3, _scrollThumbPaint);
    }

    private float DrawCurrentRun(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var island = mainGameState.CurrentWorldState;
        var controller = _gameControllerService.MainGameController.PrestigeController;

        string title = _localization.Get("stats_current_run");
        SkiaTextUtils.DrawText(canvas, title, x, y + 14, _titleFont, _accentPaint);
        y += 24;

        long tickDuration = mainGameState.Clock.CurrentTick - (island?.StartTick ?? 0);
        int cityCount = island?.PlayerCivilization.Cities.Count ?? 0;
        var allBuildings = island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings).ToList() ?? new();
        int buildingCount = allBuildings.Count;
        int totalLevels = allBuildings.Sum(b => b.Level);
        int uniqueBuildings = allBuildings.Count(b => b.IsUnique);
        int totalResearch = mainGameState.GameRecord?.TotalResearchCompleted ?? 0;
        int prestigePoints = controller.CalculatePrestigePoints();
        int WorldId = island?.WorldId ?? 0;

        var wonder = island?.Features.OfType<Wonder>().FirstOrDefault();
        int wonderLevel = wonder?.Level ?? 0;
        bool hasDeepestMine = island?.Features.OfType<DeepestMine>().Any(m => m.Dug) == true;
        bool hasCorruptionSpire = island?.Features.OfType<CorruptionSpire>().Any(s => s.Built) == true;
        bool hasAbyssGate = island?.Features.OfType<AbyssGate>().Any(g => g.Built) == true;
        bool hasRow3 = wonderLevel > 0 || hasDeepestMine || hasCorruptionSpire || hasAbyssGate;

        int tier = mainGameState.PrestigeState?.Tier ?? 1;
        int corruption = mainGameState.PrestigeState?.CurrentCorruptionLevel ?? 0;

        float cardHeight = CardPadding + RowHeight * 3 + (hasRow3 ? RowHeight : 0) + CardPadding;
        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _currentCardBorderPaint);

        float row1 = y + CardPadding + 10;
        float row2 = row1 + RowHeight;

        DrawStatCell(canvas, x + CardPadding, row1, _localization.Get("stats_island"), $"#{WorldId}", width / 4);
        DrawStatCell(canvas, x + width / 4, row1, _localization.Get("stats_playtime"), FormatTicks(tickDuration), width / 4);
        DrawStatCell(canvas, x + width / 2, row1, _localization.Get("stats_research"), totalResearch.ToString(), width / 4);
        DrawStatCell(canvas, x + width * 3 / 4, row1, _localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(prestigePoints), width / 4);

        DrawStatCell(canvas, x + CardPadding, row2, _localization.Get("stats_cities"), cityCount.ToString(), width / 4);
        DrawStatCell(canvas, x + width / 4, row2, _localization.Get("stats_buildings"), buildingCount.ToString(), width / 4);
        DrawStatCell(canvas, x + width / 2, row2, _localization.Get("stats_total_levels"), totalLevels.ToString(), width / 4);
        DrawStatCell(canvas, x + width * 3 / 4, row2, _localization.Get("stats_unique_buildings"), uniqueBuildings.ToString(), width / 4);

        if (hasRow3)
        {
            float row3 = row2 + RowHeight;
            if (wonderLevel > 0)      DrawStatCell(canvas, x + CardPadding,        row3, _localization.Get("stats_wonder"),           wonderLevel.ToString(), width / 4);
            if (hasDeepestMine)       DrawStatCell(canvas, x + width / 4,           row3, _localization.Get("stats_deepest_mine"),     "✓",                    width / 4);
            if (hasCorruptionSpire)   DrawStatCell(canvas, x + width / 2,           row3, _localization.Get("stats_corruption_spire"), "✓",                    width / 4);
            if (hasAbyssGate)         DrawStatCell(canvas, x + width * 3 / 4,       row3, _localization.Get("stats_abyss_gate"),       "✓",                    width / 4);
        }

        float tierRow = row2 + (hasRow3 ? RowHeight * 2 : RowHeight);
        DrawStatCell(canvas, x + CardPadding, tierRow, _localization.Get("stats_tier"), tier.ToString(), width / 4);
        if (corruption > 0) DrawStatCell(canvas, x + width / 4, tierRow, _localization.Get("stats_corruption"), corruption.ToString(), width / 4);

        return y + cardHeight;
    }

    private float DrawHistory(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var history = mainGameState.PrestigeState?.RunHistory;
        if (history == null || history.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("stats_no_history"), x, y + 14, _font, _mutedPaint);
            return y + 24;
        }

        string title = _localization.Get("stats_past_runs");
        SkiaTextUtils.DrawText(canvas, title, x, y + 14, _titleFont, _textPaint);
        y += 24;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var run = history[i];
            bool runHasRow3 = run.WonderLevel > 0 || run.HasDeepestMine || run.HasCorruptionSpire || run.HasAbyssGate;
            bool runHasTierRow = run.Tier > 0;
            float cardHeight = CardPadding + RowHeight * 2 + (runHasRow3 ? RowHeight : 0) + (runHasTierRow ? RowHeight : 0) + CardPadding;

            var cardRect = new SKRect(x, y, x + width, y + cardHeight);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardPaint);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardBorderPaint);

            float row1 = y + CardPadding + 10;
            float row2 = row1 + RowHeight;

            DrawStatCell(canvas, x + CardPadding, row1, _localization.Get("stats_island"), $"#{run.WorldId}", width / 4);
            DrawStatCell(canvas, x + width / 4, row1, _localization.Get("stats_playtime"), FormatTicks(run.TickDuration), width / 4);
            DrawStatCell(canvas, x + width / 2, row1, _localization.Get("stats_research"), run.ResearchCompleted.ToString(), width / 4);
            DrawStatCell(canvas, x + width * 3 / 4, row1, _localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(run.PrestigePoints), width / 4);

            DrawStatCell(canvas, x + CardPadding, row2, _localization.Get("stats_cities"), run.CityCount.ToString(), width / 4);
            DrawStatCell(canvas, x + width / 4, row2, _localization.Get("stats_buildings"), run.BuildingCount.ToString(), width / 4);
            DrawStatCell(canvas, x + width / 2, row2, _localization.Get("stats_total_levels"), run.TotalBuildingLevels.ToString(), width / 4);
            DrawStatCell(canvas, x + width * 3 / 4, row2, _localization.Get("stats_unique_buildings"), run.UniqueBuildings.ToString(), width / 4);

            if (runHasRow3)
            {
                float row3 = row2 + RowHeight;
                if (run.WonderLevel > 0)     DrawStatCell(canvas, x + CardPadding,  row3, _localization.Get("stats_wonder"),           run.WonderLevel.ToString(), width / 4);
                if (run.HasDeepestMine)      DrawStatCell(canvas, x + width / 4,    row3, _localization.Get("stats_deepest_mine"),     "✓",                        width / 4);
                if (run.HasCorruptionSpire)  DrawStatCell(canvas, x + width / 2,    row3, _localization.Get("stats_corruption_spire"), "✓",                        width / 4);
                if (run.HasAbyssGate)        DrawStatCell(canvas, x + width * 3 / 4, row3, _localization.Get("stats_abyss_gate"),      "✓",                        width / 4);
            }

            if (runHasTierRow)
            {
                float tierRow = row2 + (runHasRow3 ? RowHeight * 2 : RowHeight);
                DrawStatCell(canvas, x + CardPadding, tierRow, _localization.Get("stats_tier"), run.Tier.ToString(), width / 4);
                if (run.Corruption > 0) DrawStatCell(canvas, x + width / 4, tierRow, _localization.Get("stats_corruption"), run.Corruption.ToString(), width / 4);
            }

            y += cardHeight + 8;
        }

        return y;
    }

    private float DrawAscensionTab(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var ascension = _gameControllerService.MainGameController.AscensionController;
        var ascensionState = mainGameState.GodState.AscensionState;
        var prestigeState = mainGameState.PrestigeState;
        float col = width / 3;

        SkiaTextUtils.DrawText(canvas, _localization.Get("stats_ascension_current"), x, y + 14, _titleFont, _accentPaint);
        y += 24;

        int tier = prestigeState?.Tier ?? 1;
        int corruption = prestigeState?.CurrentCorruptionLevel ?? 1;
        long playtime = mainGameState.Clock.CurrentTick - ascensionState.CycleStartTick;
        int research = (mainGameState.GameRecord?.TotalResearchCompleted ?? 0) - ascensionState.CycleStartResearchCompleted;
        int prestigePoints = prestigeState?.TotalPrestigePointsEarned ?? 0;
        int projectedDivinePoints = ascension.GetGodPointsGain(mainGameState.GodState);

        float currentCardHeight = CardPadding + RowHeight * 2 + CardPadding;
        var currentRect = new SKRect(x, y, x + width, y + currentCardHeight);
        canvas.DrawRoundRect(currentRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(currentRect, CardRadius, CardRadius, _currentCardBorderPaint);

        float row1 = y + CardPadding + 10;
        float row2 = row1 + RowHeight;

        DrawStatCell(canvas, x + CardPadding, row1, _localization.Get("stats_max_island_tier"), tier.ToString(), col);
        DrawStatCell(canvas, x + col, row1, _localization.Get("stats_max_corruption"), corruption.ToString(), col);
        DrawStatCell(canvas, x + col * 2, row1, _localization.Get("stats_playtime"), FormatTicks(playtime), col);

        DrawStatCell(canvas, x + CardPadding, row2, _localization.Get("stats_research"), research.ToString(), col);
        DrawStatCell(canvas, x + col, row2, _localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(prestigePoints), col);
        DrawStatCell(canvas, x + col * 2, row2, _localization.Get("stats_divine_points_gained"), SkiaTextUtils.FormatNumber(projectedDivinePoints), col);

        y += currentCardHeight + SectionSpacing;

        var history = ascensionState.RunHistory;
        if (history.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("stats_no_ascension_history"), x, y + 14, _font, _mutedPaint);
            return y + 24;
        }

        SkiaTextUtils.DrawText(canvas, _localization.Get("stats_ascension_history"), x, y + 14, _titleFont, _textPaint);
        y += 24;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var run = history[i];
            float cardHeight = CardPadding + RowHeight * 2 + CardPadding;
            var cardRect = new SKRect(x, y, x + width, y + cardHeight);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardPaint);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardBorderPaint);

            float r1 = y + CardPadding + 10;
            float r2 = r1 + RowHeight;

            DrawStatCell(canvas, x + CardPadding, r1, _localization.Get("stats_max_island_tier"), run.MaxIslandTierReached.ToString(), col);
            DrawStatCell(canvas, x + col, r1, _localization.Get("stats_max_corruption"), run.MaxCorruptionReached.ToString(), col);
            DrawStatCell(canvas, x + col * 2, r1, _localization.Get("stats_playtime"), FormatTicks(run.TickDuration), col);

            DrawStatCell(canvas, x + CardPadding, r2, _localization.Get("stats_research"), run.ResearchCompleted.ToString(), col);
            DrawStatCell(canvas, x + col, r2, _localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(run.FinalPrestigePoints), col);
            DrawStatCell(canvas, x + col * 2, r2, _localization.Get("stats_divine_points_gained"), SkiaTextUtils.FormatNumber(run.DivinePointsGained), col);

            y += cardHeight + 8;
        }

        return y;
    }

    private float DrawPartieTab(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var gameRecord = mainGameState.GameRecord;
        var ascensionState = mainGameState.GodState.AscensionState;
        float col4 = width / 4;
        float col3 = width / 3;

        SkiaTextUtils.DrawText(canvas, _localization.Get("stats_partie_total_playtime"), x, y + 14, _titleFont, _accentPaint);
        y += 24;

        float totalCardHeight = CardPadding + RowHeight + CardPadding;
        var totalRect = new SKRect(x, y, x + width, y + totalCardHeight);
        canvas.DrawRoundRect(totalRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(totalRect, CardRadius, CardRadius, _currentCardBorderPaint);
        DrawStatCell(canvas, x + CardPadding, y + CardPadding + 10, _localization.Get("stats_playtime"), FormatTicks(mainGameState.Clock.CurrentTick), col4);
        y += totalCardHeight + SectionSpacing;

        SkiaTextUtils.DrawText(canvas, _localization.Get("stats_partie_prestige_records"), x, y + 14, _titleFont, _textPaint);
        y += 24;

        bool hasFlagsRow = gameRecord.HasDugDeepestMine || gameRecord.HasBuiltCorruptionSpire || gameRecord.HasBuiltAbyssGate;
        float prestigeCardHeight = CardPadding + RowHeight * 2 + (hasFlagsRow ? RowHeight : 0) + CardPadding;
        var prestigeRect = new SKRect(x, y, x + width, y + prestigeCardHeight);
        canvas.DrawRoundRect(prestigeRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(prestigeRect, CardRadius, CardRadius, _cardBorderPaint);

        float pr1 = y + CardPadding + 10;
        float pr2 = pr1 + RowHeight;

        DrawStatCell(canvas, x + CardPadding, pr1, _localization.Get("stats_cities"), gameRecord.MaxCitiesInSingleRun.ToString(), col4);
        DrawStatCell(canvas, x + col4, pr1, _localization.Get("stats_buildings"), gameRecord.MaxBuildingsInSingleRun.ToString(), col4);
        DrawStatCell(canvas, x + col4 * 2, pr1, _localization.Get("stats_total_levels"), gameRecord.MaxTotalBuildingLevelsInSingleRun.ToString(), col4);
        DrawStatCell(canvas, x + col4 * 3, pr1, _localization.Get("stats_unique_buildings"), gameRecord.MaxUniqueBuildingsInSingleRun.ToString(), col4);

        DrawStatCell(canvas, x + CardPadding, pr2, _localization.Get("stats_research"), gameRecord.MaxResearchInSingleRun.ToString(), col4);
        DrawStatCell(canvas, x + col4, pr2, _localization.Get("stats_playtime"), FormatTicks(gameRecord.MaxPlaytimeInSingleRun), col4);
        DrawStatCell(canvas, x + col4 * 2, pr2, _localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(gameRecord.MaxPrestigePointsInSingleRun), col4);
        DrawStatCell(canvas, x + col4 * 3, pr2, _localization.Get("stats_wonder"), gameRecord.MaxWonderLevelReached.ToString(), col4);

        if (hasFlagsRow)
        {
            float pr3 = pr2 + RowHeight;
            float flagX = x + CardPadding;
            if (gameRecord.HasDugDeepestMine)       { DrawStatCell(canvas, flagX, pr3, _localization.Get("stats_deepest_mine"), "✓", col4); flagX += col4; }
            if (gameRecord.HasBuiltCorruptionSpire) { DrawStatCell(canvas, flagX, pr3, _localization.Get("stats_corruption_spire"), "✓", col4); flagX += col4; }
            if (gameRecord.HasBuiltAbyssGate)       { DrawStatCell(canvas, flagX, pr3, _localization.Get("stats_abyss_gate"), "✓", col4); }
        }

        y += prestigeCardHeight + SectionSpacing;

        if (ascensionState.AscensionsPerformed <= 0) return y;

        SkiaTextUtils.DrawText(canvas, _localization.Get("stats_partie_ascension_records"), x, y + 14, _titleFont, _textPaint);
        y += 24;

        float ascCardHeight = CardPadding + RowHeight * 2 + CardPadding;
        var ascRect = new SKRect(x, y, x + width, y + ascCardHeight);
        canvas.DrawRoundRect(ascRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(ascRect, CardRadius, CardRadius, _cardBorderPaint);

        float ar1 = y + CardPadding + 10;
        float ar2 = ar1 + RowHeight;

        DrawStatCell(canvas, x + CardPadding, ar1, _localization.Get("stats_max_island_tier"), ascensionState.MaxIslandTierReached.ToString(), col3);
        DrawStatCell(canvas, x + col3, ar1, _localization.Get("stats_max_corruption"), ascensionState.MaxCorruptionReached.ToString(), col3);
        DrawStatCell(canvas, x + col3 * 2, ar1, _localization.Get("stats_playtime"), FormatTicks(ascensionState.MaxPlaytimeInSingleAscension), col3);

        DrawStatCell(canvas, x + CardPadding, ar2, _localization.Get("stats_research"), ascensionState.MaxResearchInSingleAscension.ToString(), col3);
        DrawStatCell(canvas, x + col3, ar2, _localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(ascensionState.MaxPrestigePointsInSingleAscension), col3);
        DrawStatCell(canvas, x + col3 * 2, ar2, _localization.Get("stats_divine_points_gained"), SkiaTextUtils.FormatNumber(ascensionState.MaxDivinePointsInSingleAscension), col3);

        y += ascCardHeight + SectionSpacing;

        SkiaTextUtils.DrawText(canvas, _localization.Get("stats_partie_races_played"), x, y + 14, _titleFont, _textPaint);
        y += 24;

        var races = ascensionState.AscendedRaces;
        const float raceRowHeight = 22f;
        float raceCardHeight = CardPadding + raceRowHeight * Math.Max(1, races.Count) + CardPadding;
        var raceRect = new SKRect(x, y, x + width, y + raceCardHeight);
        canvas.DrawRoundRect(raceRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(raceRect, CardRadius, CardRadius, _cardBorderPaint);

        float raceY = y + CardPadding + 14;
        foreach (var raceId in races)
        {
            string raceName = _localization.Get(RaceDefinitions.Get(raceId).NameKey);
            SkiaTextUtils.DrawText(canvas, raceName, x + CardPadding, raceY, _font, _textPaint);
            raceY += raceRowHeight;
        }

        return y + raceCardHeight;
    }

    // ── Pont vers l'hôte Avalonia ─────────────────────────────────────────────

    /// <summary>
    /// Instantané de l'onglet pour une vue portée par l'hôte. Reprend les mêmes règles que les
    /// méthodes <c>Draw*Tab</c> : quelles statistiques existent, lesquelles restent masquées
    /// faute d'objet, et quel sous-onglet est disponible.
    /// </summary>
    /// <param name="isVisible">L'onglet Stats est-il actif ? La règle appartient à
    /// <c>OverlayRenderer</c>, qui détient l'onglet courant.</param>
    public StatsSnapshot GetSnapshot(bool isVisible)
    {
        if (_disposed || !isVisible) return StatsSnapshot.Hidden;
        if (_gameControllerService.CurrentGameState is not { } state) return StatsSnapshot.Hidden;

        bool hasAscensionTab = HasGodPoints(state);
        if (!hasAscensionTab && _activeSubTab == SubTab.Ascension) _activeSubTab = SubTab.Prestige;

        var subTabs = new List<StatsSubTabSnapshot>
        {
            new(StatsSnapshot.SubTabPrestige, _localization.Get("stats_tab_prestige"), _activeSubTab == SubTab.Prestige),
        };
        if (hasAscensionTab)
            subTabs.Add(new(StatsSnapshot.SubTabAscension, _localization.Get("stats_tab_ascension"), _activeSubTab == SubTab.Ascension));
        subTabs.Add(new(StatsSnapshot.SubTabRun, _localization.Get("stats_tab_run"), _activeSubTab == SubTab.Partie));

        var sections = _activeSubTab switch
        {
            SubTab.Ascension when hasAscensionTab => BuildAscensionSections(state),
            SubTab.Partie                         => BuildRunSections(state),
            _                                     => BuildPrestigeSections(state),
        };

        return new StatsSnapshot(IsVisible: true, SubTabs: subTabs, Sections: sections);
    }

    /// <summary>Sélectionne un sous-onglet depuis une vue portée par l'hôte.</summary>
    public void SetSubTabFromHost(string key)
    {
        var target = key switch
        {
            StatsSnapshot.SubTabAscension => SubTab.Ascension,
            StatsSnapshot.SubTabRun       => SubTab.Partie,
            _                             => SubTab.Prestige,
        };
        if (_activeSubTab == target) return;
        _activeSubTab = target;
        _scrollOffsetPx = 0f;
    }

    private static StatCardSnapshot Card(List<StatCellSnapshot> cells, int columns, bool isCurrent = false) =>
        new(cells, columns, isCurrent, []);

    private List<StatSectionSnapshot> BuildPrestigeSections(MainGameState state)
    {
        var island = state.CurrentWorldState;
        var controller = _gameControllerService.MainGameController.PrestigeController;

        long tickDuration = state.Clock.CurrentTick - (island?.StartTick ?? 0);
        var allBuildings = island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings).ToList() ?? new();
        var wonder = island?.Features.OfType<Wonder>().FirstOrDefault();
        int wonderLevel = wonder?.Level ?? 0;
        bool hasDeepestMine = island?.Features.OfType<DeepestMine>().Any(m => m.Dug) == true;
        bool hasCorruptionSpire = island?.Features.OfType<CorruptionSpire>().Any(s => s.Built) == true;
        bool hasAbyssGate = island?.Features.OfType<AbyssGate>().Any(g => g.Built) == true;
        int corruption = state.PrestigeState?.CurrentCorruptionLevel ?? 0;

        var current = new List<StatCellSnapshot>
        {
            new(_localization.Get("stats_island"), $"#{island?.WorldId ?? 0}"),
            new(_localization.Get("stats_playtime"), FormatTicks(tickDuration)),
            new(_localization.Get("stats_research"), (state.GameRecord?.TotalResearchCompleted ?? 0).ToString()),
            new(_localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(controller.CalculatePrestigePoints())),
            new(_localization.Get("stats_cities"), (island?.PlayerCivilization.Cities.Count ?? 0).ToString()),
            new(_localization.Get("stats_buildings"), allBuildings.Count.ToString()),
            new(_localization.Get("stats_total_levels"), allBuildings.Sum(b => b.Level).ToString()),
            new(_localization.Get("stats_unique_buildings"), allBuildings.Count(b => b.IsUnique).ToString()),
        };
        if (wonderLevel > 0)    current.Add(new(_localization.Get("stats_wonder"), wonderLevel.ToString()));
        if (hasDeepestMine)     current.Add(new(_localization.Get("stats_deepest_mine"), Check));
        if (hasCorruptionSpire) current.Add(new(_localization.Get("stats_corruption_spire"), Check));
        if (hasAbyssGate)       current.Add(new(_localization.Get("stats_abyss_gate"), Check));
        current.Add(new(_localization.Get("stats_tier"), (state.PrestigeState?.Tier ?? 1).ToString()));
        if (corruption > 0)     current.Add(new(_localization.Get("stats_corruption"), corruption.ToString()));

        var sections = new List<StatSectionSnapshot>
        {
            new(_localization.Get("stats_current_run"), IsAccent: true, EmptyMessage: null, Cards: [Card(current, 4, isCurrent: true)]),
        };

        var history = state.PrestigeState?.RunHistory;
        var cards = new List<StatCardSnapshot>();
        // Ordre inverse : la course la plus recente en premier, comme le rendu Skia.
        for (int i = (history?.Count ?? 0) - 1; i >= 0; i--)
        {
            var run = history![i];
            var cells = new List<StatCellSnapshot>
            {
                new(_localization.Get("stats_island"), $"#{run.WorldId}"),
                new(_localization.Get("stats_playtime"), FormatTicks(run.TickDuration)),
                new(_localization.Get("stats_research"), run.ResearchCompleted.ToString()),
                new(_localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(run.PrestigePoints)),
                new(_localization.Get("stats_cities"), run.CityCount.ToString()),
                new(_localization.Get("stats_buildings"), run.BuildingCount.ToString()),
                new(_localization.Get("stats_total_levels"), run.TotalBuildingLevels.ToString()),
                new(_localization.Get("stats_unique_buildings"), run.UniqueBuildings.ToString()),
            };
            if (run.WonderLevel > 0)    cells.Add(new(_localization.Get("stats_wonder"), run.WonderLevel.ToString()));
            if (run.HasDeepestMine)     cells.Add(new(_localization.Get("stats_deepest_mine"), Check));
            if (run.HasCorruptionSpire) cells.Add(new(_localization.Get("stats_corruption_spire"), Check));
            if (run.HasAbyssGate)       cells.Add(new(_localization.Get("stats_abyss_gate"), Check));
            if (run.Tier > 0)
            {
                cells.Add(new(_localization.Get("stats_tier"), run.Tier.ToString()));
                if (run.Corruption > 0) cells.Add(new(_localization.Get("stats_corruption"), run.Corruption.ToString()));
            }
            cards.Add(Card(cells, 4));
        }

        sections.Add(new(_localization.Get("stats_past_runs"), IsAccent: false,
            EmptyMessage: cards.Count == 0 ? _localization.Get("stats_no_history") : null, Cards: cards));

        return sections;
    }

    private List<StatSectionSnapshot> BuildAscensionSections(MainGameState state)
    {
        var ascension = _gameControllerService.MainGameController.AscensionController;
        var ascensionState = state.GodState.AscensionState;
        var prestigeState = state.PrestigeState;

        var current = new List<StatCellSnapshot>
        {
            new(_localization.Get("stats_max_island_tier"), (prestigeState?.Tier ?? 1).ToString()),
            new(_localization.Get("stats_max_corruption"), (prestigeState?.CurrentCorruptionLevel ?? 1).ToString()),
            new(_localization.Get("stats_playtime"), FormatTicks(state.Clock.CurrentTick - ascensionState.CycleStartTick)),
            new(_localization.Get("stats_research"), ((state.GameRecord?.TotalResearchCompleted ?? 0) - ascensionState.CycleStartResearchCompleted).ToString()),
            new(_localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(prestigeState?.TotalPrestigePointsEarned ?? 0)),
            new(_localization.Get("stats_divine_points_gained"), SkiaTextUtils.FormatNumber(ascension.GetGodPointsGain(state.GodState))),
        };

        var sections = new List<StatSectionSnapshot>
        {
            new(_localization.Get("stats_ascension_current"), IsAccent: true, EmptyMessage: null, Cards: [Card(current, 3, isCurrent: true)]),
        };

        var cards = new List<StatCardSnapshot>();
        for (int i = ascensionState.RunHistory.Count - 1; i >= 0; i--)
        {
            var run = ascensionState.RunHistory[i];
            cards.Add(Card(
            [
                new(_localization.Get("stats_max_island_tier"), run.MaxIslandTierReached.ToString()),
                new(_localization.Get("stats_max_corruption"), run.MaxCorruptionReached.ToString()),
                new(_localization.Get("stats_playtime"), FormatTicks(run.TickDuration)),
                new(_localization.Get("stats_research"), run.ResearchCompleted.ToString()),
                new(_localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(run.FinalPrestigePoints)),
                new(_localization.Get("stats_divine_points_gained"), SkiaTextUtils.FormatNumber(run.DivinePointsGained)),
            ], 3));
        }

        sections.Add(new(_localization.Get("stats_ascension_history"), IsAccent: false,
            EmptyMessage: cards.Count == 0 ? _localization.Get("stats_no_ascension_history") : null, Cards: cards));

        return sections;
    }

    private List<StatSectionSnapshot> BuildRunSections(MainGameState state)
    {
        var gameRecord = state.GameRecord;
        var ascensionState = state.GodState.AscensionState;

        var sections = new List<StatSectionSnapshot>
        {
            new(_localization.Get("stats_partie_total_playtime"), IsAccent: true, EmptyMessage: null,
                Cards: [Card([new(_localization.Get("stats_playtime"), FormatTicks(state.Clock.CurrentTick))], 4, isCurrent: true)]),
        };

        var records = new List<StatCellSnapshot>
        {
            new(_localization.Get("stats_cities"), gameRecord.MaxCitiesInSingleRun.ToString()),
            new(_localization.Get("stats_buildings"), gameRecord.MaxBuildingsInSingleRun.ToString()),
            new(_localization.Get("stats_total_levels"), gameRecord.MaxTotalBuildingLevelsInSingleRun.ToString()),
            new(_localization.Get("stats_unique_buildings"), gameRecord.MaxUniqueBuildingsInSingleRun.ToString()),
            new(_localization.Get("stats_research"), gameRecord.MaxResearchInSingleRun.ToString()),
            new(_localization.Get("stats_playtime"), FormatTicks(gameRecord.MaxPlaytimeInSingleRun)),
            new(_localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(gameRecord.MaxPrestigePointsInSingleRun)),
            new(_localization.Get("stats_wonder"), gameRecord.MaxWonderLevelReached.ToString()),
        };
        if (gameRecord.HasDugDeepestMine)       records.Add(new(_localization.Get("stats_deepest_mine"), Check));
        if (gameRecord.HasBuiltCorruptionSpire) records.Add(new(_localization.Get("stats_corruption_spire"), Check));
        if (gameRecord.HasBuiltAbyssGate)       records.Add(new(_localization.Get("stats_abyss_gate"), Check));

        sections.Add(new(_localization.Get("stats_partie_prestige_records"), IsAccent: false, EmptyMessage: null,
            Cards: [Card(records, 4)]));

        // Les records d'ascension n'ont de sens qu'apres une premiere ascension.
        if (ascensionState.AscensionsPerformed <= 0) return sections;

        sections.Add(new(_localization.Get("stats_partie_ascension_records"), IsAccent: false, EmptyMessage: null,
            Cards:
            [
                Card(
                [
                    new(_localization.Get("stats_max_island_tier"), ascensionState.MaxIslandTierReached.ToString()),
                    new(_localization.Get("stats_max_corruption"), ascensionState.MaxCorruptionReached.ToString()),
                    new(_localization.Get("stats_playtime"), FormatTicks(ascensionState.MaxPlaytimeInSingleAscension)),
                    new(_localization.Get("stats_research"), ascensionState.MaxResearchInSingleAscension.ToString()),
                    new(_localization.Get("stats_prestige_points"), SkiaTextUtils.FormatNumber(ascensionState.MaxPrestigePointsInSingleAscension)),
                    new(_localization.Get("stats_divine_points_gained"), SkiaTextUtils.FormatNumber(ascensionState.MaxDivinePointsInSingleAscension)),
                ], 3),
            ]));

        var raceNames = ascensionState.AscendedRaces
            .Select(id => _localization.Get(RaceDefinitions.Get(id).NameKey))
            .ToList();

        sections.Add(new(_localization.Get("stats_partie_races_played"), IsAccent: false, EmptyMessage: null,
            Cards: [new StatCardSnapshot([], 1, false, raceNames)]));

        return sections;
    }

    private const string Check = "✓";

    private void DrawStatCell(SKCanvas canvas, float x, float y, string label, string value, float cellWidth)
    {
        SkiaTextUtils.DrawText(canvas, label, x, y - 2, _smallFont, _labelPaint);
        SkiaTextUtils.DrawText(canvas, value, x, y + 14, _boldFont, _textPaint);
    }

    private static string FormatTicks(long ticks)
    {
        long totalSeconds = ticks / 100;
        long hours = totalSeconds / 3600;
        long minutes = (totalSeconds % 3600) / 60;
        long seconds = totalSeconds % 60;
        return hours > 0
            ? $"{hours}h{minutes:D2}m{seconds:D2}s"
            : $"{minutes}m{seconds:D2}s";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardBorderPaint.Dispose();
        _currentCardBorderPaint.Dispose();
        _innerTabActivePaint.Dispose();
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _textPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _labelPaint.Dispose();
        _titleFont.Dispose();
        _boldFont.Dispose();
        _font.Dispose();
        _smallFont.Dispose();
        _disposed = true;
    }
}
