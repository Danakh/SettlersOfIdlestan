using SettlersOfIdlestanSkia.Core;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Classe de base pour tous les renderers de popup.
/// Absorbe les fonctionnalités de PopupChrome et fournit les paints/polices communs.
/// </summary>
public abstract class PopupRendererBase : IDisposable
{
    // ── Chrome layout (références aux constantes de PopupChrome) ─────────────────
    protected const float ChromeCornerRadius = PopupChrome.CornerRadius;
    protected const float ChromeCloseSize    = PopupChrome.CloseSize;
    protected const float ChromeCloseMargin  = PopupChrome.CloseMargin;

    // ── Chrome paints ────────────────────────────────────────────────────────────
    private readonly SKPaint _bgPaint      = new() { Color = PopupChrome.BackgroundColor, Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _borderPaint  = new() { Color = PopupChrome.BorderColor,     StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _overlayPaint = new() { Color = PopupChrome.OverlayColor,    Style = SKPaintStyle.Fill };
    private readonly SKPaint _closeBgPaint = new() { Color = PopupChrome.CloseBtnColor,   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _closeXPaint  = new() { Color = SKColors.White,               IsAntialias = true };

    // ── Paints communs ───────────────────────────────────────────────────────────
    protected readonly SKPaint TextPaint      = new() { Color = SKColors.White,              IsAntialias = true };
    protected readonly SKPaint SubtlePaint    = new() { Color = new SKColor(180, 180, 190), IsAntialias = true };
    protected readonly SKPaint BtnBorderPaint = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };

    // ── Polices communes ─────────────────────────────────────────────────────────
    protected SKFont? TitleFont;
    protected SKFont? BodyFont;
    protected SKFont? BtnFont;
    private   SKFont? _closeFont;
    private   float   _lastFontScale;

    // ── État partagé ─────────────────────────────────────────────────────────────
    protected SKSize CanvasSize;
    protected bool   Disposed;
    protected bool   JustOpened;

    public bool IsOpen { get; protected set; }

    // ── Dimensions du popup (à surcharger) ───────────────────────────────────────
    protected virtual float PopupWidth  => 0;
    protected virtual float PopupHeight => 0;
    protected virtual float TitleFontSize => 16f;
    protected virtual float BodyFontSize  => 13f;
    protected virtual float BtnFontSize   => 13f;

    // ── Cycle de vie ─────────────────────────────────────────────────────────────
    public virtual void Initialize(SKSize canvasSize)
    {
        CanvasSize = canvasSize;
        UpdateFonts(1f);
    }

    public virtual void Open()
    {
        IsOpen     = true;
        JustOpened = true;
    }

    public virtual void Close() => IsOpen = false;

    // ── Gestion de l'échelle ─────────────────────────────────────────────────────
    protected float ComputeScale(float requestedScale)
    {
        const float margin = 20f;
        return Math.Min(requestedScale, Math.Min(
            (CanvasSize.Width  - margin) / PopupWidth,
            (CanvasSize.Height - margin) / PopupHeight));
    }

    protected void UpdateFonts(float s)
    {
        if (s == _lastFontScale) return;
        _lastFontScale = s;
        TitleFont?.Dispose(); TitleFont  = new SKFont { Size = TitleFontSize * s, Typeface = SkiaFonts.Bold };
        BodyFont?.Dispose();  BodyFont   = new SKFont { Size = BodyFontSize  * s, Typeface = SkiaFonts.Regular };
        BtnFont?.Dispose();   BtnFont    = new SKFont { Size = BtnFontSize   * s, Typeface = SkiaFonts.Bold };
        _closeFont?.Dispose(); _closeFont = new SKFont { Size = 14 * s,           Typeface = SkiaFonts.Bold };
        OnFontsUpdated(s);
    }

    protected virtual void OnFontsUpdated(float s) { }

    // ── Dessin du chrome ─────────────────────────────────────────────────────────
    protected SKRect GetCenteredRect(float s = 1f)
    {
        float w = PopupWidth  * s;
        float h = PopupHeight * s;
        float x = (CanvasSize.Width  - w) / 2;
        float y = (CanvasSize.Height - h) / 2;
        return new SKRect(x, y, x + w, y + h);
    }

    /// <summary>Dessine l'overlay plein écran + le fond et la bordure du popup.</summary>
    protected void DrawBackground(SKCanvas canvas, SKRect popup, float s = 1f)
    {
        canvas.DrawRect(new SKRect(0, 0, CanvasSize.Width, CanvasSize.Height), _overlayPaint);
        canvas.DrawRoundRect(popup, ChromeCornerRadius * s, ChromeCornerRadius * s, _bgPaint);
        canvas.DrawRoundRect(popup, ChromeCornerRadius * s, ChromeCornerRadius * s, _borderPaint);
    }

    /// <summary>Dessine uniquement le fond et la bordure, sans overlay (ex : About).</summary>
    protected void DrawBackgroundOnly(SKCanvas canvas, SKRect popup, float cornerRadius)
    {
        canvas.DrawRoundRect(popup, cornerRadius, cornerRadius, _bgPaint);
        canvas.DrawRoundRect(popup, cornerRadius, cornerRadius, _borderPaint);
    }

    protected static SKRect GetCloseRect(SKRect popup, float s = 1f) =>
        new(popup.Right - (ChromeCloseMargin + ChromeCloseSize) * s,
            popup.Top   + ChromeCloseMargin * s,
            popup.Right - ChromeCloseMargin * s,
            popup.Top   + (ChromeCloseMargin + ChromeCloseSize) * s);

    protected void DrawCloseButton(SKCanvas canvas, SKRect rect, float s = 1f)
    {
        canvas.DrawRoundRect(rect, 5 * s, 5 * s, _closeBgPaint);
        SkiaTextUtils.DrawText(canvas, "X", rect.MidX, rect.MidY + 6 * s, SKTextAlign.Center, _closeFont!, _closeXPaint);
    }

    /// <summary>Dessine un bouton arrondi avec fond, bordure et texte centré (via BtnFont + TextPaint).</summary>
    protected void DrawButton(SKCanvas canvas, SKRect rect, SKPaint fillPaint, string label, float s = 1f)
    {
        canvas.DrawRoundRect(rect, 6 * s, 6 * s, fillPaint);
        canvas.DrawRoundRect(rect, 6 * s, 6 * s, BtnBorderPaint);
        float tw = BtnFont!.MeasureText(label);
        SkiaTextUtils.DrawText(canvas, label,
            rect.Left + (rect.Width  - tw)          / 2f,
            rect.Top  + (rect.Height + BtnFont.Size) / 2f,
            BtnFont, TextPaint);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────────
    public virtual void Dispose()
    {
        if (Disposed) return;
        TextPaint.Dispose();
        SubtlePaint.Dispose();
        BtnBorderPaint.Dispose();
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _overlayPaint.Dispose();
        _closeBgPaint.Dispose();
        _closeXPaint.Dispose();
        TitleFont?.Dispose();
        BodyFont?.Dispose();
        BtnFont?.Dispose();
        _closeFont?.Dispose();
        Disposed = true;
    }
}
