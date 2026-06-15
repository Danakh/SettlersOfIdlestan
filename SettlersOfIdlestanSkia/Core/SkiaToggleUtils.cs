using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Dessine un toggle pill vert/rouge avec bouton blanc glissant, partagé entre tous les écrans.
/// </summary>
public static class SkiaToggleUtils
{
    private static readonly SKPaint OnPaint                  = new() { Color = new SKColor(46,  125,  50),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint OnHoverPaint             = new() { Color = new SKColor(60,  150,  64),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint OffPaint                 = new() { Color = new SKColor(160,  50,  50),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint OffHoverPaint            = new() { Color = new SKColor(185,  65,  65),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint IndeterminatePaint       = new() { Color = new SKColor( 90,  90, 105),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint IndeterminateHoverPaint  = new() { Color = new SKColor(110, 110, 125),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint DimPaint                 = new() { Color = new SKColor( 70,  70,  80),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private static readonly SKPaint BorderPaint              = new() { Color = new SKColor(180, 180, 200),  StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private static readonly SKPaint KnobPaint                = new() { Color = SKColors.White,              Style = SKPaintStyle.Fill,   IsAntialias = true };

    /// <summary>
    /// Dessine un toggle. isOn=null → état indéterminé (bouton centré, gris).
    /// isDimmed → fond gris, bouton centré (bâtiment inactif ou non disponible).
    /// </summary>
    public static void Draw(SKCanvas canvas, SKRect rect, bool? isOn, bool isHovered, bool isDimmed = false)
    {
        float radius = rect.Height / 2f;
        float knobR  = radius * 0.72f;
        float knobCy = rect.MidY;

        SKPaint fill;
        float knobCx;

        if (isDimmed)
        {
            fill   = DimPaint;
            knobCx = rect.MidX;
        }
        else if (isOn == null)
        {
            fill   = isHovered ? IndeterminateHoverPaint : IndeterminatePaint;
            knobCx = rect.MidX;
        }
        else if (isOn.Value)
        {
            fill   = isHovered ? OnHoverPaint : OnPaint;
            knobCx = rect.Right - radius - rect.Height * 0.04f;
        }
        else
        {
            fill   = isHovered ? OffHoverPaint : OffPaint;
            knobCx = rect.Left + radius + rect.Height * 0.04f;
        }

        canvas.DrawRoundRect(rect, radius, radius, fill);
        canvas.DrawRoundRect(rect, radius, radius, BorderPaint);
        canvas.DrawCircle(knobCx, knobCy, knobR, KnobPaint);
    }

    /// <summary>Surcharge sans état indéterminé ni dim.</summary>
    public static void Draw(SKCanvas canvas, SKRect rect, bool isOn, bool isHovered)
        => Draw(canvas, rect, (bool?)isOn, isHovered, false);
}
