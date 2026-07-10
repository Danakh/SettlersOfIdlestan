using SkiaSharp;

namespace SettlersOfIdlestanSkia.Screens;

/// <summary>
/// Dessine le titre du jeu ("Settlers of Idlestan") centré sur un canvas — utilisé par
/// l'écran de jeu (debug F11) et par d'autres générateurs d'images (ex. SOICapsuleGenerator).
/// </summary>
public static class TitleCardRenderer
{
    public static void Draw(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        const string title = "Settlers of Idlestan";
        using var titlePaint   = new SKPaint { Color = new SKColor(230, 190, 90), IsAntialias = true };
        using var dividerPaint = new SKPaint { Color = new SKColor(100, 85, 45), StrokeWidth = 2f * scale, Style = SKPaintStyle.Stroke };

        float glowBlurRadius = 6f * scale;
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 130),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowBlurRadius)
        };
        using var outlinePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f * scale,
            StrokeJoin = SKStrokeJoin.Round
        };

        float cx       = canvasSize.Width / 2f;
        float margin   = 40f * scale;
        float maxWidth = Math.Max(10f, canvasSize.Width - margin * 2f);

        const float minFontSize = 10f;
        float fontSize = 80f * scale;
        var titleFont = new SKFont { Size = fontSize, Typeface = Core.SkiaFonts.Bold };

        List<string> lines;
        float lineH, topOverhang;
        while (true)
        {
            float fullWidth = titleFont.MeasureText(title);
            lines = fullWidth <= maxWidth
                ? [title]
                : Core.SkiaTextUtils.MeasureWrappedText(title, maxWidth, titleFont).Lines;

            lineH = titleFont.Spacing;
            titleFont.GetFontMetrics(out var fontMetrics);
            topOverhang = -fontMetrics.Top;
            float totalHeight = topOverhang + outlinePaint.StrokeWidth / 2f + glowBlurRadius + lines.Count * lineH;

            float maxLineWidth = 0f;
            foreach (var line in lines)
                maxLineWidth = Math.Max(maxLineWidth, titleFont.MeasureText(line));

            if ((maxLineWidth <= maxWidth && totalHeight <= canvasSize.Height) || fontSize <= minFontSize)
                break;

            titleFont.Dispose();
            fontSize  = Math.Max(minFontSize, fontSize * 0.92f);
            titleFont = new SKFont { Size = fontSize, Typeface = Core.SkiaFonts.Bold };
        }

        using (titleFont)
        {
            float strokeMargin = outlinePaint.StrokeWidth / 2f;
            float titleY = canvasSize.Height / 2f - (lines.Count * lineH) / 2f + topOverhang + strokeMargin + glowBlurRadius;
            foreach (var line in lines)
            {
                float lineW = titleFont.MeasureText(line);
                float lineX = cx - lineW / 2f;
                Core.SkiaTextUtils.DrawText(canvas, line, lineX, titleY, titleFont, glowPaint);
                Core.SkiaTextUtils.DrawText(canvas, line, lineX, titleY, titleFont, outlinePaint);
                Core.SkiaTextUtils.DrawText(canvas, line, lineX, titleY, titleFont, titlePaint);
                titleY += lineH;
            }

            float divY     = titleY - lineH + 18f * scale;
            float divHalfW = Math.Min(220f * scale, cx - 20f * scale);
            canvas.DrawLine(cx - divHalfW, divY, cx + divHalfW, divY, dividerPaint);
        }
    }
}
