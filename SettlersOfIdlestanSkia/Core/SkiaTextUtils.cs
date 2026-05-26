using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace SettlersOfIdlestanSkia.Core
{
    using SettlersOfIdlestan.Model.IslandMap;
    using SettlersOfIdlestan.Services.Localization;
    using SkiaSharp;
    using static System.Net.Mime.MediaTypeNames;

    /// <summary>
    /// Représente le résultat de la mesure d'un texte wrappé.
    /// </summary>
    public class WrappedTextLayout
    {
        /// <summary>
        /// Les lignes de texte découpées.
        /// </summary>
        public List<string> Lines { get; set; } = new List<string>();

        /// <summary>
        /// La taille du rectangle englobant.
        /// </summary>
        public SKSize Size { get; set; }
    }

    public static class SkiaTextUtils
    {
        public static WrappedTextLayout MeasureWrappedText(
            string text,
            float maxWidth,
            SKFont font)
        {
            return MeasureWrappedText(new string[] { text }, maxWidth, font);
        }

        public static WrappedTextLayout MeasureWrappedText(
            string[] texts,
            float maxWidth,
            SKFont font)
        {
            var layout = new WrappedTextLayout();

            float maxLineWidth = 0;

            foreach (string paragraph in texts)
            {
                string[] sentences = paragraph.Split("\n", StringSplitOptions.None);

                foreach (string sentence in sentences)
                {
                    if (string.IsNullOrEmpty(sentence))
                    {
                        layout.Lines.Add("");
                        continue;
                    }

                    string[] words = sentence.Split(' ');
                    string line = string.Empty;

                    foreach (string word in words)
                    {
                        string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                        float width = font.MeasureText(testLine);

                        if (width > maxWidth && !string.IsNullOrEmpty(line))
                        {
                            layout.Lines.Add(line);
                            float lineWidth = font.MeasureText(line);
                            maxLineWidth = Math.Max(maxLineWidth, lineWidth);
                            line = word;
                        }
                        else
                        {
                            line = testLine;
                        }
                    }

                    // Dernière ligne
                    if (!string.IsNullOrEmpty(line))
                    {
                        layout.Lines.Add(line);
                        float lineWidth = font.MeasureText(line);
                        if (lineWidth > maxLineWidth)
                            maxLineWidth = lineWidth;
                    }
                }
            }

            if (layout.Lines.Count == 0)
            {
                layout.Size = new SKSize(0, 0);
            }
            else
            {
                float height = layout.Lines.Count * font.Spacing;
                layout.Size = new SKSize(maxLineWidth, height);
            }
            
            return layout;
        }

        /// <summary>
        /// Dessine les lignes de texte à partir d'un layout.
        /// </summary>
        /// <param name="canvas">Le canvas Skia sur lequel dessiner.</param>
        /// <param name="layout">Le layout contenant les lignes à dessiner.</param>
        /// <param name="x">Position X de départ.</param>
        /// <param name="y">Position Y de la première ligne (baseline).</param>
        /// <param name="font">La police à utiliser.</param>
        /// <param name="paint">Le pinceau à utiliser.</param>
        public static void DrawTextLayout(
            SKCanvas canvas,
            WrappedTextLayout layout,
            float x,
            float y,
            SKFont font,
            SKPaint paint)
        {
            if (layout.Lines.Count == 0) return;

            float lineHeight = font.Spacing;
            float currentY = y;

            foreach (string line in layout.Lines)
            {
                canvas.DrawText(line, x, currentY, SKTextAlign.Left, font, paint);
                currentY += lineHeight;
            }
        }

        public static void DrawWrappedText(
            SKCanvas canvas,
            string text,
            float x,
            float y,
            float maxWidth,
            SKFont font,
            SKPaint paint)
        {
            var layout = MeasureWrappedText(text, maxWidth, font);
            DrawTextLayout(canvas, layout, x, y, font, paint);
        }

        public static void DrawWrappedText(
            SKCanvas canvas,
            string[] texts,
            float x,
            float y,
            float maxWidth,
            SKFont font,
            SKPaint paint)
        {
            var layout = MeasureWrappedText(texts, maxWidth, font);
            DrawTextLayout(canvas, layout, x, y, font, paint);
        }

        public static string computeCostString(ILocalizationService localizationService, ResourceSet cost)
        {
            return string.Join(" | ", cost.Select(kvp => $"{localizationService.Get($"resource_{kvp.Key.ToString().ToLower()}_short")}: {kvp.Value}"));
        }
    }
}
