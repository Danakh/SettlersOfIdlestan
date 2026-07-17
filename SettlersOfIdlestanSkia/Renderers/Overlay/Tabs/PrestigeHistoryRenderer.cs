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

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint = new() { Color = new SKColor(80, 80, 100), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _currentCardBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _innerTabActivePaint = new() { Color = new SKColor(60, 100, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
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

        switch (_activeSubTab)
        {
            case SubTab.Ascension when hasAscensionTab:
                DrawAscensionTab(canvas, mainGameState, x, y, contentWidth);
                break;
            case SubTab.Partie:
                DrawPartieTab(canvas, mainGameState, x, y, contentWidth);
                break;
            default:
                y = DrawCurrentRun(canvas, mainGameState, x, y, contentWidth);
                y += SectionSpacing;
                DrawHistory(canvas, mainGameState, x, y, contentWidth);
                break;
        }
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

    /// Returns true if the click was on one of the inner sub-tabs (and consumed).
    public bool HandlePointerPressed(SKPoint position)
    {
        foreach (var (tab, rect) in _innerTabs)
        {
            if (rect.Contains(position.X, position.Y))
            {
                _activeSubTab = tab;
                return true;
            }
        }
        return false;
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

        float cardHeight = CardPadding + RowHeight * 2 + (hasRow3 ? RowHeight : 0) + CardPadding;
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

        return y + cardHeight;
    }

    private void DrawHistory(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var history = mainGameState.PrestigeState?.RunHistory;
        if (history == null || history.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("stats_no_history"), x, y + 14, _font, _mutedPaint);
            return;
        }

        string title = _localization.Get("stats_past_runs");
        SkiaTextUtils.DrawText(canvas, title, x, y + 14, _titleFont, _textPaint);
        y += 24;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var run = history[i];
            bool runHasRow3 = run.WonderLevel > 0 || run.HasDeepestMine || run.HasCorruptionSpire || run.HasAbyssGate;
            float cardHeight = CardPadding + RowHeight * 2 + (runHasRow3 ? RowHeight : 0) + CardPadding;

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

            y += cardHeight + 8;

            if (y + cardHeight > _canvasSize.Height - 10)
                break;
        }
    }

    private void DrawAscensionTab(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
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

        y += currentCardHeight + SectionSpacing;

        var history = ascensionState.RunHistory;
        if (history.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("stats_no_ascension_history"), x, y + 14, _font, _mutedPaint);
            return;
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

            y += cardHeight + 8;
            if (y + cardHeight > _canvasSize.Height - 10) break;
        }
    }

    private void DrawPartieTab(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
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

        if (ascensionState.AscensionsPerformed <= 0) return;

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
    }

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
