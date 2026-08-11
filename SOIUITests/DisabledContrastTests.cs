using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SettlersOfIdlestanUI;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Verrouille la lisibilite des controles indisponibles.
///
/// Le theme Fluent peint ces etats en superposant du noir ou du blanc translucide, la couleur
/// etant choisie selon la variante du theme. Personne n'ayant fixe cette variante, elle suivait
/// le systeme hote et tombait le plus souvent sur « clair » alors que toute l'interface du jeu
/// est sombre : le bouton « Construire » d'un batiment trop cher sortait en noir a 40 % sur gris
/// fonce, illisible, et les cases a cocher verrouillees avec lui.
/// </summary>
public class DisabledContrastTests
{
    /// Fond des panneaux du jeu, sur lequel les couleurs translucides se composent.
    private static readonly Color PanelBackground = Color.FromRgb(30, 30, 40);

    /// Seuil AA du WCAG pour du texte de taille normale.
    private const double MinimumContrast = 4.5;

    [AvaloniaFact]
    public void Le_theme_est_en_variante_sombre()
    {
        // Sans cela, toutes les couleurs derivees par Fluent sont calculees pour un fond clair.
        Assert.Equal(ThemeVariant.Dark, Application.Current!.ActualThemeVariant);
    }

    [AvaloniaFact]
    public void Le_libelle_d_un_bouton_indisponible_reste_lisible()
    {
        var button = new Button
        {
            Content = "Construire",
            // Teinte d'un bouton Construire du panneau de ville.
            Background = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
            Foreground = Brushes.White,
            IsEnabled = false,
        };
        button.Classes.Add(GameControlStyles.ToneButton);

        AssertLegible(Show(button), "Le libelle d'un bouton indisponible");
    }

    [AvaloniaFact]
    public void Le_libelle_d_une_case_a_cocher_indisponible_reste_lisible()
    {
        var checkBox = new CheckBox { Content = "Investissement complete", IsEnabled = false };

        AssertLegible(Show(checkBox), "Le libelle d'une case a cocher indisponible");
    }

    /// Affiche le controle et rend le ContentPresenter de son template, ou vivent le fond et la
    /// couleur de texte que le theme applique aux etats.
    private static ContentPresenter Show(Control control)
    {
        var window = new Window
        {
            Width = 300,
            Height = 200,
            Background = new SolidColorBrush(PanelBackground),
            Content = control,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var presenter = control.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
        Assert.NotNull(presenter);
        return presenter;
    }

    private static void AssertLegible(ContentPresenter presenter, string what)
    {
        // Le TextBlock du contenu herite du ContentPresenter : c'est bien la couleur affichee.
        var text = presenter.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        Assert.NotNull(text);

        var background = Composite(Solid(presenter.Background), PanelBackground);
        var foreground = Composite(Solid(text.Foreground), background);
        var contrast = Contrast(foreground, background);

        Assert.True(contrast >= MinimumContrast,
            $"{what} ressort a {contrast:F1}:1 ({foreground} sur {background}), sous le seuil "
            + $"de {MinimumContrast}:1. Verifier les etats :disabled de GameControlStyles.");
    }

    private static Color Solid(IBrush? brush) =>
        brush is ISolidColorBrush s ? s.Color : Colors.Transparent;

    /// Composition alpha du premier plan sur le fond, canal par canal.
    private static Color Composite(Color foreground, Color background)
    {
        double a = foreground.A / 255d;
        return Color.FromRgb(
            (byte)Math.Round(foreground.R * a + background.R * (1 - a)),
            (byte)Math.Round(foreground.G * a + background.G * (1 - a)),
            (byte)Math.Round(foreground.B * a + background.B * (1 - a)));
    }

    /// Rapport de contraste WCAG entre deux couleurs opaques.
    private static double Contrast(Color a, Color b)
    {
        double la = RelativeLuminance(a), lb = RelativeLuminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double RelativeLuminance(Color c) =>
        0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);

    private static double Linear(byte channel)
    {
        double v = channel / 255d;
        return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }
}
