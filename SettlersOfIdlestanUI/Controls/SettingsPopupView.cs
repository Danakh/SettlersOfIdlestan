using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Popup de reglages en jeu : un chrome bloquant autour du panneau partage avec l'ecran-titre.
/// </summary>
public sealed class SettingsPopupView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush CloseButton = new(Color.FromArgb(230, 90, 50, 50));

    private const double BorderWidth = 2;
    private const double Padding_ = 24;

    /// <summary>
    /// Marge a droite du panneau. Moitie de ce qu'elle valait : le panneau reserve desormais une
    /// bande a son ascenseur, qui separe deja celui-ci du bord, et l'ecart total paraissait
    /// double de celui des autres cotes.
    /// </summary>
    private const double RightPadding = 13;

    /// <summary>Largeur du cadre : le panneau, plus ses marges et sa bordure.</summary>
    public const double BoxWidth =
        SettingsPanelView.TotalWidth + Padding_ + RightPadding + 2 * BorderWidth;

    public SettingsPopupView(SettingsPopupViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(SettingsPopupViewModel.IsOpen));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
            [!TextBlock.TextProperty] = new Binding(nameof(SettingsPopupViewModel.Title)),
        };

        // Pas d'ascenseur ici : le panneau porte le sien, sous une barre d'onglets ancree, et sa
        // hauteur ne depend pas de l'onglet affiche. Le cadre garde donc la meme taille d'un
        // onglet a l'autre, et les boutons d'onglet ne bougent pas.
        var panel = new SettingsPanelView(viewModel.Panel);

        var close = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Background = CloseButton,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Content = new TextBlock
            {
                Text = "X",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                // Sans cela le X se dessine en haut du bouton : Fluent laisse
                // VerticalContentAlignment a Stretch, le TextBlock prend donc toute la
                // hauteur et sa ligne de texte reste calee en haut de la boite.
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        close.Classes.Add(GameControlStyles.ToneButton);
        close.Click += (_, _) => viewModel.Close();

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(title);
        stack.Children.Add(panel);

        var body = new Panel();
        body.Children.Add(stack);
        body.Children.Add(close);

        var box = new Border
        {
            // La largeur du cadre se deduit du panneau et de ses marges : aucun jeu, le panneau
            // remplit exactement la boite de contenu, ce qui aligne le titre sur lui. La hauteur,
            // elle, n'est pas imposee ici : le panneau a la sienne, fixe, donc le cadre garde la
            // meme d'un onglet a l'autre — c'est ce que verifie SettingsPanelSizeTests.
            Width = BoxWidth,
            Background = PanelBackground,
            BorderBrush = Border_,
            BorderThickness = new Thickness(BorderWidth),
            CornerRadius = new CornerRadius(10),
            // Moins de padding a droite qu'ailleurs : c'est de ce cote que le panneau porte la
            // bande de son ascenseur, qui fait deja office de marge.
            Padding = new Thickness(Padding_, Padding_, RightPadding, Padding_),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = body,
        };

        Content = PopupVeil.Create(Veil, box, viewModel.Close);
    }
}
