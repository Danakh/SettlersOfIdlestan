using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Barre d'onglets de la barre du haut.
///
/// Chaque onglet est un <see cref="TabButton"/> qui porte lui-meme son etat visuel : la liste
/// etant mise a jour en place par le ViewModel, les boutons ne sont pas recrees a chaque
/// rafraichissement et conservent survol et animation.
/// </summary>
public sealed class TabBarView : ItemsControl
{
    /// <summary>
    /// Obligatoire. Les ControlTheme d'Avalonia sont indexes sur le type EXACT : une classe
    /// derivee ne recoit pas le template de sa classe de base. Sans cette redirection,
    /// Presenter reste null, aucun onglet n'est instancie, et la barre disparait — sans lever
    /// la moindre erreur. C'est ce qui avait fait disparaitre toute la barre d'onglets.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    public TabBarView(TabBarViewModel viewModel)
    {
        DataContext = viewModel;
        this[!ItemsSourceProperty] = new Binding(nameof(TabBarViewModel.Tabs));
        this[!IsVisibleProperty] = new Binding(nameof(TabBarViewModel.IsVisible));

        ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
        });

        ItemTemplate = new FuncDataTemplate<TabItemViewModel>(
            (_, _) => new TabButton(viewModel), supportsRecycling: true);
    }
}

/// <summary>
/// Bouton d'un onglet. Reflete IsActive / IsGlowing et pilote la pulsation d'attention avec
/// une animation Avalonia, la ou l'ancien renderer recalculait une sinusoide sur
/// Environment.TickCount64 a chaque frame.
/// </summary>
internal sealed class TabButton : Button
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(60, 100, 160));
    private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(35, 35, 45));
    private static readonly SolidColorBrush GlowBrush = new(Color.FromRgb(160, 100, 10));
    private static readonly SolidColorBrush ActiveTextBrush = new(Colors.White);
    private static readonly SolidColorBrush InactiveTextBrush = new(Color.FromRgb(180, 180, 185));
    private static readonly SolidColorBrush ActiveBorderBrush = new(Colors.Gold);

    private static readonly Animation Pulse = new()
    {
        Duration = TimeSpan.FromSeconds(1),
        IterationCount = IterationCount.Infinite,
        Easing = new SineEaseInOut(),
        Children =
        {
            new KeyFrame { Cue = new Cue(0d),   Setters = { new Setter(OpacityProperty, 1.0d) } },
            new KeyFrame { Cue = new Cue(0.5d), Setters = { new Setter(OpacityProperty, 0.5d) } },
            new KeyFrame { Cue = new Cue(1d),   Setters = { new Setter(OpacityProperty, 1.0d) } },
        },
    };

    /// <summary>Meme raison que <see cref="TabBarView"/> : sans redirection, pas de template.</summary>

    private readonly TabBarViewModel _owner;
    private readonly TextBlock _label;
    private TabItemViewModel? _tab;
    private CancellationTokenSource? _pulseCancellation;

    public TabButton(TabBarViewModel owner)
    {
        _owner = owner;

        _label = new TextBlock
        {
            FontSize = 12,
            // Les libelles sur deux lignes ("Infra\nmonde") doivent tenir dans les 28 px de
            // hauteur : sans interligne resserre, la seconde ligne est rognee.
            LineHeight = 12,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        MinWidth = 62;
        Height = 28;
        Padding = new Thickness(4, 0);
        CornerRadius = new CornerRadius(5);
        BorderThickness = new Thickness(1);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Content = _label;

        Click += (_, _) => { if (_tab != null) _owner.Select(_tab.TabId); };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_tab != null) _tab.PropertyChanged -= OnTabPropertyChanged;
        _tab = DataContext as TabItemViewModel;
        if (_tab != null) _tab.PropertyChanged += OnTabPropertyChanged;

        ApplyState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopPulse();
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        ApplyState();

    private void ApplyState()
    {
        if (_tab == null)
        {
            StopPulse();
            return;
        }

        _label.Text = _tab.Label;
        _label.Foreground = _tab.IsActive ? ActiveTextBrush : InactiveTextBrush;
        Background = _tab.IsActive ? ActiveBrush : _tab.IsGlowing ? GlowBrush : InactiveBrush;
        BorderBrush = _tab.IsActive ? ActiveBorderBrush : Brushes.Transparent;

        // L'onglet actif n'a plus a reclamer l'attention : on coupe la pulsation.
        if (_tab.IsGlowing && !_tab.IsActive) StartPulse();
        else StopPulse();
    }

    private void StartPulse()
    {
        if (_pulseCancellation != null) return;
        _pulseCancellation = new CancellationTokenSource();
        _ = Pulse.RunAsync(this, _pulseCancellation.Token);
    }

    private void StopPulse()
    {
        if (_pulseCancellation == null) return;
        _pulseCancellation.Cancel();
        _pulseCancellation.Dispose();
        _pulseCancellation = null;
        Opacity = 1d;
    }
}
