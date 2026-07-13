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
        /// <summary>
        /// Mode d'affichage des grands nombres, câblé depuis GameSettings.NumberFormat
        /// (au chargement d'une partie et à chaque changement dans le panneau de paramètres).
        /// </summary>
        public static SettlersOfIdlestan.Model.Game.NumberFormatMode NumberFormat { get; set; }
            = SettlersOfIdlestan.Model.Game.NumberFormatMode.Classic;

        private static readonly string[] ClassicSuffixes = { "k", "M", "B", "T", "Qa", "Qi", "Sx", "Sp" };

        /// <summary>
        /// Formate un nombre selon le réglage d'affichage des grands nombres :
        /// classique (1.5k, 12M…), scientifique (1.23e4…) ou ingénieur (12.3e3…).
        /// Les valeurs inférieures à 1000 sont affichées telles quelles.
        /// </summary>
        public static string FormatNumber(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return value.ToString();

            bool negative = value < 0;
            double abs = Math.Abs(value);

            string result;
            if (abs < 1000)
            {
                result = abs.ToString("0.##");
            }
            else
            {
                result = NumberFormat switch
                {
                    SettlersOfIdlestan.Model.Game.NumberFormatMode.Scientific => FormatScientific(abs),
                    SettlersOfIdlestan.Model.Game.NumberFormatMode.Engineering => FormatEngineering(abs),
                    _ => FormatClassic(abs),
                };
            }

            return negative ? "-" + result : result;
        }

        // Mantisse à 3 chiffres significatifs maximum : 1.23, 12.3, 123.
        private static string FormatMantissa(double m)
            => m < 10 ? m.ToString("0.##") : m < 100 ? m.ToString("0.#") : m.ToString("0");

        private static string FormatClassic(double abs)
        {
            int tier = 0;
            while (abs >= 1000 && tier < ClassicSuffixes.Length)
            {
                abs /= 1000.0;
                tier++;
            }
            // L'arrondi de la mantisse peut repasser la barre des 1000 (ex. 999 999 → "1000k") :
            // on monte alors d'un palier.
            if (Math.Round(abs, 2) >= 1000 && tier < ClassicSuffixes.Length)
            {
                abs /= 1000.0;
                tier++;
            }
            return FormatMantissa(abs) + ClassicSuffixes[tier - 1];
        }

        private static string FormatScientific(double abs)
        {
            int exp = (int)Math.Floor(Math.Log10(abs));
            double mantissa = abs / Math.Pow(10, exp);
            if (Math.Round(mantissa, 2) >= 10)
            {
                mantissa /= 10.0;
                exp++;
            }
            return mantissa.ToString("0.##") + "e" + exp;
        }

        private static string FormatEngineering(double abs)
        {
            int exp = (int)Math.Floor(Math.Log10(abs));
            int exp3 = exp - exp % 3;
            double mantissa = abs / Math.Pow(10, exp3);
            if (Math.Round(mantissa, 2) >= 1000)
            {
                mantissa /= 1000.0;
                exp3 += 3;
            }
            return FormatMantissa(mantissa) + "e" + exp3;
        }

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
            return string.Join(" | ", cost.Select(kvp => $"{localizationService.Get($"resource_{kvp.Key.ToString().ToLower()}_short")}: {FormatNumber(kvp.Value)}"));
        }
    }
}
