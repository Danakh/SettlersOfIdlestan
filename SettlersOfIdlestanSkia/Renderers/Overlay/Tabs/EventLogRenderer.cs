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
    private readonly UILayoutService _uiLayout;

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

    public EventLogRenderer(GameControllerService gameControllerService, LocalizationService localization, UILayoutService uiLayout)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _uiLayout = uiLayout;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderEvents(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState mainGameState) return;

        float topBarHeight = _uiLayout.SecondRowBottom;
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

            var (tone, title, body) = GetEntryContent(entry);
            var (cardPaint, borderPaint, titlePaint) = PaintsFor(tone);
            var cardRect = new SKRect(x, y, x + contentWidth, y + CardHeight);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, cardPaint);
            canvas.DrawRoundRect(cardRect, CardRadius, CardRadius, borderPaint);
            SkiaTextUtils.DrawText(canvas, title, x + CardPadding, y + TitleLineY, _titleFont, titlePaint);
            SkiaTextUtils.DrawText(canvas, body, x + CardPadding, y + BodyLineY, _bodyFont, _bodyTextPaint);
            y += CardHeight + CardSpacing;
        }
    }

    /// <summary>Paints correspondant à un ton, pour le rendu Skia.</summary>
    private (SKPaint Card, SKPaint Border, SKPaint Title) PaintsFor(EventLogTone tone) => tone switch
    {
        EventLogTone.Warning   => (_warningCardPaint,   _warningBorderPaint,   _warningTextPaint),
        EventLogTone.Success   => (_successCardPaint,   _successBorderPaint,   _successTextPaint),
        EventLogTone.Reward    => (_rewardCardPaint,    _rewardBorderPaint,    _rewardTextPaint),
        EventLogTone.Discovery => (_discoveryCardPaint, _discoveryBorderPaint, _discoveryTextPaint),
        _                      => (_dangerCardPaint,    _dangerBorderPaint,    _dangerTextPaint),
    };

    /// <summary>
    /// Instantané de l'onglet pour une vue portée par l'hôte. Réutilise <see cref="GetEntryContent"/> :
    /// le classement des dizaines de types d'événement en cinq tons n'existe qu'à un seul endroit.
    /// </summary>
    /// <param name="isVisible">L'onglet Journal est-il actif ? La règle appartient à
    /// <c>OverlayRenderer</c>, qui détient l'onglet courant.</param>
    public EventLogSnapshot GetSnapshot(bool isVisible)
    {
        if (_disposed || !isVisible) return EventLogSnapshot.Hidden;

        var eventLog = _gameControllerService.CurrentGameState?.CurrentWorldState?.EventLog;

        var entries = new List<EventLogEntrySnapshot>();
        if (eventLog != null)
        {
            // Pas de troncature ici, contrairement au rendu Skia qui s'arrête au bas de l'écran :
            // la vue défile, et affiche donc les 50 entrées que le modèle conserve.
            foreach (var entry in eventLog.Entries)
            {
                var (tone, title, body) = GetEntryContent(entry);
                entries.Add(new EventLogEntrySnapshot(title, body, tone));
            }
        }

        return new EventLogSnapshot(
            IsVisible: true,
            Title: _localization.Get("tab_events"),
            EmptyMessage: _localization.Get("events_empty"),
            Entries: entries);
    }

    private (EventLogTone Tone, string Title, string Body) GetEntryContent(GameLogEntry entry) => entry.Type switch
    {
        GameEventType.RuntimeError => (
            EventLogTone.Danger,
            _localization.Get("event_runtime_error_title"),
            entry.Message ?? ""),
        GameEventType.BanditDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_bandit_title"),
            _localization.Get("event_bandit_body")),
        GameEventType.BanditHideoutDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_bandit_hideout_title"),
            _localization.Get("event_bandit_hideout_body")),
        GameEventType.SoldierStarved => (
            EventLogTone.Warning,
            _localization.Get("event_soldier_starved_title"),
            _localization.Get("event_soldier_starved_body")),
        GameEventType.BanditDefeated => (
            EventLogTone.Success,
            _localization.Get("event_bandit_defeated_title"),
            _localization.Get("event_bandit_defeated_body")),
        GameEventType.TreasureTroveClaimed => (
            EventLogTone.Success,
            _localization.Get("event_treasure_claimed_title"),
            _localization.Get("event_treasure_claimed_body")),
        GameEventType.BanditHideoutDestroyed => (
            EventLogTone.Success,
            _localization.Get("event_bandit_hideout_destroyed_title"),
            _localization.Get("event_bandit_hideout_destroyed_body")),
        GameEventType.TreasureTroveDiscovered => (
            EventLogTone.Reward,
            _localization.Get("event_treasure_found_title"),
            _localization.Get("event_treasure_found_body")),
        GameEventType.CivilizationDiscovered => (
            EventLogTone.Discovery,
            _localization.Get("event_civilization_discovered_title"),
            _localization.Get("event_civilization_discovered_body")),
        GameEventType.CivilizationDestroyed => (
            EventLogTone.Success,
            _localization.Get("event_civilization_destroyed_title"),
            _localization.Get("event_civilization_destroyed_body")),
        GameEventType.WonderPlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_wonder_placed_title"),
            _localization.Get("event_wonder_placed_body")),
        GameEventType.WonderLevelUp => (
            EventLogTone.Success,
            _localization.Get("event_wonder_levelup_title"),
            _localization.GetFormated("event_wonder_levelup_body", entry.Message ?? "?")),
        GameEventType.GreatLighthousePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_great_lighthouse_placed_title"),
            _localization.Get("event_great_lighthouse_placed_body")),
        GameEventType.GreatLighthouseLevelUp => (
            EventLogTone.Success,
            _localization.Get("event_great_lighthouse_levelup_title"),
            _localization.GetFormated("event_great_lighthouse_levelup_body", entry.Message ?? "?")),
        GameEventType.RatsDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_rats_title"),
            _localization.Get("event_rats_body")),
        GameEventType.RatsDefeated => (
            EventLogTone.Success,
            _localization.Get("event_rats_defeated_title"),
            _localization.Get("event_rats_defeated_body")),
        GameEventType.TrollDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_troll_title"),
            _localization.Get("event_troll_body")),
        GameEventType.TrollDefeated => (
            EventLogTone.Success,
            _localization.Get("event_troll_defeated_title"),
            _localization.Get("event_troll_defeated_body")),
        GameEventType.OgreDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_ogre_title"),
            _localization.Get("event_ogre_body")),
        GameEventType.OgreDefeated => (
            EventLogTone.Success,
            _localization.Get("event_ogre_defeated_title"),
            _localization.Get("event_ogre_defeated_body")),
        GameEventType.DragonDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_dragon_discovered_title"),
            _localization.Get("event_dragon_discovered_body")),
        GameEventType.DragonDefeated => (
            EventLogTone.Success,
            _localization.Get("event_dragon_defeated_title"),
            _localization.Get("event_dragon_defeated_body")),
        GameEventType.MinorDemonDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_minor_demon_discovered_title"),
            _localization.Get("event_minor_demon_discovered_body")),
        GameEventType.VolcanoDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_volcano_discovered_title"),
            _localization.Get("event_volcano_discovered_body")),
        GameEventType.MinorDemonDefeated => (
            EventLogTone.Success,
            _localization.Get("event_minor_demon_defeated_title"),
            _localization.Get("event_minor_demon_defeated_body")),
        GameEventType.MajorDemonDiscovered => (
            EventLogTone.Danger,
            _localization.Get("event_major_demon_discovered_title"),
            _localization.Get("event_major_demon_discovered_body")),
        GameEventType.MajorDemonDefeated => (
            EventLogTone.Success,
            _localization.Get("event_major_demon_defeated_title"),
            _localization.Get("event_major_demon_defeated_body")),
        GameEventType.DeepestMinePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_deepest_mine_placed_title"),
            _localization.Get("event_deepest_mine_placed_body")),
        GameEventType.DeepestMineDug => (
            EventLogTone.Discovery,
            _localization.Get("event_deepest_mine_dug_title"),
            _localization.Get("event_deepest_mine_dug_body")),
        GameEventType.FairyCircleDiscovered => (
            EventLogTone.Reward,
            _localization.Get("event_fairy_circle_title"),
            _localization.Get("event_fairy_circle_body")),
        GameEventType.RitualCollapsed => (
            EventLogTone.Warning,
            _localization.Get("event_ritual_collapsed_title"),
            _localization.Get("event_ritual_collapsed_body")),
        GameEventType.UnderworldLost => (
            EventLogTone.Danger,
            _localization.Get("event_underworld_lost_title"),
            _localization.Get("event_underworld_lost_body")),
        GameEventType.CityLostToTerrain => (
            EventLogTone.Danger,
            _localization.Get("event_city_lost_to_terrain_title"),
            _localization.Get("event_city_lost_to_terrain_body")),
        GameEventType.CorruptionSpirePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_corruption_spire_placed_title"),
            _localization.Get("event_corruption_spire_placed_body")),
        GameEventType.CorruptionSpireBuilt => (
            EventLogTone.Success,
            _localization.Get("event_corruption_spire_built_title"),
            _localization.Get("event_corruption_spire_built_body")),
        GameEventType.CorruptionSpireDestroyed => (
            _warningCardPaint, _warningBorderPaint, _warningTextPaint,
            _localization.Get("event_corruption_spire_destroyed_title"),
            _localization.Get("event_corruption_spire_destroyed_body")),
        GameEventType.CorruptionSpireRadiusUpgraded => (
            EventLogTone.Success,
            _localization.Get("event_corruption_spire_radius_upgraded_title"),
            _localization.GetFormated("event_corruption_spire_radius_upgraded_body", entry.Message ?? "?")),
        GameEventType.AdventurerDiscovered => (
            EventLogTone.Discovery,
            _localization.Get("event_adventurer_title"),
            _localization.Get("event_adventurer_body")),
        GameEventType.AdventurerDefeated => (
            EventLogTone.Warning,
            _localization.Get("event_adventurer_defeated_title"),
            _localization.Get("event_adventurer_defeated_body")),
        GameEventType.RaidMissingBarracks => (
            EventLogTone.Warning,
            _localization.Get("event_raid_missing_barracks_title"),
            _localization.Get("event_raid_missing_barracks_body")),
        GameEventType.WarHeraldAutoReinforcementConflict => (
            EventLogTone.Warning,
            _localization.Get("event_war_herald_auto_reinforcement_conflict_title"),
            _localization.Get("event_war_herald_auto_reinforcement_conflict_body")),
        GameEventType.AbyssGateEligible => (
            EventLogTone.Reward,
            _localization.Get("event_abyss_gate_eligible_title"),
            _localization.Get("event_abyss_gate_eligible_body")),
        GameEventType.AbyssGatePlaced => (
            EventLogTone.Discovery,
            _localization.Get("event_abyss_gate_placed_title"),
            _localization.Get("event_abyss_gate_placed_body")),
        GameEventType.AbyssGateBuilt => (
            EventLogTone.Success,
            _localization.Get("event_abyss_gate_built_title"),
            _localization.Get("event_abyss_gate_built_body")),
        GameEventType.DivineBonesPurified => (
            EventLogTone.Reward,
            _localization.Get("event_divine_bones_purified_title"),
            _localization.Get("event_divine_bones_purified_body")),
        GameEventType.DivineBonesPurifiedNoEssence => (
            EventLogTone.Discovery,
            _localization.Get("event_divine_bones_purified_no_essence_title"),
            _localization.Get("event_divine_bones_purified_no_essence_body")),
        _ => (EventLogTone.Danger, "?", entry.Message ?? "")
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
