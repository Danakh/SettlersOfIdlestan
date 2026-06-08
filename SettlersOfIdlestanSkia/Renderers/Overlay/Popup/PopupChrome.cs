using SettlersOfIdlestanSkia.Core;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PopupChrome : IDisposable
{
    // ── Couleurs de référence ────────────────────────────────────────────────────
    public static readonly SKColor BackgroundColor = new(24, 24, 30, 245);
    public static readonly SKColor BorderColor     = SKColors.Gold;
    public static readonly SKColor OverlayColor    = new(0, 0, 0, 120);
    public static readonly SKColor CloseBtnColor   = new(90, 50, 50, 230);

    // ── Constantes de layout (valeurs de base à scale=1) ────────────────────────
    public const float CornerRadius  = 10f;
    public const float CloseSize     = 28f;
    public const float CloseMargin   = 10f;

    private readonly SKPaint _bgPaint      = new() { Color = BackgroundColor, Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _borderPaint  = new() { Color = BorderColor,     StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _overlayPaint = new() { Color = OverlayColor,    Style = SKPaintStyle.Fill };
    private readonly SKPaint _closeBgPaint = new() { Color = CloseBtnColor,   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _closeXPaint  = new() { Color = SKColors.White,  IsAntialias = true };
    private SKFont  _closeFont    = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private float   _lastFontScale = 0f;

    private bool _disposed;

    public void DrawBackground(SKCanvas canvas, SKRect popup, SKSize canvasSize, float s = 1f)
    {
        canvas.DrawRect(new SKRect(0, 0, canvasSize.Width, canvasSize.Height), _overlayPaint);
        canvas.DrawRoundRect(popup, CornerRadius * s, CornerRadius * s, _bgPaint);
        canvas.DrawRoundRect(popup, CornerRadius * s, CornerRadius * s, _borderPaint);
    }

    public static SKRect GetCloseRect(SKRect popup, float s = 1f) =>
        new(popup.Right - (CloseMargin + CloseSize) * s,
            popup.Top  + CloseMargin * s,
            popup.Right - CloseMargin * s,
            popup.Top  + (CloseMargin + CloseSize) * s);

    public void DrawCloseButton(SKCanvas canvas, SKRect rect, float s = 1f)
    {
        if (s != _lastFontScale)
        {
            _lastFontScale = s;
            _closeFont.Dispose();
            _closeFont = new SKFont { Size = 14 * s, Typeface = SkiaFonts.Bold };
        }
        canvas.DrawRoundRect(rect, 5 * s, 5 * s, _closeBgPaint);
        SkiaTextUtils.DrawText(canvas, "X", rect.MidX, rect.MidY + 6 * s, SKTextAlign.Center, _closeFont, _closeXPaint);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _overlayPaint.Dispose();
        _closeBgPaint.Dispose();
        _closeXPaint.Dispose();
        _closeFont.Dispose();
        _disposed = true;
    }
}
