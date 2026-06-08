using System.Reflection;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Typefaces loaded from embedded Noto fonts so that all Unicode symbols and emoji
/// (⚠ ⚔ 💰 🐉) render correctly on every platform including WebAssembly.
/// On desktop, system fonts are used as primary lookup when embedded resources are absent.
/// </summary>
internal static class SkiaFonts
{
    private static readonly Lazy<SKTypeface> _regular = new(() =>
        LoadEmbedded("NotoSans-Regular.ttf")
        ?? SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, null, 'A')
        ?? SKTypeface.Default);

    private static readonly Lazy<SKTypeface> _bold = new(() =>
        LoadEmbedded("NotoSans-Bold.ttf")
        ?? SKFontManager.Default.MatchCharacter(null, SKFontStyle.Bold, null, 'A')
        ?? SKTypeface.Default);

    // Covers Miscellaneous Symbols block: ⚠ (U+26A0), ⚔ (U+2694), etc.
    private static readonly Lazy<SKTypeface> _symbols = new(() =>
        LoadEmbedded("NotoSansSymbols2-Regular.ttf")
        ?? SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, null, '⚠')
        ?? SKTypeface.Default);

    // Covers emoji: 💰 (U+1F4B0), 🐉 (U+1F409), etc.
    private static readonly Lazy<SKTypeface> _emoji = new(() =>
        LoadEmbedded("NotoEmoji.ttf")
        ?? SKFontManager.Default.MatchCharacter(null, SKFontStyle.Normal, null, 0x1F4B0)
        ?? SKTypeface.Default);

    public static SKTypeface Regular => _regular.Value;
    public static SKTypeface Bold    => _bold.Value;
    public static SKTypeface Symbols => _symbols.Value;
    public static SKTypeface Emoji   => _emoji.Value;

    /// <summary>
    /// Returns the best typeface for the given Unicode codepoint, falling back through
    /// Symbols then Emoji fonts when the primary font lacks the glyph.
    /// </summary>
    internal static SKTypeface FallbackFor(int codepoint, SKTypeface primary)
    {
        if (primary.ContainsGlyph(codepoint)) return primary;
        if (_symbols.Value.ContainsGlyph(codepoint)) return _symbols.Value;
        if (_emoji.Value.ContainsGlyph(codepoint)) return _emoji.Value;
        return primary;
    }

    private static SKTypeface? LoadEmbedded(string fileName)
    {
        var asm = typeof(SkiaFonts).Assembly;
        using var stream = asm.GetManifestResourceStream(
            $"SettlersOfIdlestanSkia.Resources.fonts.{fileName}");
        if (stream is null) return null;
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data);
    }
}
