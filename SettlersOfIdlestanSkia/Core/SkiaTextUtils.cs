using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core
{
    using SkiaSharp;

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
        /// <summary>
        /// Mesure un texte avec retour à la ligne automatique pour une largeur maximale donnée.
        /// Retourne les lignes découpées et la taille du rectangle englobant.
        /// </summary>
        /// <param name="text">Le texte à mesurer.</param>
        /// <param name="maxWidth">Largeur maximale avant retour à la ligne.</param>
        /// <param name="font">La police à utiliser.</param>
        /// <returns>Un objet WrappedTextLayout contenant les lignes et la taille.</returns>
        public static WrappedTextLayout MeasureWrappedText(
            string text,
            float maxWidth,
            SKFont font)
        {
            var layout = new WrappedTextLayout();

            if (string.IsNullOrEmpty(text))
            {
                layout.Size = new SKSize(0, 0);
                return layout;
            }

            float lineHeight = font.Spacing;
            string[] words = text.Split(' ');
            string line = string.Empty;
            float maxLineWidth = 0;

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                float width = font.MeasureText(testLine);

                if (width > maxWidth && !string.IsNullOrEmpty(line))
                {
                    layout.Lines.Add(line);
                    float lineWidth = font.MeasureText(line);
                    if (lineWidth > maxLineWidth)
                        maxLineWidth = lineWidth;
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

            float height = layout.Lines.Count * lineHeight;
            layout.Size = new SKSize(maxLineWidth, height);

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

        /// <summary>
        /// Dessine un texte avec retour à la ligne automatique pour une largeur maximale donnée.
        /// </summary>
        /// <param name="canvas">Le canvas Skia sur lequel dessiner.</param>
        /// <param name="text">Le texte à afficher.</param>
        /// <param name="x">Position X de départ.</param>
        /// <param name="y">Position Y de la première ligne (baseline).</param>
        /// <param name="maxWidth">Largeur maximale avant retour à la ligne.</param>
        /// <param name="font">La police à utiliser.</param>
        /// <param name="paint">Le pinceau à utiliser.</param>
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
    }
}
