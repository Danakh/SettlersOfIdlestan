using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Typefaces resolved via SKFontManager.MatchCharacter so that Unicode symbols (⚠ etc.)
/// render correctly on every platform (Segoe UI Symbol on Windows, Apple Symbols on macOS/iOS).
/// </summary>
internal static class SkiaFonts
{
    private static readonly Lazy<SKTypeface> _regular = new(() =>
        SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, null, '⚠')
        ?? SKTypeface.Default);

    private static readonly Lazy<SKTypeface> _bold = new(() =>
        SKFontManager.Default.MatchCharacter(null, SKFontStyle.Bold, null, '⚠')
        ?? SKTypeface.Default);

    public static SKTypeface Regular => _regular.Value;
    public static SKTypeface Bold    => _bold.Value;
}
