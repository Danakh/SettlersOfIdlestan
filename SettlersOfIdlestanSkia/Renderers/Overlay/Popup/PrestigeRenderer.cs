using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PrestigeRenderer : PopupRendererBase
{
    protected override float PopupWidth    => 460;
    protected override float PopupHeight   => 448 + (ShowTierPicker ? 44 : 0);
    protected override float TitleFontSize => 20f;
    protected override float BodyFontSize  => 14f;
    protected override float BtnFontSize   => 14f;

    private const float Padding         = 18;
    private const float ButtonHeight    = 36;
    private const float SourceRowHeight = 24;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService   _localization;
    private readonly TooltipRenderer       _tooltipRenderer;
    private readonly Action<bool>          _prestigeRequested;

    private SKRect  _prestigeButtonRect          = SKRect.Empty;
    private SKRect  _corruptedPrestigeButtonRect = SKRect.Empty;
    private SKRect  _closeButtonRect     = SKRect.Empty;
    private SKRect  _tierMinusButtonRect = SKRect.Empty;
    private SKRect  _tierPlusButtonRect  = SKRect.Empty;
    private SKPoint _lastPointerPosition;

    private bool ShowTierPicker
        => _gameControllerService.MainGameController.PrestigeController.CanChooseNextIslandTier();

    private readonly List<(SKRect Rect, string Key)> _hoverRects = new();
    private SKFont? _smallFont;

    private readonly SKPaint _buttonPaint         = new() { Color = new SKColor(46, 125, 50),      Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _buttonDisabledPaint = new() { Color = new SKColor(70, 70, 70, 220),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _corruptedButtonPaint = new() { Color = new SKColor(120, 40, 150),     Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _warningTextPaint    = new() { Color = new SKColor(220, 70, 70),       IsAntialias = true };
    private readonly SKPaint _separatorPaint      = new() { Color = new SKColor(100, 100, 110, 180), StrokeWidth = 1, Style = SKPaintStyle.Stroke };

    public PrestigeRenderer(
        GameControllerService gameControllerService,
        LocalizationService   localization,
        Action<bool>          prestigeRequested,
        TooltipRenderer       tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization          = localization;
        _prestigeRequested     = prestigeRequested;
        _tooltipRenderer       = tooltipRenderer;
    }

    public override void Initialize(SKSize canvasSize) => base.Initialize(canvasSize);

    protected override void OnFontsUpdated(float s)
    {
        _smallFont?.Dispose();
        _smallFont = new SKFont { Size = 9 * s, Typeface = SkiaFonts.Regular };
    }

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;
        _lastPointerPosition = position;
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || Disposed) return;

        var popup = GetPopupRect();
        DrawBackground(canvas, popup);

        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_title"), popup.MidX, popup.Top + 30, SKTextAlign.Center, TitleFont!, TextPaint);

        _closeButtonRect = GetCloseRect(popup);
        DrawCloseButton(canvas, _closeButtonRect);

        var controller       = _gameControllerService.MainGameController.PrestigeController;
        var sources          = controller.GetPrestigePointSources();
        bool wondersUnlocked = controller.WondersUnlocked();
        bool showSpireBonus      = controller.HasCorruptionSpireBuilt();
        double gainBonus         = controller.GetPrestigeGainBonus();
        double seaportBonus      = controller.GetSeaportPrestigeBonus();
        double civDestroyedBonus = controller.GetCivilizationsDestroyedBonus();
        int tier                 = controller.GetTier();
        double tierBonus         = controller.GetTierBonus();
        double observatoryBonus  = controller.GetObservatoryPrestigeBonus();
        bool showGainBonus       = gainBonus > 0;
        bool showSeaportBonus    = seaportBonus > 0;
        bool showCivBonus        = civDestroyedBonus > 0;
        bool showObservatoryBonus = observatoryBonus > 0;
        const float tierOffset   = 28f; // toujours affiché
        float gainOffset         = showGainBonus    ? 28f : 0f;
        float seaportOffset      = showSeaportBonus ? 28f : 0f;
        float civOffset          = showCivBonus     ? 28f : 0f;
        float spireOffset        = showSpireBonus   ? 28f : 0f;
        float observatoryOffset  = showObservatoryBonus ? 28f : 0f;
        float belowWonderOffset  = gainOffset + seaportOffset + civOffset + spireOffset + tierOffset + observatoryOffset;
        bool showTierPicker  = ShowTierPicker;
        float tierPickerOffset = showTierPicker ? 44f : 0f;
        float contentBottom  = popup.Bottom - tierPickerOffset;
        float y              = popup.Top + 68;
        float listBottom     = contentBottom - 152 - belowWonderOffset;
        int maxVisibleSources = Math.Max(0, (int)((listBottom - y) / SourceRowHeight));

        _hoverRects.Clear();

        foreach (var source in sources.Take(maxVisibleSources))
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get(source.LabelKey), popup.Left + Padding, y, BodyFont!, TextPaint);
            SkiaTextUtils.DrawText(canvas, source.Points.ToString(), popup.Right - Padding, y, SKTextAlign.Right, BtnFont!, TextPaint);
            if (source.TooltipKey != null)
                _hoverRects.Add((new SKRect(popup.Left, y - BodyFont!.Size, popup.Right, y + 6), source.TooltipKey));
            y += SourceRowHeight;
        }

        int hiddenSourceCount = sources.Count - maxVisibleSources;
        if (hiddenSourceCount > 0)
            SkiaTextUtils.DrawText(canvas, string.Format(_localization.Get("prestige_more_sources"), hiddenSourceCount), popup.Left + Padding, y, BodyFont!, SubtlePaint);

        // Monstres
        bool hasMonstersLeft = controller.HasSurfaceMonsters();
        canvas.DrawLine(popup.Left + Padding, contentBottom - 142 - belowWonderOffset, popup.Right - Padding, contentBottom - 142 - belowWonderOffset, _separatorPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_monster_bonus"), popup.Left + Padding, contentBottom - 128 - belowWonderOffset, BodyFont!, SubtlePaint);
        if (hasMonstersLeft)
            SkiaTextUtils.DrawText(canvas, "×1",   popup.Right - Padding, contentBottom - 128 - belowWonderOffset, SKTextAlign.Right, BtnFont!, _warningTextPaint);
        else
            SkiaTextUtils.DrawText(canvas, "×1.2", popup.Right - Padding, contentBottom - 128 - belowWonderOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
        _hoverRects.Add((new SKRect(popup.Left, contentBottom - 142 - belowWonderOffset, popup.Right, contentBottom - 114 - belowWonderOffset), "prestige_tooltip_monster_bonus"));

        // Wonder (affiché quand débloqué)
        if (wondersUnlocked)
        {
            canvas.DrawLine(popup.Left + Padding, contentBottom - 114 - belowWonderOffset, popup.Right - Padding, contentBottom - 114 - belowWonderOffset, _separatorPaint);
            var (wonderLevel, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            string duration    = FormatRunDuration(runTicks);
            string wonderLabel = _localization.GetFormated("prestige_wonder_bonus", wonderLevel, timeFactor, duration);
            SkiaTextUtils.DrawText(canvas, wonderLabel, popup.Left + Padding, contentBottom - 100 - belowWonderOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"×{Math.Max(1, wonderLevel * timeFactor)}", popup.Right - Padding, contentBottom - 100 - belowWonderOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114 - belowWonderOffset, popup.Right, contentBottom - 86 - belowWonderOffset), "prestige_tooltip_wonder_bonus"));
        }

        // Observatoire (affiché quand niveau > 0)
        if (showObservatoryBonus)
        {
            float rowOffset = gainOffset + seaportOffset + civOffset + spireOffset + tierOffset;
            canvas.DrawLine(popup.Left + Padding, contentBottom - 114 - rowOffset, popup.Right - Padding, contentBottom - 114 - rowOffset, _separatorPaint);
            string observatoryLabel = _localization.GetFormated("prestige_observatory_bonus", controller.GetObservatoryLevel());
            SkiaTextUtils.DrawText(canvas, observatoryLabel, popup.Left + Padding, contentBottom - 100 - rowOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"+{observatoryBonus * 100:0}%", popup.Right - Padding, contentBottom - 100 - rowOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114 - rowOffset, popup.Right, contentBottom - 86 - rowOffset), "prestige_tooltip_observatory_bonus"));
        }

        // Spire de Corruption (affichée quand construite)
        if (showSpireBonus)
        {
            float spireRowOffset = gainOffset + seaportOffset + civOffset + tierOffset;
            canvas.DrawLine(popup.Left + Padding, contentBottom - 114 - spireRowOffset, popup.Right - Padding, contentBottom - 114 - spireRowOffset, _separatorPaint);
            string spireLabel = _localization.GetFormated("prestige_corruption_spire_bonus", controller.GetCorruptionLevel());
            SkiaTextUtils.DrawText(canvas, spireLabel, popup.Left + Padding, contentBottom - 100 - spireRowOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"×{controller.GetCorruptionSpireMultiplier()}", popup.Right - Padding, contentBottom - 100 - spireRowOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114 - spireRowOffset, popup.Right, contentBottom - 86 - spireRowOffset), "prestige_tooltip_corruption_spire_bonus"));
        }

        // Bonus gain de prestige (affiché quand > 0)
        if (showGainBonus)
        {
            float rowOffset = seaportOffset + civOffset + tierOffset;
            canvas.DrawLine(popup.Left + Padding, contentBottom - 114 - rowOffset, popup.Right - Padding, contentBottom - 114 - rowOffset, _separatorPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_gain_bonus"), popup.Left + Padding, contentBottom - 100 - rowOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"×{1 + gainBonus:0.##}", popup.Right - Padding, contentBottom - 100 - rowOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114 - rowOffset, popup.Right, contentBottom - 86 - rowOffset), "prestige_tooltip_prestige_gain_bonus"));
        }

        // Bonus Ports niv. 4 (affiché quand > 0)
        if (showSeaportBonus)
        {
            int portCount = controller.GetSeaportLevel4Count();
            float rowOffset = civOffset + tierOffset;
            canvas.DrawLine(popup.Left + Padding, contentBottom - 114 - rowOffset, popup.Right - Padding, contentBottom - 114 - rowOffset, _separatorPaint);
            SkiaTextUtils.DrawText(canvas, _localization.GetFormated("prestige_seaport_bonus", portCount), popup.Left + Padding, contentBottom - 100 - rowOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"+{seaportBonus * 100:0}%", popup.Right - Padding, contentBottom - 100 - rowOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114 - rowOffset, popup.Right, contentBottom - 86 - rowOffset), "prestige_tooltip_seaport_bonus"));
        }

        // Bonus civilisations détruites (affiché quand > 0)
        if (showCivBonus)
        {
            int civCount = controller.GetCivilizationsDestroyedCount();
            canvas.DrawLine(popup.Left + Padding, contentBottom - 114 - tierOffset, popup.Right - Padding, contentBottom - 114 - tierOffset, _separatorPaint);
            SkiaTextUtils.DrawText(canvas, _localization.GetFormated("prestige_civilizations_destroyed_bonus", civCount), popup.Left + Padding, contentBottom - 100 - tierOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"+{civDestroyedBonus * 100:0}%", popup.Right - Padding, contentBottom - 100 - tierOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114 - tierOffset, popup.Right, contentBottom - 86 - tierOffset), "prestige_tooltip_civilizations_destroyed_bonus"));
        }

        // Bonus de palier (Tier) — toujours affiché
        canvas.DrawLine(popup.Left + Padding, contentBottom - 114, popup.Right - Padding, contentBottom - 114, _separatorPaint);
        SkiaTextUtils.DrawText(canvas, _localization.GetFormated("prestige_tier_bonus", tier), popup.Left + Padding, contentBottom - 100, BodyFont!, SubtlePaint);
        SkiaTextUtils.DrawText(canvas, $"+{tierBonus * 100:0}%", popup.Right - Padding, contentBottom - 100, SKTextAlign.Right, BtnFont!, SubtlePaint);
        _hoverRects.Add((new SKRect(popup.Left, contentBottom - 114, popup.Right, contentBottom - 86), "prestige_tooltip_tier_bonus"));

        // Total
        canvas.DrawLine(popup.Left + Padding, contentBottom - 86, popup.Right - Padding, contentBottom - 86, _separatorPaint);
        var total = controller.CalculatePrestigePoints();
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_total"), popup.Left + Padding, contentBottom - 72, BtnFont!, TextPaint);
        SkiaTextUtils.DrawText(canvas, total.ToString(), popup.Right - Padding, contentBottom - 72, SKTextAlign.Right, BtnFont!, TextPaint);

        // Tier de la prochaine île (Observatoire niveau 3) — dans l'espace réservé sous le Total
        if (showTierPicker)
        {
            int minTier = tier;
            int maxTier = minTier + PrestigeController.MaxNextIslandTierChoiceBonus;
            int chosenTier = controller.GetNextIslandTierChoice();
            float btnSize = 24f;
            float rowY = popup.Bottom - Padding - ButtonHeight - 8 - btnSize;
            _tierMinusButtonRect = new SKRect(popup.Left + Padding, rowY, popup.Left + Padding + btnSize, rowY + btnSize);
            _tierPlusButtonRect  = new SKRect(popup.Right - Padding - btnSize, rowY, popup.Right - Padding, rowY + btnSize);
            canvas.DrawRoundRect(_tierMinusButtonRect, 4, 4, chosenTier > minTier ? _buttonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, "-", _tierMinusButtonRect.MidX, _tierMinusButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);
            canvas.DrawRoundRect(_tierPlusButtonRect, 4, 4, chosenTier < maxTier ? _buttonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, "+", _tierPlusButtonRect.MidX, _tierPlusButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);
            SkiaTextUtils.DrawText(canvas, _localization.GetFormated("prestige_next_island_tier_picker", chosenTier),
                popup.MidX, rowY + btnSize / 2 + 5, SKTextAlign.Center, BodyFont!, TextPaint);
            _hoverRects.Add((new SKRect(popup.Left, rowY - 4, popup.Right, rowY + btnSize + 4), "tooltip_prestige_next_island_tier_picker"));
        }
        else
        {
            _tierMinusButtonRect = SKRect.Empty;
            _tierPlusButtonRect  = SKRect.Empty;
        }

        bool canPrestige     = controller.PrestigeIsAvailable();
        bool hasEnoughPoints = controller.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints;
        bool hasImperialPort = controller.HasImperialPort();
        bool hasSpire        = controller.HasCorruptionSpireBuilt();

        if (hasSpire)
        {
            const float gap  = 10;
            const float btnW = 150;
            _prestigeButtonRect = new SKRect(popup.MidX - btnW - gap / 2, popup.Bottom - Padding - ButtonHeight, popup.MidX - gap / 2, popup.Bottom - Padding);
            _corruptedPrestigeButtonRect = new SKRect(popup.MidX + gap / 2, popup.Bottom - Padding - ButtonHeight, popup.MidX + gap / 2 + btnW, popup.Bottom - Padding);

            canvas.DrawRoundRect(_corruptedPrestigeButtonRect, 7, 7, canPrestige ? _corruptedButtonPaint : _buttonDisabledPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_corrupted_action"), _corruptedPrestigeButtonRect.MidX, _corruptedPrestigeButtonRect.MidY - 1, SKTextAlign.Center, BtnFont!, TextPaint);
            int currentCorruptionLevel = controller.GetCorruptionLevel();
            SkiaTextUtils.DrawText(canvas, $"{currentCorruptionLevel} -> {currentCorruptionLevel + 1}", _corruptedPrestigeButtonRect.MidX, _corruptedPrestigeButtonRect.MidY + 13, SKTextAlign.Center, _smallFont!, TextPaint);

            _hoverRects.Add((_corruptedPrestigeButtonRect, "prestige_tooltip_corrupted_action"));
        }
        else
        {
            _corruptedPrestigeButtonRect = SKRect.Empty;
            _prestigeButtonRect = new SKRect(popup.MidX - 75, popup.Bottom - Padding - ButtonHeight, popup.MidX + 75, popup.Bottom - Padding);
        }

        canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, canPrestige ? _buttonPaint : _buttonDisabledPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_action"), _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);

        if (hasEnoughPoints && !hasImperialPort)
        {
            SkiaTextUtils.DrawText(canvas,
                _localization.Get("prestige_requires_imperial_port"),
                popup.MidX,
                _prestigeButtonRect.Bottom + 18,
                SKTextAlign.Center,
                BodyFont!, SubtlePaint);
        }

        foreach (var (rect, key) in _hoverRects)
        {
            if (rect.Contains(_lastPointerPosition.X, _lastPointerPosition.Y))
            {
                _tooltipRenderer.SetTooltip(_localization.Get(key), new SKPoint(rect.Right, rect.Top));
                break;
            }
        }
    }

    public bool HandlePointerPressed(SKPoint position, PointerButton button)
    {
        if (!IsOpen) return false;

        if (button != PointerButton.Left)
            return GetPopupRect().Contains(position.X, position.Y);

        if (_closeButtonRect.Contains(position.X, position.Y)) { Close(); return true; }

        var prestigeController = _gameControllerService.MainGameController.PrestigeController;

        if (!_tierMinusButtonRect.IsEmpty && _tierMinusButtonRect.Contains(position.X, position.Y))
        {
            prestigeController.SetNextIslandTierChoice(prestigeController.GetNextIslandTierChoice() - 1);
            return true;
        }

        if (!_tierPlusButtonRect.IsEmpty && _tierPlusButtonRect.Contains(position.X, position.Y))
        {
            prestigeController.SetNextIslandTierChoice(prestigeController.GetNextIslandTierChoice() + 1);
            return true;
        }

        if (_prestigeButtonRect.Contains(position.X, position.Y)
            && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _prestigeRequested(false);
            return true;
        }

        if (!_corruptedPrestigeButtonRect.IsEmpty && _corruptedPrestigeButtonRect.Contains(position.X, position.Y)
            && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _prestigeRequested(true);
            return true;
        }

        if (!GetPopupRect().Contains(position.X, position.Y)) { Close(); return false; }
        return true;
    }

    private static string FormatRunDuration(long ticks)
    {
        int totalMinutes = (int)(ticks / 6000);
        int hours   = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (hours > 0 && minutes > 0) return $"{hours}h{minutes:D2}m";
        if (hours > 0) return $"{hours}h";
        return $"{Math.Max(1, minutes)}m";
    }

    private SKRect GetPopupRect()
    {
        float width  = Math.Min(PopupWidth,  CanvasSize.Width  - 30);
        float height = Math.Min(PopupHeight, CanvasSize.Height - 30);
        float x = (CanvasSize.Width  - width)  / 2;
        float y = (CanvasSize.Height - height) / 2;
        return new SKRect(x, y, x + width, y + height);
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _buttonPaint.Dispose();
        _buttonDisabledPaint.Dispose();
        _corruptedButtonPaint.Dispose();
        _warningTextPaint.Dispose();
        _separatorPaint.Dispose();
        _smallFont?.Dispose();
        base.Dispose();
    }
}
