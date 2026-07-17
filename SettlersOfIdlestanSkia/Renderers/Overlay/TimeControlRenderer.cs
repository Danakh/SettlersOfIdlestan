using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Affiche la banque de temps, le toggle Pause/Lecture et le sélecteur de vitesse (x1/x3/x5/x10)
/// dans la barre du haut. Doit être positionné à gauche de l'icône engrenage via la propriété RightEdge.
/// </summary>
public class TimeControlRenderer : IDisposable
{
    private const float ButtonSize = 26f;
    private const float ButtonSpacing = 3f;
    private const float BankLabelWidth = 72f;
    private const float BankToButtonsGap = 5f;
    private const float TotalWidth = BankLabelWidth + BankToButtonsGap + 2 * ButtonSize + ButtonSpacing;

    // Retrait depuis le bord droit de la zone réservée (juste à gauche du gear)
    public const float RequiredWidth = TotalWidth + 8f;

    private static readonly int[] SpeedOptions = { 1, 3, 5, 10 };

    private enum HoveredControl { None, Bank, Toggle, Speed }

    private readonly GameControllerService _gameControllerService;
    private readonly InputHandlingService _inputService;
    private readonly LocalizationService _localization;

    private HoveredControl _hoveredControl = HoveredControl.None;
    private SKPoint _lastPointerPosition;

    private SKSize _canvasSize;
    private float _rightEdge;
    private float _rowTop;
    private float _scale = 1f;

    private bool _speedMenuOpen;
    private int _hoveredSpeedOption = -1;

    private SKRect _bankRect = SKRect.Empty;
    private SKRect _toggleRect = SKRect.Empty;
    private SKRect _speedRect = SKRect.Empty;
    private readonly SKRect[] _speedOptionRects = new SKRect[SpeedOptions.Length];

