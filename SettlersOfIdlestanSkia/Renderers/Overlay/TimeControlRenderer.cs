using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Affiche la banque de temps et les boutons Pause / Play / Fast-forward dans la barre du haut.
/// Doit être positionné à gauche de l'icône engrenage via la propriété RightEdge.
/// </summary>
public class TimeControlRenderer : IDisposable
{
    private const float ButtonSize = 26f;
    private const float ButtonSpacing = 3f;
    private const float BankLabelWidth = 72f;
    private const float BankToButtonsGap = 5f;
    private const float TotalWidth = BankLabelWidth + BankToButtonsGap + 3 * ButtonSize + 2 * ButtonSpacing;

    // Retrait depuis le bord droit de la zone réservée (juste à gauche du gear)
    public const float RequiredWidth = TotalWidth + 8f;

    private enum HoveredControl { None, Bank, Pause, Play, Fast }

    private readonly GameControllerService _gameControllerService;
    private readonly InputHandlingService _inputService;
    private readonly LocalizationService _localization;

    private HoveredControl _hoveredControl = HoveredControl.None;
    private SKPoint _lastPointerPosition;

    private SKSize _canvasSize;
    private float _rightEdge;
    private float _rowTop;
    private float _scale = 1f;

    private SKRect _pauseRect = SKRect.Empty;
    private SKRect _playRect = SKRect.Empty;
    private SKRect _fastRect = SKRect.Empty;
    private SKRect _bankRect = SKRect.Empty;

