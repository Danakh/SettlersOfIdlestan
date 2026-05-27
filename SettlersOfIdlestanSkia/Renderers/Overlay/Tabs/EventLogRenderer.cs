using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class EventLogRenderer : IDisposable
{
    private const float Padding = 20f;
    private const float CardPadding = 10f;
    private const float CardRadius = 6f;
    private const float CardHeight = 42f;
    private const float CardSpacing = 6f;
    private const float TitleLineY = CardPadding + 14f;
    private const float BodyLineY = CardPadding + 30f;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;

    private SKSize _canvasSize;
    private bool _disposed;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _banditCardPaint = new() { Color = new SKColor(80, 20, 20, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _banditBorderPaint = new() { Color = new SKColor(200, 60, 60), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _treasureFoundCardPaint = new() { Color = new SKColor(50, 40, 10, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _treasureFoundBorderPaint = new() { Color = new SKColor(200, 160, 30), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _banditDefeatedCardPaint = new() { Color = new SKColor(15, 40, 15, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _banditDefeatedBorderPaint = new() { Color = new SKColor(80, 200, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _treasureClaimedCardPaint = new() { Color = new SKColor(10, 50, 20, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _treasureClaimedBorderPaint = new() { Color = new SKColor(60, 180, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _hideoutCardPaint = new() { Color = new SKColor(40, 10, 50, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _hideoutBorderPaint = new() { Color = new SKColor(160, 60, 200), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _hideoutDestroyedCardPaint = new() { Color = new SKColor(20, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _hideoutDestroyedBorderPaint = new() { Color = new SKColor(80, 160, 200), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _civDiscoveredCardPaint = new() { Color = new SKColor(10, 40, 25, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _civDiscoveredBorderPaint = new() { Color = new SKColor(40, 180, 100), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _banditTextPaint = new() { Color = new SKColor(255, 120, 120), IsAntialias = true };
    private readonly SKPaint _banditDefeatedTextPaint = new() { Color = new SKColor(100, 220, 100), IsAntialias = true };
    private readonly SKPaint _treasureFoundTextPaint = new() { Color = new SKColor(255, 210, 60), IsAntialias = true };
    private readonly SKPaint _treasureClaimedTextPaint = new() { Color = new SKColor(80, 220, 100), IsAntialias = true };
    private readonly SKPaint _hideoutTextPaint = new() { Color = new SKColor(200, 100, 255), IsAntialias = true };
    private readonly SKPaint _hideoutDestroyedTextPaint = new() { Color = new SKColor(80, 180, 220), IsAntialias = true };
    private readonly SKPaint _civDiscoveredTextPaint = new() { Color = new SKColor(80, 220, 140), IsAntialias = true };
    private readonly SKPaint _bodyTextPaint = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKPaint _mutedPaint = new() { Color = new SKColor(120, 120, 130), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _bodyFont = new() { Size = 12, Typeface = SkiaFonts.Regular };
    private readonly SKFont _headerFont = new() { Size = 17, Typeface = SkiaFonts.Bold };

    public EventLogRenderer(GameControllerService gameControllerService, ILocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderEvents(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mainGameState) return;

        float topBarHeight = PlayerResourcesOverlayRenderer.BarHeight;
        canvas.DrawRect(new SKRect(0, topBarHeight, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        float contentWidth = Math.Min(720f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBarHeight + Padding;

        var accentPaint = new SKPaint { Color = new SKColor(255, 215, 0), IsAntialias = true };
        canvas.DrawText(_localization.Get("tab_events"), x, y + 14, _headerFont, accentPaint);
        accentPaint.Dispose();
        y += 28f;

        var eventLog = mainGameState.CurrentIslandState?.EventLog;
        if (eventLog == null || !eventLog.HasEntries)
        {
            canvas.DrawText(_localization.Get("events_empty"), x, y + 14, _bodyFont, _mutedPaint);
            return;
        }

        foreach (var entry in eventLog.Entries)
        {
            if (y + CardHeight > _canvasSize.Height - Padding) break;

            var (cardPaint, borderPaint, titlePaint, title, body) = GetEntryStyle(entry.Type);
            var cardRect = new SKRect(x, y, x + contentWidth, y + CardHeight);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, cardPaint);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, borderPaint);
            canvas.DrawText(title, x + CardPadding, y + TitleLineY, _titleFont, titlePaint);
            canvas.DrawText(body, x + CardPadding, y + BodyLineY, _bodyFont, _bodyTextPaint);
            y += CardHeight + CardSpacing;
        }
    }

    private (SKPaint card, SKPaint border, SKPaint title, string titleText, string bodyText) GetEntryStyle(GameEventType type) => type switch
    {
        GameEventType.BanditDiscovered => (
            _banditCardPaint, _banditBorderPaint, _banditTextPaint,
            _localization.Get("event_bandit_title"),
            _localization.Get("event_bandit_body")),
        GameEventType.BanditDefeated => (
            _banditDefeatedCardPaint, _banditDefeatedBorderPaint, _banditDefeatedTextPaint,
            _localization.Get("event_bandit_defeated_title"),
            _localization.Get("event_bandit_defeated_body")),
        GameEventType.TreasureTroveDiscovered => (
            _treasureFoundCardPaint, _treasureFoundBorderPaint, _treasureFoundTextPaint,
            _localization.Get("event_treasure_found_title"),
            _localization.Get("event_treasure_found_body")),
        GameEventType.TreasureTroveClaimed => (
            _treasureClaimedCardPaint, _treasureClaimedBorderPaint, _treasureClaimedTextPaint,
            _localization.Get("event_treasure_claimed_title"),
            _localization.Get("event_treasure_claimed_body")),
        GameEventType.BanditHideoutDiscovered => (
            _hideoutCardPaint, _hideoutBorderPaint, _hideoutTextPaint,
            _localization.Get("event_bandit_hideout_title"),
            _localization.Get("event_bandit_hideout_body")),
        GameEventType.BanditHideoutDestroyed => (
            _hideoutDestroyedCardPaint, _hideoutDestroyedBorderPaint, _hideoutDestroyedTextPaint,
            _localization.Get("event_bandit_hideout_destroyed_title"),
            _localization.Get("event_bandit_hideout_destroyed_body")),
        GameEventType.CivilizationDiscovered => (
            _civDiscoveredCardPaint, _civDiscoveredBorderPaint, _civDiscoveredTextPaint,
            _localization.Get("event_civilization_discovered_title"),
            _localization.Get("event_civilization_discovered_body")),
        _ => (_banditCardPaint, _banditBorderPaint, _bodyTextPaint, "?", "")
    };

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _banditCardPaint.Dispose();
        _banditBorderPaint.Dispose();
        _banditDefeatedCardPaint.Dispose();
        _banditDefeatedBorderPaint.Dispose();
        _treasureFoundCardPaint.Dispose();
        _treasureFoundBorderPaint.Dispose();
        _treasureClaimedCardPaint.Dispose();
        _treasureClaimedBorderPaint.Dispose();
        _hideoutCardPaint.Dispose();
        _hideoutBorderPaint.Dispose();
        _hideoutDestroyedCardPaint.Dispose();
        _hideoutDestroyedBorderPaint.Dispose();
        _banditTextPaint.Dispose();
        _banditDefeatedTextPaint.Dispose();
        _treasureFoundTextPaint.Dispose();
        _treasureClaimedTextPaint.Dispose();
        _hideoutTextPaint.Dispose();
        _hideoutDestroyedTextPaint.Dispose();
        _civDiscoveredCardPaint.Dispose();
        _civDiscoveredBorderPaint.Dispose();
        _civDiscoveredTextPaint.Dispose();
        _bodyTextPaint.Dispose();
        _mutedPaint.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
        _headerFont.Dispose();
        _disposed = true;
    }
}
