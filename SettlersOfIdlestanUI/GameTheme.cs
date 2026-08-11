using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace SettlersOfIdlestanUI;

/// <summary>
/// Theme de l'application, partage par les heads Desktop, navigateur et iOS.
///
/// Point important : la variante sombre est imposee. Sans elle, Fluent suit le reglage du
/// systeme hote et tourne le plus souvent en variante claire, alors que toute l'interface du
/// jeu est peinte en sombre. Les couleurs que le theme derive lui-meme — etats indisponibles,
/// cases a cocher, curseurs, barres de defilement — etaient alors calculees pour un fond clair
/// et sortaient en noir translucide sur nos panneaux : lisibles nulle part.
/// </summary>
public static class GameTheme
{
    /// <summary>
    /// A appeler depuis <c>Application.Initialize</c> de chaque head.
    /// </summary>
    public static void Apply(Application app)
    {
        app.RequestedThemeVariant = ThemeVariant.Dark;
        app.Styles.Add(new FluentTheme());

        // Apres le theme : ces styles corrigent ce que Fluent impose aux controles du jeu.
        app.Styles.Add(GameControlStyles.Create());
    }
}