    private readonly SKPaint _activePaint = new() { Color = new SKColor(60, 140, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactivePaint = new() { Color = new SKColor(35, 35, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _hoverPaint = new() { Color = new SKColor(55, 55, 75), Style = SKPaintStyle.Fill, IsAntialias = true };
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
        float totalW = bankW + gap + 2 * buttonSz + spacing;

        // Bank display (leftmost)
        float bankLeft = _rightEdge - totalW;
        _bankRect = new SKRect(bankLeft, buttonY, bankLeft + bankW, buttonY + buttonSz);

        // Pause/Play toggle
        float p1x = bankLeft + bankW + gap;
        _toggleRect = new SKRect(p1x, buttonY, p1x + buttonSz, buttonY + buttonSz);

        // Speed selector
        float p2x = p1x + buttonSz + spacing;
        _speedRect = new SKRect(p2x, buttonY, p2x + buttonSz, buttonY + buttonSz);

        // Dropdown options, empilées sous le sélecteur de vitesse
        float optY = _speedRect.Bottom + spacing;
        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            _speedOptionRects[i] = new SKRect(_speedRect.Left, optY, _speedRect.Right, optY + buttonSz);
            optY += buttonSz + spacing;
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;

        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;

        bool isPaused = clock.SpeedMultiplier == 0;
        long bankTicks = clock.OfflineBankTicks;

        // Bank display
        DrawBankLabel(canvas, bankTicks);

        // Pause/Play toggle
        SKPaint toggleBg;
        if (isPaused)
        {
            float t = (float)(Math.Sin(Environment.TickCount64 / 500.0) * 0.5 + 0.5);
            _pauseFlickerPaint.Color = new SKColor(
                (byte)(35 + (160 - 35) * t),
                (byte)(35 + (100 - 35) * t),
                (byte)MathF.Round(50 - 40 * t));
            toggleBg = _pauseFlickerPaint;
        }
        else
        {
            toggleBg = _activePaint;
        }
        DrawButton(canvas, _toggleRect, isPaused ? "||" : ">", true, toggleBg);

        // Speed selector — affiche la vitesse choisie, en surbrillance quand elle est active (pas en pause)
        DrawButton(canvas, _speedRect, $"x{clock.ActiveSpeed}", !isPaused, !isPaused ? _activePaint : _inactivePaint);

        if (_speedMenuOpen) DrawSpeedMenu(canvas, clock.ActiveSpeed);

        if (!_speedMenuOpen) DrawHoverTooltip(canvas, clock.ActiveSpeed, isPaused);
    }

    private void DrawSpeedMenu(SKCanvas canvas, int activeSpeed)
    {
        for (int i = 0; i < SpeedOptions.Length; i++)
        {
            int value = SpeedOptions[i];
            bool isSelected = value == activeSpeed;
            SKPaint bg = isSelected ? _activePaint : (i == _hoveredSpeedOption ? _hoverPaint : _inactivePaint);
            DrawButton(canvas, _speedOptionRects[i], $"x{value}", isSelected, bg);
        }
    }

    private void DrawHoverTooltip(SKCanvas canvas, int activeSpeed, bool isPaused)
    {
        string[]? lines = _hoveredControl switch
        {
            HoveredControl.Bank   => _localization.Get("timecontrol_bank_tooltip").Split('\n'),
            HoveredControl.Toggle => (isPaused
                ? _localization.GetFormated("timecontrol_toggle_play_tooltip", activeSpeed)
                : _localization.Get("timecontrol_toggle_pause_tooltip")).Split('\n'),
            HoveredControl.Speed  => _localization.Get("timecontrol_speed_tooltip").Split('\n'),
            _                     => null
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

    /// True if the point is over the bank display, the toggle/speed buttons, or (while open) the speed dropdown.
    public bool ContainsPoint(SKPoint point)
    {
        if (_bankRect.Contains(point.X, point.Y)
            || _toggleRect.Contains(point.X, point.Y)
            || _speedRect.Contains(point.X, point.Y))
            return true;

        if (_speedMenuOpen)
        {
            foreach (var rect in _speedOptionRects)
                if (rect.Contains(point.X, point.Y)) return true;
        }
        return false;
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_disposed) return;
        _lastPointerPosition = e.Position;
        var pt = e.Position;

        if (_speedMenuOpen)
        {
            _hoveredSpeedOption = -1;
            for (int i = 0; i < _speedOptionRects.Length; i++)
            {
                if (_speedOptionRects[i].Contains(pt.X, pt.Y)) { _hoveredSpeedOption = i; break; }
            }
        }

        if (_bankRect.Contains(pt.X, pt.Y))        _hoveredControl = HoveredControl.Bank;
        else if (_toggleRect.Contains(pt.X, pt.Y)) _hoveredControl = HoveredControl.Toggle;
        else if (_speedRect.Contains(pt.X, pt.Y))  _hoveredControl = HoveredControl.Speed;
        else                                        _hoveredControl = HoveredControl.None;
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (_disposed) return;

        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;

        if (e.Button != PointerButton.Left)
        {
            _speedMenuOpen = false;
            return;
        }

        var pt = e.Position;

        if (_speedMenuOpen)
        {
            for (int i = 0; i < _speedOptionRects.Length; i++)
            {
                if (_speedOptionRects[i].Contains(pt.X, pt.Y))
                {
                    clock.SetSpeed(SpeedOptions[i]);
                    _speedMenuOpen = false;
                    return;
                }
            }
            _speedMenuOpen = false;
            return;
        }

        if (_toggleRect.Contains(pt.X, pt.Y))
        {
            if (clock.SpeedMultiplier == 0) clock.Resume();
            else clock.Pause();
            return;
        }
        if (_speedRect.Contains(pt.X, pt.Y))
        {
            _speedMenuOpen = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerMoved -= HandlePointerMoved;
        _activePaint.Dispose();
        _inactivePaint.Dispose();
        _hoverPaint.Dispose();
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
