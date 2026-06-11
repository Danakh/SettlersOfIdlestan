using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Magic;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

/// <summary>
/// Écran Rituels : liste des rituels connus, lancement/arrêt, réglage de la puissance,
/// coûts en cristaux et capacité des Tours de Mages.
/// </summary>
public sealed class RitualsRenderer : IDisposable
{
    private const float Padding = 20f;
    private const float RowHeight = 78f;
    private const float RowSpacing = 8f;
    private const float ButtonWidth = 76f;
    private const float ButtonHeight = 26f;
    private const float PowerButtonSize = 26f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;

    private SKSize _canvasSize;
    private bool _disposed;
    private SKPoint _hoverPosition;

    private readonly List<(RitualId id, SKRect launchRect, SKRect minusRect, SKRect plusRect)> _buttonRects = new();

    private readonly SKPaint _bgPaint           = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardPaint         = new() { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardActivePaint   = new() { Color = new SKColor(35, 30, 55, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _cardBorderPaint   = new() { Color = new SKColor(60, 60, 80), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _cardActiveBorder  = new() { Color = new SKColor(140, 100, 220), StrokeWidth = 1.4f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _launchPaint       = new() { Color = new SKColor(90, 60, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _launchHoverPaint  = new() { Color = new SKColor(115, 80, 195), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _stopPaint         = new() { Color = new SKColor(120, 55, 55), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _stopHoverPaint    = new() { Color = new SKColor(150, 70, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _disabledPaint     = new() { Color = new SKColor(60, 60, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonBorderPaint = new() { Color = new SKColor(120, 120, 140), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _buttonTextPaint   = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _namePaint         = new() { Color = new SKColor(230, 230, 240), IsAntialias = true };
    private readonly SKPaint _descPaint         = new() { Color = new SKColor(150, 150, 165), IsAntialias = true };
    private readonly SKPaint _costPaint         = new() { Color = new SKColor(170, 150, 220), IsAntialias = true };
    private readonly SKPaint _mutedPaint        = new() { Color = new SKColor(110, 110, 125), IsAntialias = true };
    private readonly SKPaint _accentPaint       = new() { Color = new SKColor(190, 150, 255), IsAntialias = true };
    private readonly SKPaint _summaryPaint      = new() { Color = new SKColor(200, 200, 215), IsAntialias = true };

    private readonly SKFont _headerFont = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private readonly SKFont _nameFont   = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont   = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _buttonFont = new() { Size = 11, Typeface = SkiaFonts.Bold };
    private readonly SKFont _powerFont  = new() { Size = 14, Typeface = SkiaFonts.Bold };

    public RitualsRenderer(GameControllerService gameControllerService, LocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void RenderRitualsPage(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        _buttonRects.Clear();

        float topBar = PlayerResourcesOverlayRenderer.BarHeight * context.UiScale;
        canvas.DrawRect(new SKRect(0, topBar, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        float contentWidth = Math.Min(640f, _canvasSize.Width - Padding * 2);
        float x = (_canvasSize.Width - contentWidth) / 2;
        float y = topBar + Padding;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_rituals"), x, y + 14, _headerFont, _accentPaint);
        y += 32f;

        var civ = _gameControllerService.PlayerCivilization;
        var magic = _gameControllerService.MainGameController.MagicController;
        if (civ == null) return;

        // ── Résumé : tours, rituels actifs, puissance, cristaux ───────────────
        int crystals = civ.GetResourceQuantity(Resource.Crystal);
        string summary = _localization.GetFormated("rituals_summary",
            magic.MageTowerCount, magic.ActiveRituals.Count, magic.MaxActiveRituals,
            magic.UsedPower, magic.TotalPowerBudget, crystals);
        SkiaTextUtils.DrawText(canvas, summary, x, y + 12, _descFont, _summaryPaint);
        y += 26f;

        if (magic.MageTowerCount == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("rituals_no_towers"), x, y + 12, _descFont, _mutedPaint);
            y += 26f;
        }

        var known = magic.GetKnownRituals();
        if (known.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("rituals_none_known"), x, y + 12, _descFont, _mutedPaint);
            return;
        }

        foreach (var def in known)
        {
            if (y + RowHeight > _canvasSize.Height - Padding) break;
            y += DrawRitualRow(canvas, x, y, contentWidth, def, magic) + RowSpacing;
        }
    }

    private float DrawRitualRow(SKCanvas canvas, float x, float y, float width,
        RitualDefinition def, SettlersOfIdlestan.Controller.Magic.MagicController magic)
    {
        var active = magic.GetActiveRitual(def.Id);
        bool isActive = active != null;

        var cardRect = new SKRect(x, y, x + width, y + RowHeight);
        canvas.DrawRoundRect(cardRect, 6, 6, isActive ? _cardActivePaint : _cardPaint);
        canvas.DrawRoundRect(cardRect, 6, 6, isActive ? _cardActiveBorder : _cardBorderPaint);

        float textX = x + 14f;
        SkiaTextUtils.DrawText(canvas, _localization.Get(def.NameKey), textX, y + 19, _nameFont, _namePaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get(def.DescKey), textX, y + 37, _descFont, _descPaint);

        // Coûts : lancement (puissance 1) ou entretien courant
        string costText = isActive
            ? _localization.GetFormated("ritual_upkeep_cost", magic.GetUpkeepCost(def, active!.Power))
            : _localization.GetFormated("ritual_launch_cost",
                SettlersOfIdlestan.Controller.Magic.MagicController.GetLaunchCost(def, 1));
        SkiaTextUtils.DrawText(canvas, costText, textX, y + 58, _descFont, _costPaint);

        // ── Bouton Lancer / Arrêter ────────────────────────────────────────────
        float buttonX = x + width - ButtonWidth - 14f;
        float buttonY = y + 12f;
        var launchRect = new SKRect(buttonX, buttonY, buttonX + ButtonWidth, buttonY + ButtonHeight);
        bool hovered = launchRect.Contains(_hoverPosition.X, _hoverPosition.Y);

        SKPaint buttonPaint;
        string buttonLabel;
        if (isActive)
        {
            buttonPaint = hovered ? _stopHoverPaint : _stopPaint;
            buttonLabel = _localization.Get("ritual_button_stop");
        }
        else if (magic.CanLaunchRitual(def.Id))
        {
            buttonPaint = hovered ? _launchHoverPaint : _launchPaint;
            buttonLabel = _localization.Get("ritual_button_launch");
        }
        else
        {
            buttonPaint = _disabledPaint;
            buttonLabel = _localization.Get("ritual_button_launch");
        }
        canvas.DrawRoundRect(launchRect, 5, 5, buttonPaint);
        canvas.DrawRoundRect(launchRect, 5, 5, _buttonBorderPaint);
        SkiaTextUtils.DrawText(canvas, buttonLabel, launchRect.MidX, launchRect.MidY + 4, SKTextAlign.Center, _buttonFont, _buttonTextPaint);

        // ── Puissance − / + (rituel actif uniquement) ─────────────────────────
        var minusRect = SKRect.Empty;
        var plusRect = SKRect.Empty;
        if (isActive)
        {
            float powerY = buttonY + ButtonHeight + 8f;
            minusRect = new SKRect(buttonX, powerY, buttonX + PowerButtonSize, powerY + PowerButtonSize);
            plusRect  = new SKRect(buttonX + ButtonWidth - PowerButtonSize, powerY,
                                   buttonX + ButtonWidth, powerY + PowerButtonSize);

            bool minusHover = minusRect.Contains(_hoverPosition.X, _hoverPosition.Y);
            bool plusHover  = plusRect.Contains(_hoverPosition.X, _hoverPosition.Y);
            bool canPlus    = magic.CanIncreaseRitualPower(def.Id);

            canvas.DrawRoundRect(minusRect, 5, 5, minusHover ? _stopHoverPaint : _stopPaint);
            canvas.DrawRoundRect(minusRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, "−", minusRect.MidX, minusRect.MidY + 5, SKTextAlign.Center, _powerFont, _buttonTextPaint);

            canvas.DrawRoundRect(plusRect, 5, 5, canPlus ? (plusHover ? _launchHoverPaint : _launchPaint) : _disabledPaint);
            canvas.DrawRoundRect(plusRect, 5, 5, _buttonBorderPaint);
            SkiaTextUtils.DrawText(canvas, "+", plusRect.MidX, plusRect.MidY + 5, SKTextAlign.Center, _powerFont, _buttonTextPaint);

            SkiaTextUtils.DrawText(canvas, active!.Power.ToString(),
                (minusRect.Right + plusRect.Left) / 2f, minusRect.MidY + 5, SKTextAlign.Center, _powerFont, _accentPaint);
        }

        _buttonRects.Add((def.Id, launchRect, minusRect, plusRect));
        return RowHeight;
    }

    public void HandlePointerMoved(SKPoint position) => _hoverPosition = position;

    public bool HandlePointerPressed(SKPoint position)
    {
        var magic = _gameControllerService.MainGameController.MagicController;

        foreach (var (id, launchRect, minusRect, plusRect) in _buttonRects)
        {
            if (!launchRect.IsEmpty && launchRect.Contains(position.X, position.Y))
            {
                if (magic.GetActiveRitual(id) != null) magic.StopRitual(id);
                else magic.LaunchRitual(id);
                return true;
            }
            if (!minusRect.IsEmpty && minusRect.Contains(position.X, position.Y))
            {
                magic.DecreaseRitualPower(id);
                return true;
            }
            if (!plusRect.IsEmpty && plusRect.Contains(position.X, position.Y))
            {
                magic.IncreaseRitualPower(id);
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _cardPaint.Dispose();
        _cardActivePaint.Dispose();
        _cardBorderPaint.Dispose();
        _cardActiveBorder.Dispose();
        _launchPaint.Dispose();
        _launchHoverPaint.Dispose();
        _stopPaint.Dispose();
        _stopHoverPaint.Dispose();
        _disabledPaint.Dispose();
        _buttonBorderPaint.Dispose();
        _buttonTextPaint.Dispose();
        _namePaint.Dispose();
        _descPaint.Dispose();
        _costPaint.Dispose();
        _mutedPaint.Dispose();
        _accentPaint.Dispose();
        _summaryPaint.Dispose();
        _headerFont.Dispose();
        _nameFont.Dispose();
        _descFont.Dispose();
        _buttonFont.Dispose();
        _powerFont.Dispose();
        _disposed = true;
    }
}
