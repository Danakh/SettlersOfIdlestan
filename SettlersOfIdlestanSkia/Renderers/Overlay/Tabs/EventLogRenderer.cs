using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
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
    private readonly LocalizationService _localization;

    private SKSize _canvasSize;
    private bool _disposed;

    // Shared
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _bodyTextPaint = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKPaint _mutedPaint = new() { Color = new SKColor(120, 120, 130), IsAntialias = true };
    private readonly SKPaint _accentPaint = new() { Color = new SKColor(255, 215, 0), IsAntialias = true };

    // Danger (rouge) — bandit découvert, repaire découvert
    private readonly SKPaint _dangerCardPaint = new() { Color = new SKColor(70, 15, 15, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _dangerBorderPaint = new() { Color = new SKColor(200, 50, 50), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _dangerTextPaint = new() { Color = new SKColor(255, 100, 100), IsAntialias = true };

    // Warning (orange) — perte de ressource/unité
    private readonly SKPaint _warningCardPaint = new() { Color = new SKColor(50, 25, 5, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _warningBorderPaint = new() { Color = new SKColor(210, 100, 20), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _warningTextPaint = new() { Color = new SKColor(240, 130, 40), IsAntialias = true };

    // Success (vert) — victoire, trésor réclamé, repaire détruit
    private readonly SKPaint _successCardPaint = new() { Color = new SKColor(15, 45, 15, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _successBorderPaint = new() { Color = new SKColor(70, 200, 70), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _successTextPaint = new() { Color = new SKColor(100, 220, 100), IsAntialias = true };

    // Reward (or) — trésor découvert
    private readonly SKPaint _rewardCardPaint = new() { Color = new SKColor(50, 40, 10, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _rewardBorderPaint = new() { Color = new SKColor(200, 160, 30), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _rewardTextPaint = new() { Color = new SKColor(255, 210, 60), IsAntialias = true };

    // Discovery (cyan) — civilisation découverte
    private readonly SKPaint _discoveryCardPaint = new() { Color = new SKColor(10, 40, 30, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _discoveryBorderPaint = new() { Color = new SKColor(40, 190, 120), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _discoveryTextPaint = new() { Color = new SKColor(80, 225, 150), IsAntialias = true };

    private readonly SKFont _titleFont = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _bodyFont = new() { Size = 12, Typeface = SkiaFonts.Regular };
    private readonly SKFont _headerFont = new() { Size = 17, Typeface = SkiaFonts.Bold };

    public EventLogRenderer(GameControllerService gameControllerService, LocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderEvents(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mainGameState) return;

        float topBarHeight = PlayerResourcesOverlayRenderer.BarHeight * context.UiScale;
        canvas.DrawRect(new SKRect(0, topBarHeight, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        float contentWidth = Math.Min(720f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBarHeight + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_events"), x, y + 14, _headerFont, _accentPaint);
        y += 28f;

        var eventLog = mainGameState.CurrentWorldState?.EventLog;
        if (eventLog == null || !eventLog.HasEntries)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("events_empty"), x, y + 14, _bodyFont, _mutedPaint);
            return;
        }

        foreach (var entry in eventLog.Entries)
        {
            if (y + CardHeight > _canvasSize.Height - Padding) break;

            var (cardPaint, borderPaint, titlePaint, title, body) = GetEntryStyle(entry);
            var cardRect = new SKRect(x, y, x + contentWidth, y + CardHeight);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, cardPaint);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, borderPaint);
            SkiaTextUtils.DrawText(canvas, title, x + CardPadding, y + TitleLineY, _titleFont, titlePaint);
            SkiaTextUtils.DrawText(canvas, body, x + CardPadding, y + BodyLineY, _bodyFont, _bodyTextPaint);
            y += CardHeight + CardSpacing;
        }
    }

    private (SKPaint card, SKPaint border, SKPaint title, string titleText, string bodyText) GetEntryStyle(GameLogEntry entry) => entry.Type switch
    {
        GameEventType.RuntimeError => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_runtime_error_title"),
            entry.Message ?? ""),
        GameEventType.BanditDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_bandit_title"),
            _localization.Get("event_bandit_body")),
        GameEventType.BanditHideoutDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_bandit_hideout_title"),
            _localization.Get("event_bandit_hideout_body")),
        GameEventType.SoldierStarved => (
            _warningCardPaint, _warningBorderPaint, _warningTextPaint,
            _localization.Get("event_soldier_starved_title"),
            _localization.Get("event_soldier_starved_body")),
        GameEventType.BanditDefeated => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_bandit_defeated_title"),
            _localization.Get("event_bandit_defeated_body")),
        GameEventType.TreasureTroveClaimed => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_treasure_claimed_title"),
            _localization.Get("event_treasure_claimed_body")),
        GameEventType.BanditHideoutDestroyed => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_bandit_hideout_destroyed_title"),
            _localization.Get("event_bandit_hideout_destroyed_body")),
        GameEventType.TreasureTroveDiscovered => (
            _rewardCardPaint, _rewardBorderPaint, _rewardTextPaint,
            _localization.Get("event_treasure_found_title"),
            _localization.Get("event_treasure_found_body")),
        GameEventType.CivilizationDiscovered => (
            _discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint,
            _localization.Get("event_civilization_discovered_title"),
            _localization.Get("event_civilization_discovered_body")),
        GameEventType.WonderPlaced => (
            _discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint,
            _localization.Get("event_wonder_placed_title"),
            _localization.Get("event_wonder_placed_body")),
        GameEventType.WonderLevelUp => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_wonder_levelup_title"),
            _localization.GetFormated("event_wonder_levelup_body", entry.Message ?? "?")),
        GameEventType.RatsDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_rats_title"),
            _localization.Get("event_rats_body")),
        GameEventType.RatsDefeated => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_rats_defeated_title"),
            _localization.Get("event_rats_defeated_body")),
        GameEventType.TrollDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_troll_title"),
            _localization.Get("event_troll_body")),
        GameEventType.TrollDefeated => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_troll_defeated_title"),
            _localization.Get("event_troll_defeated_body")),
        GameEventType.OgreDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_ogre_title"),
            _localization.Get("event_ogre_body")),
        GameEventType.OgreDefeated => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_ogre_defeated_title"),
            _localization.Get("event_ogre_defeated_body")),
        GameEventType.DragonDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_dragon_discovered_title"),
            _localization.Get("event_dragon_discovered_body")),
        GameEventType.DragonDefeated => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_dragon_defeated_title"),
            _localization.Get("event_dragon_defeated_body")),
        GameEventType.MinorDemonDiscovered => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_minor_demon_discovered_title"),
            _localization.Get("event_minor_demon_discovered_body")),
        GameEventType.MinorDemonDefeated => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_minor_demon_defeated_title"),
            _localization.Get("event_minor_demon_defeated_body")),
        GameEventType.DeepestMinePlaced => (
            _discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint,
            _localization.Get("event_deepest_mine_placed_title"),
            _localization.Get("event_deepest_mine_placed_body")),
        GameEventType.DeepestMineDug => (
            _discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint,
            _localization.Get("event_deepest_mine_dug_title"),
            _localization.Get("event_deepest_mine_dug_body")),
        GameEventType.FairyCircleDiscovered => (
            _rewardCardPaint, _rewardBorderPaint, _rewardTextPaint,
            _localization.Get("event_fairy_circle_title"),
            _localization.Get("event_fairy_circle_body")),
        GameEventType.RitualCollapsed => (
            _warningCardPaint, _warningBorderPaint, _warningTextPaint,
            _localization.Get("event_ritual_collapsed_title"),
            _localization.Get("event_ritual_collapsed_body")),
        GameEventType.UnderworldLost => (
            _dangerCardPaint, _dangerBorderPaint, _dangerTextPaint,
            _localization.Get("event_underworld_lost_title"),
            _localization.Get("event_underworld_lost_body")),
        GameEventType.CorruptionSpirePlaced => (
            _discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint,
            _localization.Get("event_corruption_spire_placed_title"),
            _localization.Get("event_corruption_spire_placed_body")),
        GameEventType.CorruptionSpireBuilt => (
            _successCardPaint, _successBorderPaint, _successTextPaint,
            _localization.Get("event_corruption_spire_built_title"),
            _localization.Get("event_corruption_spire_built_body")),
        GameEventType.AdventurerDiscovered => (
            _discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint,
            _localization.Get("event_adventurer_title"),
            _localization.Get("event_adventurer_body")),
        GameEventType.AdventurerDefeated => (
            _warningCardPaint, _warningBorderPaint, _warningTextPaint,
            _localization.Get("event_adventurer_defeated_title"),
            _localization.Get("event_adventurer_defeated_body")),
        _ => (_dangerCardPaint, _dangerBorderPaint, _bodyTextPaint, "?", entry.Message ?? "")
    };

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _bodyTextPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _dangerCardPaint.Dispose();
        _dangerBorderPaint.Dispose();
        _dangerTextPaint.Dispose();
        _warningCardPaint.Dispose();
        _warningBorderPaint.Dispose();
        _warningTextPaint.Dispose();
        _successCardPaint.Dispose();
        _successBorderPaint.Dispose();
        _successTextPaint.Dispose();
        _rewardCardPaint.Dispose();
        _rewardBorderPaint.Dispose();
        _rewardTextPaint.Dispose();
        _discoveryCardPaint.Dispose();
        _discoveryBorderPaint.Dispose();
        _discoveryTextPaint.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
        _headerFont.Dispose();
        _disposed = true;
    }
}
