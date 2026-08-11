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
    private static readonly SolidColorBrush Background = new(Color.FromArgb(240, 18, 18, 24));
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(255, 215, 0));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(120, 120, 130));
    private static readonly SolidColorBrush BodyText = new(Color.FromRgb(200, 200, 210));

    public EventLogView(EventLogViewModel viewModel)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(EventLogViewModel.IsVisible));

        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;

        var header = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Accent,
            Margin = new Thickness(0, 0, 0, 14),
            [!TextBlock.TextProperty] = new Binding(nameof(EventLogViewModel.Title)),
        };

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

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(header);
        stack.Children.Add(empty);
        stack.Children.Add(entries);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            // Colonne centree et bornee, comme le rendu Skia.
            Content = new Border { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Center, Child = stack },
            Padding = new Thickness(20),
        };

        Content = new Border { Background = Background, Child = scroll };
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
                Converter = new FuncValueConverter<SkiaLayer.EventLogTone, IBrush>(BorderBrush),
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

    private static IBrush BorderBrush(SkiaLayer.EventLogTone tone) => tone switch
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
