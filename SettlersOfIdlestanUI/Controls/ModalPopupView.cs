using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Modale bloquante : voile plein ecran, boite centree, titre, lignes de texte et boutons.
///
/// Une seule vue sert les quatre modales de GameScreen (partie terminee, sauvegarde corrompue,
/// remise a zero, fin de demo) : elles ont la meme forme, seuls leur contenu et leurs boutons
/// changent. Le renderer reste la machine a etats — ouverture, effet de chaque bouton.
///
/// Le voile est un Border plein occupant toute la fenetre : c'est lui qui rend la modale
/// bloquante, en interceptant tout clic qui n'atteint pas la boite. L'ancien code devait pour
/// cela poser des gardes en tete de chaque gestionnaire d'entree de GameScreen.
/// </summary>
public sealed class ModalPopupView : UserControl
{
    private static readonly SolidColorBrush Veil = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(245, 24, 24, 30));
    private static readonly SolidColorBrush Border_ = new(Colors.Gold);
    private static readonly SolidColorBrush DangerTitle = new(Color.FromRgb(240, 90, 90));
    private static readonly SolidColorBrush HighlightTitle = new(Color.FromRgb(255, 200, 50));
    private static readonly SolidColorBrush BodyText = new(Color.FromRgb(180, 180, 190));
    private static readonly SolidColorBrush CloseButton = new(Color.FromArgb(230, 90, 50, 50));

    private static readonly SolidColorBrush ButtonNeutral = new(Color.FromRgb(55, 55, 65));
    private static readonly SolidColorBrush ButtonPrimary = new(Color.FromRgb(60, 110, 180));
    private static readonly SolidColorBrush ButtonDanger = new(Color.FromRgb(140, 40, 40));
    private static readonly SolidColorBrush ButtonConfirm = new(Color.FromRgb(46, 125, 50));

    public ModalPopupView(ModalPopupViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(ModalPopupViewModel.IsOpen));

        // Le voile s'etire volontairement sur toute la fenetre : c'est ce qui bloque les clics.
        // (Les vues de panneau font l'inverse et doivent s'aligner explicitement.)
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 20,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(ModalPopupViewModel.Title)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(ModalPopupViewModel.Tone))
            {
                Converter = new FuncValueConverter<SkiaLayer.ModalPopupTone, IBrush>(
                    t => t == SkiaLayer.ModalPopupTone.Highlight ? HighlightTitle : DangerTitle),
            },
        };

        var lines = new ItemsControl
        {
            Margin = new Thickness(0, 18, 0, 0),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(ModalPopupViewModel.Lines)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 6 }),
            ItemTemplate = new FuncDataTemplate<string>((_, _) => new TextBlock
            {
                FontSize = 13,
                Foreground = BodyText,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                [!TextBlock.TextProperty] = new Binding(),
            }, supportsRecycling: true),
        };

        var buttons = new ItemsControl
        {
            Margin = new Thickness(0, 24, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(ModalPopupViewModel.Buttons)),
            ItemTemplate = new FuncDataTemplate<ModalPopupButtonViewModel>(
                (_, _) => BuildButton(viewModel), supportsRecycling: true),
        };
        // Deux boutons cote a cote pour un choix binaire, empiles au-dela : trois boutons en
        // ligne ne tiendraient pas dans la largeur de la boite.
        buttons[!ItemsControl.ItemsPanelProperty] = new Binding(nameof(ModalPopupViewModel.ButtonsSideBySide))
        {
            Converter = new FuncValueConverter<bool, ITemplate<Panel?>>(side => new FuncTemplate<Panel?>(
                () => new StackPanel
                {
                    Orientation = side ? Orientation.Horizontal : Orientation.Vertical,
                    Spacing = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                })),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(title);
        stack.Children.Add(lines);
        stack.Children.Add(buttons);

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
            Margin = new Thickness(0, -8, -8, 0),
            Content = new TextBlock
            {
                Text = "X",
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            },
            [!IsVisibleProperty] = new Binding(nameof(ModalPopupViewModel.HasCloseButton)),
        };
        close.Click += (_, _) => viewModel.Close();

        var body = new Panel();
        body.Children.Add(stack);
        body.Children.Add(close);

        var box = new Border
        {
            MinWidth = 460,
            MaxWidth = 560,
            Background = PanelBackground,
            BorderBrush = Border_,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(28, 26),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = body,
        };

        Content = new Border { Background = Veil, Child = box };
    }

    private static Control BuildButton(ModalPopupViewModel owner)
    {
        ModalPopupButtonViewModel? model = null;

        var button = new Button
        {
            MinWidth = 200,
            Height = 42,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 120)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(nameof(ModalPopupButtonViewModel.Label)),
            [!BackgroundProperty] = new Binding(nameof(ModalPopupButtonViewModel.Tone))
            {
                Converter = new FuncValueConverter<SkiaLayer.ModalPopupButtonTone, IBrush>(tone => tone switch
                {
                    SkiaLayer.ModalPopupButtonTone.Primary => ButtonPrimary,
                    SkiaLayer.ModalPopupButtonTone.Danger => ButtonDanger,
                    SkiaLayer.ModalPopupButtonTone.Confirm => ButtonConfirm,
                    _ => ButtonNeutral,
                }),
            },
        };

        // Sans cela, le survol remplace la couleur du bouton par celle du theme : un bouton
        // destructeur perdrait son rouge au moment ou le curseur s'y trouve.
        button.Classes.Add(GameControlStyles.ToneButton);

        button.DataContextChanged += (_, _) => model = button.DataContext as ModalPopupButtonViewModel;
        button.Click += (_, _) => { if (model != null) owner.Invoke(model); };

        return button;
    }
}
