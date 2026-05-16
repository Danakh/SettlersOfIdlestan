using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace SettlersOfIdlestanSkia.Core
{
    internal class TooltipRenderUtils
    {
        static SKPaint _tooltipBgPaint = new SKPaint { Color = new SKColor(60, 60, 70, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
        static SKPaint _tooltipBorderPaint = new SKPaint { Color = new SKColor(220, 220, 240, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        static SKPaint _tooltipTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        public static void DrawTooltip(SKCanvas canvas, SKSize canvasSize, SKPoint pointerPosition, string text, SKFont font)
        {
            const float tooltipWidth = 200;
            const float textPadding = 8;
            const float verticalPadding = 6;

            // Étape 1 : Mesurer le texte pour obtenir les lignes et la taille
            var textLayout = SkiaTextUtils.MeasureWrappedText(text, tooltipWidth - 2 * textPadding, font);

            // Calculer la hauteur exacte du tooltip
            float tooltipHeight = textLayout.Size.Height + 2 * verticalPadding + font.Spacing / 2;

            float tooltipX = pointerPosition.X + 15;
            float tooltipY = pointerPosition.Y + 15;

            // Ajuster la position si le tooltip sort du cadre
            if (tooltipX + tooltipWidth > canvasSize.Width)
                tooltipX = pointerPosition.X - tooltipWidth - 10;
            if (tooltipY + tooltipHeight > canvasSize.Height)
                tooltipY = pointerPosition.Y - tooltipHeight - 10;

            // Afficher le rectangle du tooltip
            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, _tooltipBgPaint);
            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, _tooltipBorderPaint);

            // Étape 2 : Dessiner le texte à partir du layout
            SkiaTextUtils.DrawTextLayout(canvas, textLayout, tooltipX + textPadding, tooltipY + verticalPadding + font.Spacing, font, _tooltipTextPaint);
        }

    }
}
