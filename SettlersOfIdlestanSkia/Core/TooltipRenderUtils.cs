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

        const float TextPadding = 8f;
        const float VerticalPadding = 6f;
        const float CostIconSize = 14f;
        const float CostRowHeight = CostIconSize + 6f;
        const float BaseTooltipWidth = 200f;

        // Calcule la largeur finale du tooltip.
        // Si le texte wrappé produit ≥ 3 lignes, élargit jusqu'à 50 % de plus
        // ou jusqu'à la largeur de la plus longue ligne brute, selon ce qui est le plus petit.
        private static float ComputeTooltipWidth(
            string[] texts,
            SKFont font,
            ResourceSet? cost,
            Dictionary<Resource, SKSvg?>? resourceIcons)
        {
            float width = BaseTooltipWidth;

            if (cost != null && cost.Count > 0 && resourceIcons != null)
            {
                float cw = 2 * TextPadding;
                foreach (var kvp in cost)
                    cw += CostIconSize + 3f + font.MeasureText(kvp.Value.ToString()) + 8f;
                width = Math.Max(width, cw);
            }

            var probe = SkiaTextUtils.MeasureWrappedText(texts, width - 2 * TextPadding, font);
            if (probe.Lines.Count >= 3)
            {
                float maxRaw = 0f;
                foreach (string text in texts)
                    foreach (string sentence in text.Split('\n', StringSplitOptions.None))
                        if (!string.IsNullOrEmpty(sentence))
                            maxRaw = Math.Max(maxRaw, font.MeasureText(sentence));

                float ideal = maxRaw + 2 * TextPadding;
                float maxAllowed = width * 1.5f;
                width = Math.Min(maxAllowed, Math.Max(width, ideal));
            }

            return width;
        }

        public static void DrawTooltip(
            SKCanvas canvas,
            SKSize canvasSize,
            SKPoint pointerPosition,
            string[] texts,
            SKFont font,
            ResourceSet? cost = null,
            Dictionary<Resource, SKSvg?>? resourceIcons = null)
        {
            bool hasCost = cost != null && cost.Count > 0;

            float tooltipWidth = ComputeTooltipWidth(texts, font, cost, resourceIcons);

            var textLayout = SkiaTextUtils.MeasureWrappedText(texts, tooltipWidth - 2 * TextPadding, font);

            float textBlockHeight = textLayout.Size.Height + font.Spacing / 2;
            float tooltipHeight = textBlockHeight + 2 * VerticalPadding;
            if (hasCost) tooltipHeight += 4f + CostRowHeight + VerticalPadding;

            float tooltipX = pointerPosition.X + 15;
            float tooltipY = pointerPosition.Y + 15;

            if (tooltipX + tooltipWidth > canvasSize.Width)
                tooltipX = pointerPosition.X - tooltipWidth - 10;
            if (tooltipY + tooltipHeight > canvasSize.Height)
                tooltipY = pointerPosition.Y - tooltipHeight - 10;

            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, _tooltipBgPaint);
            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, _tooltipBorderPaint);

            SkiaTextUtils.DrawTextLayout(canvas, textLayout, tooltipX + TextPadding, tooltipY + VerticalPadding + font.Spacing, font, _tooltipTextPaint);

            if (hasCost && resourceIcons != null)
            {
                float separatorY = tooltipY + VerticalPadding + textBlockHeight + 2f;
                canvas.DrawLine(tooltipX + 4, separatorY, tooltipX + tooltipWidth - 4, separatorY, _tooltipBorderPaint);

                float rowY = separatorY + 2f;
                float iconX = tooltipX + TextPadding;

                foreach (var kvp in cost!)
                {
                    resourceIcons.TryGetValue(kvp.Key, out var svg);
                    var picture = svg?.Picture;
                    if (picture != null)
                    {
                        float scale = CostIconSize / 32f;
                        canvas.Save();
                        canvas.Translate(iconX, rowY + (CostRowHeight - CostIconSize) / 2f);
                        canvas.Scale(scale);
                        canvas.DrawPicture(picture);
                        canvas.Restore();
                    }
                    iconX += CostIconSize + 3f;

                    string numText = kvp.Value.ToString();
                    canvas.DrawText(numText, iconX, rowY + (CostRowHeight + font.Size) / 2f, font, _tooltipTextPaint);
                    iconX += font.MeasureText(numText) + 8f;
                }
            }
        }
    }
}
