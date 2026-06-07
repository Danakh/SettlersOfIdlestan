using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PrestigeRenderer : IDisposable
{
    private const float PopupWidth = 460;
    private const float PopupHeight = 420;
    private const float Padding = 18;
    private const float ButtonHeight = 36;
    private const float CloseSize = 28;
    private const float SourceRowHeight = 24;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly Action _prestigeRequested;
    private SKSize _canvasSize;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _closeButtonRect = SKRect.Empty;
    private SKPoint _lastPointerPosition;
    private bool _disposed;

    private readonly List<(SKRect Rect, string Key)> _hoverRects = new();

    private readonly PopupChrome _chrome = new();
    private readonly SKPaint _buttonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _buttonDisabledPaint = new() { Color = new SKColor(70, 70, 70, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedTextPaint = new() { Color = new SKColor(190, 190, 195), IsAntialias = true };
    private readonly SKPaint _warningTextPaint = new() { Color = new SKColor(220, 70, 70), IsAntialias = true };
    private readonly SKPaint _separatorPaint = new() { Color = new SKColor(100, 100, 110, 180), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
    private readonly SKFont _titleFont = new() { Size = 20, Typeface = SkiaFonts.Bold };
    private readonly SKFont _font = new() { Size = 14, Typeface = SkiaFonts.Regular };
    private readonly SKFont _boldFont = new() { Size = 14, Typeface = SkiaFonts.Bold };

    public bool IsOpen { get; private set; }

    public PrestigeRenderer(GameControllerService gameControllerService, LocalizationService localization, Action prestigeRequested, TooltipRenderer tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _prestigeRequested = prestigeRequested;
        _tooltipRenderer = tooltipRenderer;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void HandlePointerMoved(SKPoint position)
    {
        if (!IsOpen) return;
        _lastPointerPosition = position;
    }

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || _disposed)
            return;

        var popup = GetPopupRect();
        _chrome.DrawBackground(canvas, popup, _canvasSize);

        canvas.DrawText(_localization.Get("prestige_title"), popup.MidX, popup.Top + 30, SKTextAlign.Center, _titleFont, _textPaint);

        _closeButtonRect = PopupChrome.GetCloseRect(popup);
        _chrome.DrawCloseButton(canvas, _closeButtonRect);

        var controller = _gameControllerService.MainGameController.PrestigeController;
        var sources = controller.GetPrestigePointSources();
        bool wondersUnlocked = controller.WondersUnlocked();
        double gainBonus = controller.GetPrestigeGainBonus();
        bool showGainBonus = gainBonus > 0;
        float gainOffset = showGainBonus ? 28f : 0f;
        float y = popup.Top + 68;
        // listBottom : réserve de place pour monstre + gain (optionnel) + wonder (optionnel) + total
        float listBottom = popup.Bottom - 152 - gainOffset;
        int maxVisibleSources = Math.Max(0, (int)((listBottom - y) / SourceRowHeight));

        _hoverRects.Clear();

        foreach (var source in sources.Take(maxVisibleSources))
        {
            canvas.DrawText(_localization.Get(source.LabelKey), popup.Left + Padding, y, _font, _textPaint);
            canvas.DrawText(source.Points.ToString(), popup.Right - Padding, y, SKTextAlign.Right, _boldFont, _textPaint);
            if (source.TooltipKey != null)
                _hoverRects.Add((new SKRect(popup.Left, y - _font.Size, popup.Right, y + 6), source.TooltipKey));
            y += SourceRowHeight;
        }

        int hiddenSourceCount = sources.Count - maxVisibleSources;
        if (hiddenSourceCount > 0)
        {
            canvas.DrawText(string.Format(_localization.Get("prestige_more_sources"), hiddenSourceCount), popup.Left + Padding, y, _font, _mutedTextPaint);
        }

        // Monstres
        bool hasMonstersLeft = controller.HasSurfaceMonsters();
        canvas.DrawLine(popup.Left + Padding, popup.Bottom - 142 - gainOffset, popup.Right - Padding, popup.Bottom - 142 - gainOffset, _separatorPaint);
        canvas.DrawText(_localization.Get("prestige_monster_bonus"), popup.Left + Padding, popup.Bottom - 128 - gainOffset, _font, _mutedTextPaint);
        if (hasMonstersLeft)
            canvas.DrawText("×1", popup.Right - Padding, popup.Bottom - 128 - gainOffset, SKTextAlign.Right, _boldFont, _warningTextPaint);
        else
            canvas.DrawText("×1.2", popup.Right - Padding, popup.Bottom - 128 - gainOffset, SKTextAlign.Right, _boldFont, _mutedTextPaint);
        _hoverRects.Add((new SKRect(popup.Left, popup.Bottom - 142 - gainOffset, popup.Right, popup.Bottom - 114 - gainOffset), "prestige_tooltip_monster_bonus"));

        // Wonder (shown when unlocked)
        if (wondersUnlocked)
        {
            canvas.DrawLine(popup.Left + Padding, popup.Bottom - 114 - gainOffset, popup.Right - Padding, popup.Bottom - 114 - gainOffset, _separatorPaint);
            var (wonderLevel, timeFactor, runTicks) = controller.GetWonderBonusDetails();
            string duration = FormatRunDuration(runTicks);
            string wonderLabel = _localization.GetFormated("prestige_wonder_bonus", wonderLevel, timeFactor, duration);
            canvas.DrawText(wonderLabel, popup.Left + Padding, popup.Bottom - 100 - gainOffset, _font, _mutedTextPaint);
            canvas.DrawText($"×{Math.Max(1, wonderLevel * timeFactor)}", popup.Right - Padding, popup.Bottom - 100 - gainOffset, SKTextAlign.Right, _boldFont, _mutedTextPaint);
            _hoverRects.Add((new SKRect(popup.Left, popup.Bottom - 114 - gainOffset, popup.Right, popup.Bottom - 86 - gainOffset), "prestige_tooltip_wonder_bonus"));
        }

        // Bonus gain de prestige (shown when > 0)
        if (showGainBonus)
        {
            canvas.DrawLine(popup.Left + Padding, popup.Bottom - 114, popup.Right - Padding, popup.Bottom - 114, _separatorPaint);
            canvas.DrawText(_localization.Get("prestige_gain_bonus"), popup.Left + Padding, popup.Bottom - 100, _font, _mutedTextPaint);
            canvas.DrawText($"×{1 + gainBonus:0.##}", popup.Right - Padding, popup.Bottom - 100, SKTextAlign.Right, _boldFont, _mutedTextPaint);
            _hoverRects.Add((new SKRect(popup.Left, popup.Bottom - 114, popup.Right, popup.Bottom - 86), "prestige_tooltip_prestige_gain_bonus"));
        }

        // Total (fixed position, always well above the button)
        canvas.DrawLine(popup.Left + Padding, popup.Bottom - 86, popup.Right - Padding, popup.Bottom - 86, _separatorPaint);
        var total = controller.CalculatePrestigePoints();
        canvas.DrawText(_localization.Get("prestige_total"), popup.Left + Padding, popup.Bottom - 72, _boldFont, _textPaint);
        canvas.DrawText(total.ToString(), popup.Right - Padding, popup.Bottom - 72, SKTextAlign.Right, _boldFont, _textPaint);

        bool canPrestige = controller.PrestigeIsAvailable();
        bool hasEnoughPoints = controller.CalculatePrestigePoints() >= PrestigeController.PrestigeRequiredPoints;
        bool hasImperialPort = controller.HasImperialPort();

        _prestigeButtonRect = new SKRect(popup.MidX - 75, popup.Bottom - Padding - ButtonHeight, popup.MidX + 75, popup.Bottom - Padding);
        canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, canPrestige ? _buttonPaint : _buttonDisabledPaint);
        canvas.DrawText(_localization.Get("prestige_action"), _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 5, SKTextAlign.Center, _boldFont, _textPaint);

        if (hasEnoughPoints && !hasImperialPort)
        {
            canvas.DrawText(
                _localization.Get("prestige_requires_imperial_port"),
                _prestigeButtonRect.MidX,
                _prestigeButtonRect.Bottom + 18,
                SKTextAlign.Center,
                _font,
                _mutedTextPaint);
        }

        // Tooltip on hover
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
        if (!IsOpen)
            return false;

        if (button != PointerButton.Left)
            return GetPopupRect().Contains(position.X, position.Y);

        if (_closeButtonRect.Contains(position.X, position.Y))
        {
            Close();
            return true;
        }

        if (_prestigeButtonRect.Contains(position.X, position.Y) && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _prestigeRequested();
            return true;
        }

        if (!GetPopupRect().Contains(position.X, position.Y))
        {
            Close();
            return false;
        }

        return true;
    }

    private static string FormatRunDuration(long ticks)
    {
        int totalMinutes = (int)(ticks / 6000);
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        if (hours > 0 && minutes > 0) return $"{hours}h{minutes:D2}m";
        if (hours > 0) return $"{hours}h";
        return $"{Math.Max(1, minutes)}m";
    }

    private SKRect GetPopupRect()
    {
        float width = Math.Min(PopupWidth, _canvasSize.Width - 30);
        float height = Math.Min(PopupHeight, _canvasSize.Height - 30);
        float x = (_canvasSize.Width - width) / 2;
        float y = (_canvasSize.Height - height) / 2;
        return new SKRect(x, y, x + width, y + height);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _chrome.Dispose();
        _buttonPaint.Dispose();
        _buttonDisabledPaint.Dispose();
        _textPaint.Dispose();
        _mutedTextPaint.Dispose();
        _warningTextPaint.Dispose();
        _separatorPaint.Dispose();
        _titleFont.Dispose();
        _font.Dispose();
        _boldFont.Dispose();
        _disposed = true;
    }
}
