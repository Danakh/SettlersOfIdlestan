using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;
using SkiaLayer = SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Onglet plein ecran du journal des evenements : une carte par entree, coloree selon son ton.
///
/// Le fond opaque couvre toute la surface sous la barre du haut. C'est lui qui masque la carte
/// et intercepte les clics — l'ancien rendu Skia peignait le meme aplat, mais devait en plus
/// faire declarer l'onglet actif par IsPointBlockedByUI pour que les clics n'atteignent pas la
/// carte derriere.
///
/// La liste defile, la ou le rendu Skia s'arretait au bas de l'ecran : les 50 entrees que le
/// modele conserve sont donc toutes accessibles.
/// </summary>
public sealed class EventLogView : UserControl
{
    private static readonly SolidColorBrush PanelBackground = new(Color.FromArgb(240, 18, 18, 24));
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(120, 120, 130));
    private static readonly SolidColorBrush BodyText = new(Color.FromRgb(200, 200, 210));
    private static readonly SolidColorBrush TabIdle = new(Color.FromArgb(220, 30, 30, 40));
    private static readonly SolidColorBrush TabActive = new(Color.FromArgb(220, 60, 55, 20));
    private static readonly SolidColorBrush TabBorder = new(Color.FromRgb(60, 60, 80));

    public EventLogView(EventLogViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(EventLogViewModel.IsVisible));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var title = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            VerticalAlignment = VerticalAlignment.Center,
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogViewModel.Title)),
        };

        // Onglet Reglages, en haut a droite : bascule la page entre la liste et les cases a
        // cocher. Un Border cliquable plutot qu'un Button — le template Button du theme repeint
        // le fond au survol et masquerait l'etat actif.
        var settingsTab = BuildSettingsTab(viewModel);

        var header = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 14) };
        DockPanel.SetDock(title, Dock.Left);
        DockPanel.SetDock(settingsTab, Dock.Right);
        header.Children.Add(title);
        header.Children.Add(settingsTab);

        var empty = new TextBlock
        {
            FontSize = 12,
            Foreground = Muted,
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogViewModel.EmptyMessage)),
            [!IsVisibleProperty] = new Binding(nameof(EventLogViewModel.IsEmpty)),
        };

        var entries = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(EventLogViewModel.Entries)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 6 }),
            ItemTemplate = new FuncDataTemplate<EventLogEntryViewModel>(
                (_, _) => BuildCard(), supportsRecycling: true),
        };

        var entriesPage = new StackPanel
        {
            Orientation = Orientation.Vertical,
            [!IsVisibleProperty] = new Binding(nameof(EventLogViewModel.ShowEntries)),
        };
        entriesPage.Children.Add(empty);
        entriesPage.Children.Add(entries);

        var settingsPage = BuildSettingsPage(viewModel);

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(header);
        stack.Children.Add(entriesPage);
        stack.Children.Add(settingsPage);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            // Colonne centree de largeur fixe : Stretch + MaxWidth, et non Center, pour que le
            // cadre ne se retrecisse pas sur son contenu. Avec Center, la page Reglages (des cases
            // a cocher courtes) mesurait bien moins large que la liste du journal, et le cadre
            // changeait de taille d'un onglet a l'autre. Stretch prend toute la largeur offerte,
            // MaxWidth la borne a 720, et Avalonia recentre l'element ainsi bride — donc largeur
            // constante partout, sauf sur une fenetre plus etroite que 720 ou la colonne suit.
            Content = new Border { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch, Child = stack },
            Padding = new Thickness(20),
        };

        Content = new Border { Background = PanelBackground, Child = scroll };
    }

    /// <summary>
    /// Bouton engrenage de l'onglet Reglages. Sa surbrillance suit ShowSettings : sans elle, rien
    /// ne dirait, une fois la page basculee, d'ou vient ce qu'on regarde ni comment en sortir.
    /// </summary>
    private static Control BuildSettingsTab(EventLogViewModel viewModel)
    {
        var gear = GearIcon.Create(16, Accent);
        gear.HorizontalAlignment = HorizontalAlignment.Center;
        gear.VerticalAlignment = VerticalAlignment.Center;

        var tab = new Border
        {
            Padding = new Thickness(8, 5),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = gear,
            [!BackgroundProperty] = new Binding(nameof(EventLogViewModel.ShowSettings))
            {
                Converter = new FuncValueConverter<bool, IBrush>(on => on ? TabActive : TabIdle),
            },
            [!BorderBrushProperty] = new Binding(nameof(EventLogViewModel.ShowSettings))
            {
                Converter = new FuncValueConverter<bool, IBrush>(on => on ? Accent : TabBorder),
            },
        };

        tab.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            viewModel.ToggleSettings();
        };

        return tab;
    }

    /// <summary>
    /// Page Reglages : une case a cocher par famille d'evenements. Decochee, la famille disparait
    /// du journal, n'allume plus l'onglet et ne produit plus de toast — le filtre est applique a
    /// la source, dans GameEventLog.Add.
    /// </summary>
    private static Control BuildSettingsPage(EventLogViewModel viewModel)
    {
        var header = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogViewModel.SettingsTitle)),
        };

        var hint = new TextBlock
        {
            FontSize = 12,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 12),
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogViewModel.SettingsHint)),
        };

        var rows = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(EventLogViewModel.Filters)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Spacing = 4 }),
            ItemTemplate = new FuncDataTemplate<EventLogFilterViewModel>(
                (_, _) => BuildFilterRow(viewModel), supportsRecycling: true),
        };

        // Une famille n'apparait qu'une fois croisee dans la partie : la liste peut donc etre vide.
        var noFilter = new TextBlock
        {
            FontSize = 12,
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogViewModel.SettingsEmptyMessage)),
            [!IsVisibleProperty] = new Binding(nameof(EventLogViewModel.HasNoFilter)),
        };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(header);
        stack.Children.Add(hint);
        stack.Children.Add(noFilter);
        stack.Children.Add(rows);

        return new Border
        {
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Background = TabIdle,
            BorderBrush = TabBorder,
            Child = stack,
            [!IsVisibleProperty] = new Binding(nameof(EventLogViewModel.ShowSettings)),
        };
    }

    private static Control BuildFilterRow(EventLogViewModel viewModel)
    {
        EventLogFilterViewModel? model = null;

        var box = new CheckBox
        {
            // Sans couleur explicite, le libelle herite du noir du theme, illisible sur ce fond
            // sombre (meme correctif que la bascule globale de l'onglet Automatisation).
            Foreground = BodyText,
            [!ToggleButton.IsCheckedProperty] = new Binding(nameof(EventLogFilterViewModel.IsChecked)),
            [!ContentProperty] = new Binding(nameof(EventLogFilterViewModel.Label)),
        };

        box.DataContextChanged += (_, _) => model = box.DataContext as EventLogFilterViewModel;
        box.Click += (_, _) => { if (model != null) viewModel.ToggleFilter(model); };

        return box;
    }

    private static Control BuildCard()
    {
        var title = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap,
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogEntryViewModel.Title)),
            [!TextBlock.ForegroundProperty] = new Binding(nameof(EventLogEntryViewModel.Tone))
            {
                Converter = new FuncValueConverter<SkiaLayer.EventLogTone, IBrush>(TitleBrush),
            },
        };

        var body = new TextBlock
        {
            FontSize = 12,
            Foreground = BodyText,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 3, 0, 0),
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogEntryViewModel.Body)),
        };

        var content = new StackPanel { Orientation = Orientation.Vertical };
        content.Children.Add(title);
        content.Children.Add(body);

        return new Border
        {
            Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Child = content,
            [!BackgroundProperty] = new Binding(nameof(EventLogEntryViewModel.Tone))
            {
                Converter = new FuncValueConverter<SkiaLayer.EventLogTone, IBrush>(CardBrush),
            },
            [!BorderBrushProperty] = new Binding(nameof(EventLogEntryViewModel.Tone))
            {
                Converter = new FuncValueConverter<SkiaLayer.EventLogTone, IBrush>(ToneBorderBrush),
            },
        };
    }

    // Memes couleurs que le rendu Skia.

    private static IBrush CardBrush(SkiaLayer.EventLogTone tone) => tone switch
    {
        SkiaLayer.EventLogTone.Warning => new SolidColorBrush(Color.FromArgb(220, 50, 25, 5)),
        SkiaLayer.EventLogTone.Success => new SolidColorBrush(Color.FromArgb(220, 15, 45, 15)),
        SkiaLayer.EventLogTone.Reward => new SolidColorBrush(Color.FromArgb(220, 50, 40, 10)),
        SkiaLayer.EventLogTone.Discovery => new SolidColorBrush(Color.FromArgb(220, 10, 40, 30)),
        _ => new SolidColorBrush(Color.FromArgb(220, 70, 15, 15)),
    };

    private static IBrush ToneBorderBrush(SkiaLayer.EventLogTone tone) => tone switch
    {
        SkiaLayer.EventLogTone.Warning => new SolidColorBrush(Color.FromRgb(210, 100, 20)),
        SkiaLayer.EventLogTone.Success => new SolidColorBrush(Color.FromRgb(70, 200, 70)),
        SkiaLayer.EventLogTone.Reward => new SolidColorBrush(Color.FromRgb(200, 160, 30)),
        SkiaLayer.EventLogTone.Discovery => new SolidColorBrush(Color.FromRgb(40, 190, 120)),
        _ => new SolidColorBrush(Color.FromRgb(200, 50, 50)),
    };

    private static IBrush TitleBrush(SkiaLayer.EventLogTone tone) => tone switch
    {
        SkiaLayer.EventLogTone.Warning => new SolidColorBrush(Color.FromRgb(240, 130, 40)),
        SkiaLayer.EventLogTone.Success => new SolidColorBrush(Color.FromRgb(100, 220, 100)),
        SkiaLayer.EventLogTone.Reward => new SolidColorBrush(Color.FromRgb(255, 210, 60)),
        SkiaLayer.EventLogTone.Discovery => new SolidColorBrush(Color.FromRgb(80, 225, 150)),
        _ => new SolidColorBrush(Color.FromRgb(255, 100, 100)),
    };
}
