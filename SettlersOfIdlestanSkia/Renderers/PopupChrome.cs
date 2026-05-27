using SettlersOfIdlestanSkia.Core;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Couleurs et dessin partagés entre tous les popups du jeu.
/// </summary>
public sealed class PopupChrome : IDisposable
{
    // ── Couleurs de référence ────────────────────────────────────────────────────
    public static readonly SKColor BackgroundColor = new(24, 24, 30, 245);
    public static readonly SKColor BorderColor     = SKColors.Gold;
    public static readonly SKColor OverlayColor    = new(0, 0, 0, 120);
    public static readonly SKColor CloseBtnColor   = new(90, 50, 50, 230);

    // ── Constantes de layout ────────────────────────────────────────────────────
    public const float CornerRadius  = 10f;
    public const float CloseSize     = 28f;
    public const float CloseMargin   = 10f;

    private readonly SKPaint _bgPaint      = new() { Color = BackgroundColor, Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _borderPaint  = new() { Color = BorderColor,     StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _overlayPaint = new() { Color = OverlayColor,    Style = SKPaintStyle.Fill };
    private readonly SKPaint _closeBgPaint = new() { Color = CloseBtnColor,   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _closeXPaint  = new() { Color = SKColors.White,  IsAntialias = true };
    private readonly SKFont  _closeFont    = new() { Size = 14, Typeface = SkiaFonts.Bold };

    private bool _disposed;

    /// <summary>Dessine le fond plein-écran + fond du popup + bordure.</summary>
    public void DrawBackground(SKCanvas canvas, SKRect popup, SKSize canvasSize)
    {
        canvas.DrawRect(new SKRect(0, 0, canvasSize.Width, canvasSize.Height), _overlayPaint);
        canvas.DrawRoundRect(popup, CornerRadius, CornerRadius, _bgPaint);
        canvas.DrawRoundRect(popup, CornerRadius, CornerRadius, _borderPaint);
    }

    /// <summary>Retourne le rect standard de la croix de fermeture (coin haut-droit).</summary>
    public static SKRect GetCloseRect(SKRect popup) =>
        new(popup.Right - CloseMargin - CloseSize,
            popup.Top  + CloseMargin,
            popup.Right - CloseMargin,
            popup.Top  + CloseMargin + CloseSize);

    /// <summary>Dessine la croix de fermeture.</summary>
    public void DrawCloseButton(SKCanvas canvas, SKRect rect)
    {
        canvas.DrawRoundRect(rect, 5, 5, _closeBgPaint);
        canvas.DrawText("X", rect.MidX, rect.MidY + 6, SKTextAlign.Center, _closeFont, _closeXPaint);
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
