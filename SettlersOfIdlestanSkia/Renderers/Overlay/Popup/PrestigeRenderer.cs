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
    protected override float PopupHeight   => 420;
    protected override float TitleFontSize => 20f;
    protected override float BodyFontSize  => 14f;
    protected override float BtnFontSize   => 14f;

    private const float Padding         = 18;
    private const float ButtonHeight    = 36;
    private const float SourceRowHeight = 24;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService   _localization;
    private readonly TooltipRenderer       _tooltipRenderer;
    private readonly Action                _prestigeRequested;

    private SKRect  _prestigeButtonRect  = SKRect.Empty;
    private SKRect  _closeButtonRect     = SKRect.Empty;
    private SKPoint _lastPointerPosition;

    private readonly List<(SKRect Rect, string Key)> _hoverRects = new();

    private readonly SKPaint _buttonPaint         = new() { Color = new SKColor(46, 125, 50),      Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _buttonDisabledPaint = new() { Color = new SKColor(70, 70, 70, 220),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _warningTextPaint    = new() { Color = new SKColor(220, 70, 70),       IsAntialias = true };
    private readonly SKPaint _separatorPaint      = new() { Color = new SKColor(100, 100, 110, 180), StrokeWidth = 1, Style = SKPaintStyle.Stroke };

    public PrestigeRenderer(
        GameControllerService gameControllerService,
        LocalizationService   localization,
        Action                prestigeRequested,
        TooltipRenderer       tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization          = localization;
        _prestigeRequested     = prestigeRequested;
        _tooltipRenderer       = tooltipRenderer;
    }

    public override void Initialize(SKSize canvasSize) => base.Initialize(canvasSize);

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
        double gainBonus     = controller.GetPrestigeGainBonus();
        bool showGainBonus   = gainBonus > 0;
        float gainOffset     = showGainBonus ? 28f : 0f;
        float y              = popup.Top + 68;
        float listBottom     = popup.Bottom - 152 - gainOffset;
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
        canvas.DrawLine(popup.Left + Padding, popup.Bottom - 142 - gainOffset, popup.Right - Padding, popup.Bottom - 142 - gainOffset, _separatorPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_monster_bonus"), popup.Left + Padding, popup.Bottom - 128 - gainOffset, BodyFont!, SubtlePaint);
        if (hasMonstersLeft)
            SkiaTextUtils.DrawText(canvas, "×1",   popup.Right - Padding, popup.Bottom - 128 - gainOffset, SKTextAlign.Right, BtnFont!, _warningTextPaint);
        else
            SkiaTextUtils.DrawText(canvas, "×1.2", popup.Right - Padding, popup.Bottom - 128 - gainOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
        _hoverRects.Add((new SKRect(popup.Left, popup.Bottom - 142 - gainOffset, popup.Right, popup.Bottom - 114 - gainOffset), "prestige_tooltip_monster_bonus"));

        // Wonder (affiché quand débloqué)
        if (wondersUnlocked)
        {
            canvas.DrawLine(popup.Left + Padding, popup.Bottom - 114 - gainOffset, popup.Right - Padding, popup.Bottom - 114 - gainOffset, _separatorPaint);
            var (wonderLevel, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            string duration    = FormatRunDuration(runTicks);
            string wonderLabel = _localization.GetFormated("prestige_wonder_bonus", wonderLevel, timeFactor, duration);
            SkiaTextUtils.DrawText(canvas, wonderLabel, popup.Left + Padding, popup.Bottom - 100 - gainOffset, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"×{Math.Max(1, wonderLevel * timeFactor)}", popup.Right - Padding, popup.Bottom - 100 - gainOffset, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, popup.Bottom - 114 - gainOffset, popup.Right, popup.Bottom - 86 - gainOffset), "prestige_tooltip_wonder_bonus"));
        }

        // Bonus gain de prestige (affiché quand > 0)
        if (showGainBonus)
        {
            canvas.DrawLine(popup.Left + Padding, popup.Bottom - 114, popup.Right - Padding, popup.Bottom - 114, _separatorPaint);
            SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_gain_bonus"), popup.Left + Padding, popup.Bottom - 100, BodyFont!, SubtlePaint);
            SkiaTextUtils.DrawText(canvas, $"×{1 + gainBonus:0.##}", popup.Right - Padding, popup.Bottom - 100, SKTextAlign.Right, BtnFont!, SubtlePaint);
            _hoverRects.Add((new SKRect(popup.Left, popup.Bottom - 114, popup.Right, popup.Bottom - 86), "prestige_tooltip_prestige_gain_bonus"));
        }

        // Total
        canvas.DrawLine(popup.Left + Padding, popup.Bottom - 86, popup.Right - Padding, popup.Bottom - 86, _separatorPaint);
        var total = controller.CalculatePrestigePoints();
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_total"), popup.Left + Padding, popup.Bottom - 72, BtnFont!, TextPaint);
        SkiaTextUtils.DrawText(canvas, total.ToString(), popup.Right - Padding, popup.Bottom - 72, SKTextAlign.Right, BtnFont!, TextPaint);

        bool canPrestige     = controller.PrestigeIsAvailable();
        bool hasEnoughPoints = controller.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints;
        bool hasImperialPort = controller.HasImperialPort();

        _prestigeButtonRect = new SKRect(popup.MidX - 75, popup.Bottom - Padding - ButtonHeight, popup.MidX + 75, popup.Bottom - Padding);
        canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, canPrestige ? _buttonPaint : _buttonDisabledPaint);
        SkiaTextUtils.DrawText(canvas, _localization.Get("prestige_action"), _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 5, SKTextAlign.Center, BtnFont!, TextPaint);

        if (hasEnoughPoints && !hasImperialPort)
        {
            SkiaTextUtils.DrawText(canvas,
                _localization.Get("prestige_requires_imperial_port"),
                _prestigeButtonRect.MidX,
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

        if (_prestigeButtonRect.Contains(position.X, position.Y)
            && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _prestigeRequested();
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
        _warningTextPaint.Dispose();
        _separatorPaint.Dispose();
        base.Dispose();
    }
}
