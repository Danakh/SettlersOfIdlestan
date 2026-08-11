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

    [AvaloniaTheory]
    [InlineData('A')]         // texte courant
    [InlineData('e')]
    [InlineData(0x00E9)]      // e accentue : les libelles francais en sont pleins
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
