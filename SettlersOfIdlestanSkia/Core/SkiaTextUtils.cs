using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core
{
    using SkiaSharp;

    public static class SkiaTextUtils
    {
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
            if (string.IsNullOrEmpty(text)) return;

            float lineHeight = font.Spacing;
            string[] words = text.Split(' ');
            string line = string.Empty;

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
                float width = font.MeasureText(testLine);

                if (width > maxWidth && !string.IsNullOrEmpty(line))
                {
                    canvas.DrawText(line, x, y, SKTextAlign.Left, font, paint);
                    y += lineHeight;
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
                canvas.DrawText(line, x, y, SKTextAlign.Left, font, paint);
            }
        }
    }
}
