using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Barre horizontale des ressources du joueur.
///
/// Le debordement est gere par un ScrollViewer natif : il remplace les fleches de pagination,
/// la barre de defilement dessinee a la main et le glisser tactile de l'ancien renderer, qui
/// devaient en plus etre declares dans IsPointBlockedByUI pour ne pas laisser fuir les clics.
/// </summary>
public sealed class ResourceBarView : UserControl
{
    public ResourceBarView(ResourceBarViewModel viewModel, SvgIconCache icons)
    {
        DataContext = viewModel;
        this[!IsVisibleProperty] = new Binding(nameof(ResourceBarViewModel.IsAvailable));

        var items = new ItemsControl
        {
            [!ItemsControl.ItemsSourceProperty] = new Binding(nameof(ResourceBarViewModel.Resources)),
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                VerticalAlignment = VerticalAlignment.Center,
            }),
            ItemTemplate = new FuncDataTemplate<ResourceItemViewModel>(
                (_, _) => new ResourcePill(icons), supportsRecycling: true),
        };

        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = items,
        };
    }
}

/// <summary>Pastille d'une ressource : icone a gauche, quantite/max a droite.</summary>
internal sealed class ResourcePill : Border
{
    private const int IconPixelSize = 22;

    private static readonly SolidColorBrush NormalText = new(Colors.White);
    private static readonly SolidColorBrush AtMaxText = new(Color.FromRgb(120, 230, 140));

    private static readonly Animation Flicker = new()
    {
        Duration = TimeSpan.FromSeconds(0.6),
        IterationCount = IterationCount.Infinite,
        Easing = new SineEaseInOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0d),   Setters = { new Setter(OpacityProperty, 1.0d) } },
            new KeyFrame { Cue = new Cue(0.5d), Setters = { new Setter(OpacityProperty, 0.4d) } },
            new KeyFrame { Cue = new Cue(1d),   Setters = { new Setter(OpacityProperty, 1.0d) } },
        },
    };

    private readonly SvgIconCache _icons;
    private readonly Image _icon;
    private readonly TextBlock _quantity;
    private ResourceItemViewModel? _item;
    private CancellationTokenSource? _flickerCancellation;

    public ResourcePill(SvgIconCache icons)
    {
        _icons = icons;

        _icon = new Image { Width = IconPixelSize, Height = IconPixelSize, VerticalAlignment = VerticalAlignment.Center };
        _quantity = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            Foreground = NormalText,
        };

        Width = 66;
        Height = 32;
        CornerRadius = new CornerRadius(4);
        Background = new SolidColorBrush(Color.FromArgb(210, 40, 40, 40));
        Padding = new Thickness(4, 0);

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_icon, Dock.Left);
        layout.Children.Add(_icon);
        layout.Children.Add(_quantity);
        Child = layout;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_item != null) _item.PropertyChanged -= OnItemChanged;
        _item = DataContext as ResourceItemViewModel;
        if (_item != null)
        {
            _item.PropertyChanged += OnItemChanged;
            _icon.Source = _icons.GetResourceIcon(_item.IconName, IconPixelSize);
        }

        ApplyState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopFlicker();
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ApplyState();

    private void ApplyState()
    {
        if (_item == null)
        {
            StopFlicker();
            return;
        }

        // Deux lignes quand la capacite est grande, comme l'ancien renderer : sinon
        // "12,3k/45,6k" deborde de la pastille.
        _quantity.Text = _item.MaxLabel.Length > 4
            ? $"{_item.QuantityLabel}\n/{_item.MaxLabel}"
            : $"{_item.QuantityLabel}/{_item.MaxLabel}";

        _quantity.Foreground = _item.IsAtMax ? AtMaxText : NormalText;

        if (_item.IsFlickering) StartFlicker();
        else StopFlicker();
    }

    private void StartFlicker()
    {
        if (_flickerCancellation != null) return;
        _flickerCancellation = new CancellationTokenSource();
        _ = Flicker.RunAsync(this, _flickerCancellation.Token);
    }

    private void StopFlicker()
    {
        if (_flickerCancellation == null) return;
        _flickerCancellation.Cancel();
        _flickerCancellation.Dispose();
        _flickerCancellation = null;
        Opacity = 1d;
    }
}
