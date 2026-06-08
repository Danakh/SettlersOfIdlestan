using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace SettlersOfIdlestanSkia.Core
{
    using SettlersOfIdlestan.Model.IslandMap;
    using SettlersOfIdlestanSkia.Services.Localization;
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
        /// Dessine les lignes de texte à partir d'un layout, avec fallback automatique
        /// vers les polices Symbols/Emoji pour les caractères hors NotoSans.
        /// </summary>
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
                DrawText(canvas, line, x, currentY, SKTextAlign.Left, font, paint);
                currentY += lineHeight;
            }
        }

        /// <summary>
        /// Remplacement de canvas.DrawText gérant le fallback multi-police pour les symboles
        /// Unicode (⚠ ⚔ ✓ ☐ ► ◄ …) et les emoji (💰 🐉 …) absents de NotoSans.
        /// Utilise la même signature que SKCanvas.DrawText pour faciliter la substitution.
        /// </summary>
        public static void DrawText(
            SKCanvas canvas, string text, float x, float y, SKTextAlign align, SKFont? font, SKPaint? paint)
        {
            if (string.IsNullOrEmpty(text) || font is null || paint is null) return;

            var primary = font.Typeface ?? SKTypeface.Default;

            // Fast path : aucun fallback nécessaire (cas courant : texte latin pur)
            bool needsFallback = false;
            for (int k = 0; k < text.Length; k++)
            {
                int cp = char.IsHighSurrogate(text[k]) && k + 1 < text.Length
                    ? char.ConvertToUtf32(text[k], text[k + 1]) : text[k];
                if (SkiaFonts.FallbackFor(cp, primary) != primary) { needsFallback = true; break; }
            }

            if (!needsFallback)
            {
                canvas.DrawText(text, x, y, align, font, paint);
                return;
            }

            // Découpe le texte en runs consécutifs partageant la même typeface
            var runs = BuildRuns(text, primary, font.Size);

            float totalWidth = 0f;
            foreach (var (_, f, w) in runs) totalWidth += w;

            float startX = align == SKTextAlign.Center ? x - totalWidth / 2f
                         : align == SKTextAlign.Right   ? x - totalWidth
                         : x;

            float cx = startX;
            foreach (var (seg, f, w) in runs)
            {
                canvas.DrawText(seg, cx, y, SKTextAlign.Left, f, paint);
                cx += w;
                f.Dispose();
            }
        }

        /// <summary>
        /// Surcharge sans alignement (équivalent SKTextAlign.Left).
        /// </summary>
        public static void DrawText(
            SKCanvas canvas, string text, float x, float y, SKFont? font, SKPaint? paint)
            => DrawText(canvas, text, x, y, SKTextAlign.Left, font, paint);

        // Construit la liste de runs (segment, SKFont créé, largeur mesurée).
        // L'appelant est responsable de disposer les SKFont retournés.
        private static List<(string text, SKFont font, float width)> BuildRuns(
            string text, SKTypeface primary, float size)
        {
            var runs = new List<(string, SKFont, float)>();
            var sb = new StringBuilder();
            var currentTf = primary;

            void FlushRun()
            {
                if (sb.Length == 0) return;
                var seg = sb.ToString();
                var f = new SKFont(currentTf, size);
                runs.Add((seg, f, f.MeasureText(seg)));
                sb.Clear();
            }

            for (int i = 0; i < text.Length;)
            {
                bool isSurrogate = char.IsHighSurrogate(text[i]) && i + 1 < text.Length;
                int charLen = isSurrogate ? 2 : 1;
                int cp = isSurrogate ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
                var tf = SkiaFonts.FallbackFor(cp, primary);
                if (tf != currentTf) { FlushRun(); currentTf = tf; }
                sb.Append(text, i, charLen);
                i += charLen;
            }
            FlushRun();

            return runs;
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

        public static string computeCostString(LocalizationService localizationService, ResourceSet cost)
        {
            return string.Join(" | ", cost.Select(kvp => $"{localizationService.Get($"resource_{kvp.Key.ToString().ToLower()}_short")}: {kvp.Value}"));
        }
    }
}
