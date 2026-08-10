using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Ecran-titre : titre, trois onglets (changelog, credits, reglages) et les boutons de depart.
///
/// Son fond est opaque et couvre toute la fenetre : c'est lui qui masque le canevas Skia, encore
/// present dessous, et qui intercepte les clics.
///
/// L'onglet des reglages reutilise le meme panneau que le popup en jeu — c'est ce qui permet a
/// la version Skia partagee de disparaitre.
/// </summary>
public sealed class TitleScreenView : UserControl
{
    private static readonly SolidColorBrush Background = new(Color.FromRgb(15, 15, 22));
    private static readonly SolidColorBrush TitleBrush = new(Color.FromRgb(230, 190, 90));
    private static readonly SolidColorBrush Divider = new(Color.FromRgb(100, 85, 45));
    private static readonly SolidColorBrush SectionBg = new(Color.FromRgb(22, 22, 32));
    private static readonly SolidColorBrush SectionBorder = new(Color.FromRgb(55, 55, 75));
    private static readonly SolidColorBrush TabActive = new(Color.FromRgb(45, 90, 145));
    private static readonly SolidColorBrush Subtle = new(Color.FromRgb(155, 155, 170));
    private static readonly SolidColorBrush Primary = new(Color.FromRgb(35, 80, 130));
    private static readonly SolidColorBrush Cloud = new(Color.FromRgb(30, 90, 75));
    private static readonly SolidColorBrush Danger = new(Color.FromRgb(80, 30, 30));

    public TitleScreenView(TitleScreenViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(TitleScreenViewModel.IsVisible));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 34,
            FontWeight = FontWeight.Bold,
            Foreground = TitleBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(TitleScreenViewModel.Title)),
        };

        var divider = new Border
        {
            Height = 2,
            Width = 440,
            Background = Divider,
            Margin = new Thickness(0, 10, 0, 22),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var tabs = new ItemsControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(TitleScreenViewModel.Tabs)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal }),
            ItemTemplate = new FuncDataTemplate<TitleTabViewModel>((_, _) => BuildTab(viewModel), true),
        };

        var content = new Panel { Margin = new Thickness(0, 8, 0, 0) };
        content.Children.Add(BuildChangelog());
        content.Children.Add(BuildCredits());
        content.Children.Add(BuildSettings(viewModel));

        var actions = new ItemsControl
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 16, 0, 0),
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(TitleScreenViewModel.Actions)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
            }),
            ItemTemplate = new FuncDataTemplate<TitleActionViewModel>((_, _) => BuildAction(viewModel), true),
        };

        var discord = new Button
        {
            Content = "Discord",
            FontSize = 12,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(88, 101, 242)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(14, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 16, 16),
        };
        discord.Classes.Add(GameControlStyles.ToneButton);
        discord.Click += (_, _) => viewModel.OpenDiscord();

        // Le contenu occupe la place restante entre les onglets et les boutons : c'est lui qui
        // defile, pas la page entiere, pour que les boutons restent toujours atteignables.
        var layout = new DockPanel { LastChildFill = true, Margin = new Thickness(24, 32, 24, 24) };
        var header = new StackPanel { Orientation = Orientation.Vertical };
        header.Children.Add(title);
        header.Children.Add(divider);
        header.Children.Add(tabs);
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(header);
        layout.Children.Add(actions);
        layout.Children.Add(content);

        var root = new Panel();
        root.Children.Add(layout);
        root.Children.Add(discord);

        Content = new Border { Background = Background, Child = root };
    }

    private static Control BuildTab(TitleScreenViewModel owner)
    {
        TitleTabViewModel? model = null;

        var button = new Button
        {
            Width = 213,
            Height = 30,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            BorderBrush = SectionBorder,
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(nameof(TitleTabViewModel.Label)),
            [!BackgroundProperty] = new Binding(nameof(TitleTabViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? TabActive : SectionBg),
            },
            [!ForegroundProperty] = new Binding(nameof(TitleTabViewModel.IsActive))
            {
                Converter = new FuncValueConverter<bool, IBrush>(a => a ? Brushes.White : Subtle),
            },
        };
        button.Classes.Add(GameControlStyles.ToneButton);
        button.DataContextChanged += (_, _) => model = button.DataContext as TitleTabViewModel;
        button.Click += (_, _) => { if (model != null) owner.SelectTab(model); };
        return button;
    }

    private static Control BuildChangelog() => new Border
    {
        MaxWidth = 640,
        Background = SectionBg,
        BorderBrush = SectionBorder,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Padding = new Thickness(14, 10),
        HorizontalAlignment = HorizontalAlignment.Center,
        [!IsVisibleProperty] = new Binding(nameof(TitleScreenViewModel.ShowingChangelog)),
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new TextBlock
            {
                FontSize = 13,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                [!TextBlock.TextProperty] = new Binding(nameof(TitleScreenViewModel.ChangelogText)),
            },
        },
    };

    private static Control BuildCredits()
    {
        var studio = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = TitleBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(TitleScreenViewModel.CreditsStudio)),
        };
        var dev = new TextBlock
        {
            FontSize = 13,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(TitleScreenViewModel.CreditsDev)),
        };

        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 30, 0, 0),
            [!IsVisibleProperty] = new Binding(nameof(TitleScreenViewModel.ShowingCredits)),
        };
        stack.Children.Add(studio);
        stack.Children.Add(dev);
        return stack;
    }

    // Pas de largeur ici : c'est le panneau qui porte la sienne, identique au popup en jeu et
    // stable d'une langue a l'autre.
    private static Control BuildSettings(TitleScreenViewModel owner) => new ScrollViewer
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Margin = new Thickness(0, 16, 0, 0),
        [!IsVisibleProperty] = new Binding(nameof(TitleScreenViewModel.ShowingSettings)),
        Content = new SettingsPanelView(owner.Settings),
    };

    private static Control BuildAction(TitleScreenViewModel owner)
    {
        TitleActionViewModel? model = null;

        var button = new Button
        {
            MinWidth = 190,
            Height = 44,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(6),
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 125)),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            [!ContentProperty] = new Binding(nameof(TitleActionViewModel.Label)),
        };

        button.Classes.Add(GameControlStyles.ToneButton);
        button.DataContextChanged += (_, _) =>
        {
            model = button.DataContext as TitleActionViewModel;
            button.Background = model?.Tone switch
            {
                SkiaLayer.TitleActionTone.Cloud => Cloud,
                SkiaLayer.TitleActionTone.Danger => Danger,
                _ => Primary,
            };
        };
        button.Click += (_, _) => { if (model != null) owner.Invoke(model); };
        return button;
    }
}
