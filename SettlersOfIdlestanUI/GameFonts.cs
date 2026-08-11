using Avalonia;
using Avalonia.Media;

namespace SettlersOfIdlestanUI;

/// <summary>
/// Polices de l'overlay Avalonia. Pendant de <see cref="SettlersOfIdlestanSkia.Core.SkiaFonts"/>
/// pour le rendu de la carte : les memes fichiers Noto, embarques cette fois en ressources
/// Avalonia, servent aux controles.
///
/// Sans cela chaque head prend la police du systeme : Segoe UI sous Windows, rien du tout sous
/// WebAssembly ou le navigateur n'expose aucune police au gestionnaire Skia. Le head navigateur
/// retombe alors sur une police generique qui n'a ni les memes metriques ni les glyphes des
/// symboles et emoji (⚠ ⚔ 💰 🐉) utilises par les libelles du jeu — ils s'affichent en tofu.
/// </summary>
public static class GameFonts
{
    private const string FontsUri = "avares://SettlersOfIdlestanUI/Assets/Fonts";

    /// <summary>Famille de texte principale, identique a celle de la carte.</summary>
    public const string DefaultFamilyName = $"{FontsUri}#Noto Sans";

    /// <summary>
    /// Cable les polices embarquees sur le gestionnaire de polices d'Avalonia. A appeler sur
    /// l'<see cref="AppBuilder"/> de chaque head, avant le demarrage.
    /// </summary>
    public static AppBuilder WithGameFonts(this AppBuilder builder) => builder
        .With(new FontManagerOptions
        {
            DefaultFamilyName = DefaultFamilyName,

            // Noto Sans ne couvre ni les symboles divers ni les emoji : sans ces reprises,
            // le glyphe manquant est rendu en carre vide au lieu d'aller chercher la police
            // qui le possede. Meme chaine de fallback que SkiaFonts.FallbackFor.
            FontFallbacks =
            [
                new FontFallback { FontFamily = new FontFamily($"{FontsUri}#Noto Sans Symbols2") },
                new FontFallback { FontFamily = new FontFamily($"{FontsUri}#Noto Emoji") },
            ],
        });
}
