using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers;

public sealed class PrestigeHistoryRenderer : IDisposable
{
    private const float Padding = 20;
    private const float SectionSpacing = 18;
    private const float RowHeight = 26;
    private const float CardPadding = 12;
    private const float CardRadius = 8;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;

    private SKSize _canvasSize;
    private bool _disposed;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint = new() { Color = new SKColor(80, 80, 100), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _currentCardBorderPaint = new() { Color = SKColors.Gold, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedPaint = new() { Color = new SKColor(180, 180, 190), IsAntialias = true };
    private readonly SKPaint _accentPaint = new() { Color = new SKColor(255, 215, 0), IsAntialias = true };
    private readonly SKPaint _labelPaint = new() { Color = new SKColor(140, 180, 220), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 17, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
    private readonly SKFont _boldFont = new() { Size = 13, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
    private readonly SKFont _font = new() { Size = 13, Typeface = SKTypeface.FromFamilyName("Arial") };
    private readonly SKFont _smallFont = new() { Size = 11, Typeface = SKTypeface.FromFamilyName("Arial") };

    public PrestigeHistoryRenderer(GameControllerService gameControllerService, ILocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderHistory(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mainGameState) return;

        float topBarHeight = PlayerResourcesOverlayRenderer.BarHeight;
        var area = new SKRect(0, topBarHeight, _canvasSize.Width, _canvasSize.Height);
        canvas.DrawRect(area, _bgPaint);

        float contentWidth = Math.Min(700, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBarHeight + Padding;

        // Current run
        y = DrawCurrentRun(canvas, mainGameState, x, y, contentWidth);
        y += SectionSpacing;

        // History
        DrawHistory(canvas, mainGameState, x, y, contentWidth);
    }

    private float DrawCurrentRun(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var island = mainGameState.CurrentIslandState;
        var controller = _gameControllerService.MainGameController.PrestigeController;

        string title = _localization.Get("stats_current_run");
        canvas.DrawText(title, x, y + 14, _titleFont, _accentPaint);
        y += 24;

        long tickDuration = mainGameState.Clock.CurrentTick - (island?.StartTick ?? 0);
        int cityCount = island?.PlayerCivilization.Cities.Count ?? 0;
        var allBuildings = island?.PlayerCivilization.Cities.SelectMany(c => c.Buildings).ToList() ?? new();
        int buildingCount = allBuildings.Count;
        int totalLevels = allBuildings.Sum(b => b.Level);
        int prestigePoints = controller.CalculatePrestigePoints();
        int islandId = island?.IslandID ?? 0;

        float cardHeight = CardPadding + RowHeight * 2 + CardPadding;
        var cardRect = new SKRect(x, y, x + width, y + cardHeight);
        canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardPaint);
        canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _currentCardBorderPaint);

        float row1 = y + CardPadding + 10;
        float row2 = row1 + RowHeight;

        DrawStatCell(canvas, x + CardPadding, row1, _localization.Get("stats_island"), $"#{islandId}", width / 4);
        DrawStatCell(canvas, x + width / 4, row1, _localization.Get("stats_playtime"), FormatTicks(tickDuration), width / 4);
        DrawStatCell(canvas, x + width / 2, row1, _localization.Get("stats_cities"), cityCount.ToString(), width / 4);
        DrawStatCell(canvas, x + width * 3 / 4, row1, _localization.Get("stats_prestige_points"), prestigePoints.ToString(), width / 4);

        DrawStatCell(canvas, x + CardPadding, row2, _localization.Get("stats_buildings"), buildingCount.ToString(), width / 3);
        DrawStatCell(canvas, x + width / 3, row2, _localization.Get("stats_total_levels"), totalLevels.ToString(), width / 3);

        return y + cardHeight;
    }

    private void DrawHistory(SKCanvas canvas, MainGameState mainGameState, float x, float y, float width)
    {
        var history = mainGameState.PrestigeState?.RunHistory;
        if (history == null || history.Count == 0)
        {
            canvas.DrawText(_localization.Get("stats_no_history"), x, y + 14, _font, _mutedPaint);
            return;
        }

        string title = _localization.Get("stats_past_runs");
        canvas.DrawText(title, x, y + 14, _titleFont, _textPaint);
        y += 24;

        float cardHeight = CardPadding + RowHeight * 2 + CardPadding;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            var run = history[i];
            var cardRect = new SKRect(x, y, x + width, y + cardHeight);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardPaint);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, _cardBorderPaint);

            float row1 = y + CardPadding + 10;
            float row2 = row1 + RowHeight;

            DrawStatCell(canvas, x + CardPadding, row1, _localization.Get("stats_island"), $"#{run.IslandId}", width / 4);
            DrawStatCell(canvas, x + width / 4, row1, _localization.Get("stats_playtime"), FormatTicks(run.TickDuration), width / 4);
            DrawStatCell(canvas, x + width / 2, row1, _localization.Get("stats_cities"), run.CityCount.ToString(), width / 4);
            DrawStatCell(canvas, x + width * 3 / 4, row1, _localization.Get("stats_prestige_points"), run.PrestigePoints.ToString(), width / 4);

            DrawStatCell(canvas, x + CardPadding, row2, _localization.Get("stats_buildings"), run.BuildingCount.ToString(), width / 3);
            DrawStatCell(canvas, x + width / 3, row2, _localization.Get("stats_total_levels"), run.TotalBuildingLevels.ToString(), width / 3);

            y += cardHeight + 8;

            if (y + cardHeight > _canvasSize.Height - 10)
                break;
        }
    }

    private void DrawStatCell(SKCanvas canvas, float x, float y, string label, string value, float cellWidth)
    {
        canvas.DrawText(label, x, y - 2, _smallFont, _labelPaint);
        canvas.DrawText(value, x, y + 14, _boldFont, _textPaint);
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
