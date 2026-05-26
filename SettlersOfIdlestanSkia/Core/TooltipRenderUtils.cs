using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Model.IslandMap;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Core
{
    internal class TooltipRenderUtils
    {
        static SKPaint _tooltipBgPaint = new SKPaint { Color = new SKColor(60, 60, 70, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
        static SKPaint _tooltipBorderPaint = new SKPaint { Color = new SKColor(220, 220, 240, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        static SKPaint _tooltipTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        public static void DrawTooltip(
            SKCanvas canvas,
            SKSize canvasSize,
            SKPoint pointerPosition,
            string[] texts,
            SKFont font,
            ResourceSet? cost = null,
            Dictionary<Resource, SKSvg?>? resourceIcons = null)
        {
            const float textPadding = 8f;
            const float verticalPadding = 6f;
            const float costIconSize = 14f;
            const float costRowHeight = costIconSize + 6f;

            bool hasCost = cost != null && cost.Count > 0;

            // Largeur du tooltip — étendue si la ligne de coût le nécessite
            float tooltipWidth = 200f;
            if (hasCost && resourceIcons != null)
            {
                float cw = 2 * textPadding;
                foreach (var kvp in cost!)
                    cw += costIconSize + 3f + font.MeasureText(kvp.Value.ToString()) + 8f;
                tooltipWidth = Math.Max(tooltipWidth, cw);
            }

            var textLayout = SkiaTextUtils.MeasureWrappedText(texts, tooltipWidth - 2 * textPadding, font);

            float textBlockHeight = textLayout.Size.Height + font.Spacing / 2;
            float tooltipHeight = textBlockHeight + 2 * verticalPadding;
            if (hasCost) tooltipHeight += 4f + costRowHeight + verticalPadding;

            float tooltipX = pointerPosition.X + 15;
            float tooltipY = pointerPosition.Y + 15;

            if (tooltipX + tooltipWidth > canvasSize.Width)
                tooltipX = pointerPosition.X - tooltipWidth - 10;
            if (tooltipY + tooltipHeight > canvasSize.Height)
                tooltipY = pointerPosition.Y - tooltipHeight - 10;

            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, _tooltipBgPaint);
            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, _tooltipBorderPaint);

            SkiaTextUtils.DrawTextLayout(canvas, textLayout, tooltipX + textPadding, tooltipY + verticalPadding + font.Spacing, font, _tooltipTextPaint);

            if (hasCost && resourceIcons != null)
            {
                float separatorY = tooltipY + verticalPadding + textBlockHeight + 2f;
                canvas.DrawLine(tooltipX + 4, separatorY, tooltipX + tooltipWidth - 4, separatorY, _tooltipBorderPaint);

                float rowY = separatorY + 2f;
                float iconX = tooltipX + textPadding;

                foreach (var kvp in cost!)
                {
                    resourceIcons.TryGetValue(kvp.Key, out var svg);
                    var picture = svg?.Picture;
                    if (picture != null)
                    {
                        float scale = costIconSize / 32f;
                        canvas.Save();
                        canvas.Translate(iconX, rowY + (costRowHeight - costIconSize) / 2f);
                        canvas.Scale(scale);
                        canvas.DrawPicture(picture);
                        canvas.Restore();
                    }
                    iconX += costIconSize + 3f;

                    string numText = kvp.Value.ToString();
                    canvas.DrawText(numText, iconX, rowY + (costRowHeight + font.Size) / 2f, font, _tooltipTextPaint);
                    iconX += font.MeasureText(numText) + 8f;
                }
            }
        }
    }
}
