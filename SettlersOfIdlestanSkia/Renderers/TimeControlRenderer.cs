using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

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
    private readonly ILocalizationService _localization;

    private HoveredControl _hoveredControl = HoveredControl.None;
    private SKPoint _lastPointerPosition;

    private SKSize _canvasSize;
    private float _rightEdge;

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
    private readonly SKFont _font = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _bankFont = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _tooltipFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private bool _disposed;

    public TimeControlRenderer(GameControllerService gameControllerService, InputHandlingService inputService, ILocalizationService localization)
    {
        _gameControllerService = gameControllerService;
        _inputService = inputService;
        _localization = localization;
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
    }

    public void Initialize(SKSize canvasSize, float rightEdge)
    {
        _canvasSize = canvasSize;
        _rightEdge = rightEdge;
        RecalcRects();
    }

    private void RecalcRects()
    {
        float barHeight = PlayerResourcesOverlayRenderer.BarHeight;
        float buttonY = (barHeight - ButtonSize) / 2f;

        // Bank display (leftmost)
        float bankLeft = _rightEdge - TotalWidth;
        _bankRect = new SKRect(bankLeft, buttonY, bankLeft + BankLabelWidth, buttonY + ButtonSize);

        // Pause button
        float p1x = bankLeft + BankLabelWidth + BankToButtonsGap;
        _pauseRect = new SKRect(p1x, buttonY, p1x + ButtonSize, buttonY + ButtonSize);

        // Play button
        float p2x = p1x + ButtonSize + ButtonSpacing;
        _playRect = new SKRect(p2x, buttonY, p2x + ButtonSize, buttonY + ButtonSize);

        // Fast-forward button
        float p3x = p2x + ButtonSize + ButtonSpacing;
        _fastRect = new SKRect(p3x, buttonY, p3x + ButtonSize, buttonY + ButtonSize);
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
            float t = (MathF.Sin(context.TotalTime * MathF.PI * 2f) + 1f) * 0.5f;
            _pauseFlickerPaint.Color = new SKColor(
                (byte)(60 + (120 - 60) * t),
                (byte)(140 + (180 - 140) * t),
                (byte)(220 + (255 - 220) * t));
            pauseBg = _pauseFlickerPaint;
        }
        DrawButton(canvas, _pauseRect, "||", speed == 0, pauseBg);
        DrawButton(canvas, _playRect, ">", speed == 1, speed == 1 ? _activePaint : _inactivePaint);
        DrawButton(canvas, _fastRect, ">>", speed == 3, speed == 3 ? _activePaint : _inactivePaint);

        DrawHoverTooltip(canvas);
    }

    private void DrawHoverTooltip(SKCanvas canvas)
    {
        string[]? lines = _hoveredControl switch
        {
            HoveredControl.Bank  => _localization.Get("timecontrol_bank_tooltip").Split('\n'),
            HoveredControl.Pause => new[] { _localization.Get("timecontrol_pause_tooltip") },
            HoveredControl.Play  => new[] { _localization.Get("timecontrol_play_tooltip") },
            HoveredControl.Fast  => new[] { _localization.Get("timecontrol_fast_tooltip") },
            _                    => null
        };
        if (lines == null) return;
        TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, lines, _tooltipFont, null, new());
    }

    private void DrawBankLabel(SKCanvas canvas, long bankTicks)
    {
        // Fond léger
        canvas.DrawRoundRect(_bankRect, 4, 4, _inactivePaint);
        canvas.DrawRoundRect(_bankRect, 4, 4, _borderPaint);

        double seconds = bankTicks / 100.0;
        string text = FormatBankTime(seconds);
        float textY = _bankRect.MidY + _bankFont.Size / 2f - 1f;
        canvas.DrawText(text, _bankRect.MidX, textY, SKTextAlign.Center, _bankFont, _bankTextPaint);
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
        canvas.DrawText(label, rect.MidX, textY, SKTextAlign.Center, _font, _textPaint);
    }

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
        if (_disposed || e.Button != PointerButton.Left) return;

        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;

        var pt = e.Position;

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