    private readonly SKPaint _activePaint = new() { Color = new SKColor(60, 140, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactivePaint = new() { Color = new SKColor(35, 35, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _pauseFlickerPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Color = new SKColor(100, 100, 130), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _bankTextPaint = new() { Color = new SKColor(200, 240, 255), IsAntialias = true };
    private SKFont _font = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private SKFont _bankFont = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private SKFont _tooltipFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private bool _disposed;

    public TimeControlRenderer(GameControllerService gameControllerService, InputHandlingService inputService, LocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _inputService = inputService;
        _localization = localization;
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
    }

    public void Initialize(SKSize canvasSize, float rightEdge, float rowTop = 0f, float scale = 1f)
    {
        _canvasSize = canvasSize;
        _rightEdge = rightEdge;
        _rowTop = rowTop;
        if (Math.Abs(scale - _scale) > 0.001f)
        {
            _scale = scale;
            _font.Dispose();  _font = new SKFont { Size = 13 * scale, Typeface = SkiaFonts.Bold };
            _bankFont.Dispose(); _bankFont = new SKFont { Size = 11 * scale, Typeface = SkiaFonts.Regular };
            _tooltipFont.Dispose(); _tooltipFont = new SKFont { Size = 10 * scale, Typeface = SkiaFonts.Regular };
        }
        RecalcRects();
    }

    private void RecalcRects()
    {
        float s = _scale;
        float rowH = _rowTop > 0f
            ? PlayerResourcesOverlayRenderer.SecondRowHeight * s
            : PlayerResourcesOverlayRenderer.BarHeight * s;
        float buttonSz = ButtonSize * s;
        float buttonY = _rowTop + (rowH - buttonSz) / 2f;

        float bankW = BankLabelWidth * s;
        float gap = BankToButtonsGap * s;
        float spacing = ButtonSpacing * s;
        float totalW = bankW + gap + 3 * buttonSz + 2 * spacing;

        // Bank display (leftmost)
        float bankLeft = _rightEdge - totalW;
        _bankRect = new SKRect(bankLeft, buttonY, bankLeft + bankW, buttonY + buttonSz);

        // Pause button
        float p1x = bankLeft + bankW + gap;
        _pauseRect = new SKRect(p1x, buttonY, p1x + buttonSz, buttonY + buttonSz);

        // Play button
        float p2x = p1x + buttonSz + spacing;
        _playRect = new SKRect(p2x, buttonY, p2x + buttonSz, buttonY + buttonSz);

        // Fast-forward button
        float p3x = p2x + buttonSz + spacing;
        _fastRect = new SKRect(p3x, buttonY, p3x + buttonSz, buttonY + buttonSz);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;

        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;

        int speed = clock.SpeedMultiplier;
        long bankTicks = clock.OfflineBankTicks;

        // Bank display
        DrawBankLabel(canvas, bankTicks);

        // Buttons
        SKPaint pauseBg = _inactivePaint;
        if (speed == 0)
        {
            float t = (float)(Math.Sin(Environment.TickCount64 / 500.0) * 0.5 + 0.5);
            _pauseFlickerPaint.Color = new SKColor(
                (byte)(35 + (160 - 35) * t),
                (byte)(35 + (100 - 35) * t),
                (byte)MathF.Round(50 - 40 * t));
            pauseBg = _pauseFlickerPaint;
        }
        bool isFast = speed > 1;
        int fastMultiplier = clock.FastMultiplier;

        DrawButton(canvas, _pauseRect, "||", speed == 0, pauseBg);
        DrawButton(canvas, _playRect, ">", speed == 1, speed == 1 ? _activePaint : _inactivePaint);
        DrawButton(canvas, _fastRect, $"x{fastMultiplier}", isFast, isFast ? _activePaint : _inactivePaint);

        DrawHoverTooltip(canvas, fastMultiplier);
    }

    private void DrawHoverTooltip(SKCanvas canvas, int fastMultiplier)
    {
        string[]? lines = _hoveredControl switch
        {
            HoveredControl.Bank  => _localization.Get("timecontrol_bank_tooltip").Split('\n'),
            HoveredControl.Pause => new[] { _localization.Get("timecontrol_pause_tooltip") },
            HoveredControl.Play  => new[] { _localization.Get("timecontrol_play_tooltip") },
            HoveredControl.Fast  => _localization.GetFormated("timecontrol_fast_tooltip", fastMultiplier).Split('\n'),
            _                    => null
        };
        if (lines == null) return;
        TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, lines, _tooltipFont, null, new(), _scale);
    }

    private void DrawBankLabel(SKCanvas canvas, long bankTicks)
    {
        // Fond léger
        canvas.DrawRoundRect(_bankRect, 4, 4, _inactivePaint);
        canvas.DrawRoundRect(_bankRect, 4, 4, _borderPaint);

        double seconds = bankTicks / 100.0;
        string text = FormatBankTime(seconds);
        float textY = _bankRect.MidY + _bankFont.Size / 2f - 1f;
        SkiaTextUtils.DrawText(canvas, text, _bankRect.MidX, textY, SKTextAlign.Center, _bankFont, _bankTextPaint);
    }

    private static string FormatBankTime(double seconds)
    {
        if (seconds >= 3600) return $"{seconds / 3600:0.#}h";
        if (seconds >= 60) return $"{seconds / 60:0.#}m";
        return $"{seconds:0.#}s";
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string label, bool isActive, SKPaint bgPaint)
    {
        canvas.DrawRoundRect(rect, 4, 4, bgPaint);
        canvas.DrawRoundRect(rect, 4, 4, _borderPaint);

        float textY = rect.MidY + _font.Size / 2f - 2f;
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, textY, SKTextAlign.Center, _font, _textPaint);
    }

    /// True if the point is over the bank display or one of the pause/play/fast buttons.
    public bool ContainsPoint(SKPoint point) =>
        _bankRect.Contains(point.X, point.Y)
        || _pauseRect.Contains(point.X, point.Y)
        || _playRect.Contains(point.X, point.Y)
        || _fastRect.Contains(point.X, point.Y);

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_disposed) return;
        _lastPointerPosition = e.Position;
        var pt = e.Position;
        if (_bankRect.Contains(pt.X, pt.Y))       _hoveredControl = HoveredControl.Bank;
        else if (_pauseRect.Contains(pt.X, pt.Y)) _hoveredControl = HoveredControl.Pause;
        else if (_playRect.Contains(pt.X, pt.Y))  _hoveredControl = HoveredControl.Play;
        else if (_fastRect.Contains(pt.X, pt.Y))  _hoveredControl = HoveredControl.Fast;
        else                                       _hoveredControl = HoveredControl.None;
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (_disposed) return;

        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;

        var pt = e.Position;

        if (e.Button == PointerButton.Right)
        {
            if (_fastRect.Contains(pt.X, pt.Y)) clock.CycleFastMultiplier();
            return;
        }
        if (e.Button != PointerButton.Left) return;

        if (_pauseRect.Contains(pt.X, pt.Y))
        {
            clock.Pause();
            return;
        }
        if (_playRect.Contains(pt.X, pt.Y))
        {
            clock.Resume();
            return;
        }
        if (_fastRect.Contains(pt.X, pt.Y))
        {
            clock.SetFast();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerMoved -= HandlePointerMoved;
        _activePaint.Dispose();
        _inactivePaint.Dispose();
        _pauseFlickerPaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _bankTextPaint.Dispose();
        _font.Dispose();
        _bankFont.Dispose();
        _tooltipFont.Dispose();
        _disposed = true;
    }
}
