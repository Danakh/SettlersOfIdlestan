using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using SettlersOfIdlestanUI;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Verrouille les polices de l'overlay. Le head navigateur n'a aucune police systeme a offrir
/// au gestionnaire de polices : tant que le jeu ne fournissait pas les siennes, la version web
/// s'affichait dans une police generique et rendait les symboles et emoji des libelles en tofu.
///
/// Ces tests passent par les memes appels que le moteur de texte, et exigent que la police
/// retenue soit celle embarquee — un test qui se contenterait de trouver un glyphe passerait
/// sous Windows grace a Segoe UI Emoji, sans rien prouver pour le navigateur.
///
/// D'ou la separation en deux cas : le moteur n'interroge TryMatchCharacter que pour les
/// glyphes absents de la famille demandee. Le latin, que Noto Sans porte, ne passe jamais par
/// la — et ne doit pas y etre teste : Avalonia ignore une entree de repli identique a la
/// famille demandee, si bien que la question "qui sait dessiner 'A', Noto Sans etant demandee"
/// se resout par la police systeme (Segoe UI sous Windows, rien sous WebAssembly). Ce test
/// mesurait donc la machine hote plutot que le jeu.
/// </summary>
public class GameFontsTests
{
    [AvaloniaFact]
    public void La_police_par_defaut_est_la_police_embarquee()
    {
        Assert.True(
            FontManager.Current.TryGetGlyphTypeface(
                new Typeface(GameFonts.DefaultFamilyName), out var glyphTypeface),
            "La famille par defaut ne se resout pas : verifier que les .ttf sont bien inclus en "
            + "AvaloniaResource sous Assets/Fonts dans SettlersOfIdlestanUI.csproj.");

        Assert.Equal("Noto Sans", glyphTypeface.FamilyName);
    }

    /// <summary>
    /// Texte courant : la police par defaut doit le porter elle-meme, sans repli. C'est la seule
    /// garantie qui vaille pour le navigateur, ou aucune police systeme ne viendrait au secours.
    /// </summary>
    [AvaloniaTheory]
    [InlineData('A')]
    [InlineData('e')]
    [InlineData(0x00E9)]      // e accentue : les libelles francais en sont pleins
    public void Le_texte_courant_est_porte_par_la_police_par_defaut(int codepoint)
    {
        Assert.True(
            FontManager.Current.TryGetGlyphTypeface(
                new Typeface(GameFonts.DefaultFamilyName), out var glyphTypeface));

        Assert.True(glyphTypeface.CharacterToGlyphMap.ContainsGlyph(codepoint),
            $"Noto Sans n'a pas le glyphe U+{codepoint:X4} : il partirait chercher une police "
            + "systeme, absente du head navigateur.");
    }

    /// <summary>
    /// Symboles et emoji : absents de Noto Sans, ils passent par le repli. C'est la que se joue
    /// le tofu du navigateur, et la police retenue doit venir des ressources du jeu.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(0x26A0)]      // avertissement
    [InlineData(0x2694)]      // epees croisees
    [InlineData(0x1F4B0)]     // sac d'or
    [InlineData(0x1F409)]     // dragon
    public void Les_caracteres_speciaux_trouvent_un_glyphe_dans_les_polices_embarquees(int codepoint)
    {
        Assert.True(
            FontManager.Current.TryMatchCharacter(
                codepoint, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
                new FontFamily(GameFonts.DefaultFamilyName), CultureInfo.InvariantCulture,
                out var typeface),
            $"Aucune police pour U+{codepoint:X4} : il s'afficherait en carre vide.");

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface));
        Assert.True(glyphTypeface.CharacterToGlyphMap.ContainsGlyph(codepoint),
            $"La police retenue pour U+{codepoint:X4} n'a pas le glyphe.");

        // La police retenue doit venir des ressources du jeu, pas du systeme hote.
        Assert.Contains(glyphTypeface.FamilyName, new[] { "Noto Sans", "Noto Sans Symbols2", "Noto Emoji" });
    }
}
