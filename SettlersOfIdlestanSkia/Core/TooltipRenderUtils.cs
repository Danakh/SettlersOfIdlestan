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
            Dictionary<Resource, SKSvg?>? resourceIcons,
            float uiScale)
        {
            float textPadding = TextPadding * uiScale;
            float costIconSize = CostIconSize * uiScale;
            float width = BaseTooltipWidth * uiScale;

            if (cost != null && cost.Count > 0 && resourceIcons != null)
            {
                float cw = 2 * textPadding;
                foreach (var kvp in cost)
                    cw += costIconSize + 3f * uiScale + font.MeasureText(kvp.Value.ToString()) + 8f * uiScale;
                width = Math.Max(width, cw);
            }

            var probe = SkiaTextUtils.MeasureWrappedText(texts, width - 2 * textPadding, font);
            if (probe.Lines.Count >= 3)
            {
                float maxRaw = 0f;
                foreach (string text in texts)
                    foreach (string sentence in text.Split('\n', StringSplitOptions.None))
                        if (!string.IsNullOrEmpty(sentence))
                            maxRaw = Math.Max(maxRaw, font.MeasureText(sentence));

                float ideal = maxRaw + 2 * textPadding;
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
            Dictionary<Resource, SKSvg?>? resourceIcons = null,
            float uiScale = 1f)
        {
            float textPadding = TextPadding * uiScale;
            float verticalPadding = VerticalPadding * uiScale;
            float costIconSize = CostIconSize * uiScale;
            float costRowHeight = CostRowHeight * uiScale;

            bool hasCost = cost != null && cost.Count > 0;

            float tooltipWidth = ComputeTooltipWidth(texts, font, cost, resourceIcons, uiScale);

            var textLayout = SkiaTextUtils.MeasureWrappedText(texts, tooltipWidth - 2 * textPadding, font);

            float textBlockHeight = textLayout.Size.Height + font.Spacing / 2;
            float tooltipHeight = textBlockHeight + 2 * verticalPadding;
            if (hasCost) tooltipHeight += 4f * uiScale + costRowHeight + verticalPadding;

            float tooltipX = pointerPosition.X + 15 * uiScale;
            float tooltipY = pointerPosition.Y + 15 * uiScale;

            if (tooltipX + tooltipWidth > canvasSize.Width)
                tooltipX = pointerPosition.X - tooltipWidth - 10 * uiScale;
            if (tooltipY + tooltipHeight > canvasSize.Height)
                tooltipY = pointerPosition.Y - tooltipHeight - 10 * uiScale;

            // Si le tooltip déborde encore (des deux côtés), on centre
            if (tooltipX < 0 && tooltipX + tooltipWidth > canvasSize.Width)
                tooltipX = (canvasSize.Width - tooltipWidth) / 2f;
            else
                tooltipX = Math.Max(0, tooltipX);

            if (tooltipY < 0 && tooltipY + tooltipHeight > canvasSize.Height)
                tooltipY = (canvasSize.Height - tooltipHeight) / 2f;
            else
                tooltipY = Math.Max(0, tooltipY);

            float cornerRadius = 8 * uiScale;
            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, cornerRadius, cornerRadius, _tooltipBgPaint);
            canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, cornerRadius, cornerRadius, _tooltipBorderPaint);

            SkiaTextUtils.DrawTextLayout(canvas, textLayout, tooltipX + textPadding, tooltipY + verticalPadding + font.Spacing, font, _tooltipTextPaint);

            if (hasCost && resourceIcons != null)
            {
                float separatorY = tooltipY + verticalPadding + textBlockHeight + 2f * uiScale;
                canvas.DrawLine(tooltipX + 4 * uiScale, separatorY, tooltipX + tooltipWidth - 4 * uiScale, separatorY, _tooltipBorderPaint);

                float rowY = separatorY + 2f * uiScale;
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
                    iconX += costIconSize + 3f * uiScale;

                    string numText = kvp.Value.ToString();
                    SkiaTextUtils.DrawText(canvas, numText, iconX, rowY + (costRowHeight + font.Size) / 2f, font, _tooltipTextPaint);
                    iconX += font.MeasureText(numText) + 8f * uiScale;
                }
            }
        }
    }
}
