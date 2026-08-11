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

/// <summary>Ou la barre d'onglets est posee dans <c>GameView</c>.</summary>
public enum TabBarPlacement
{
    /// Dans la barre du haut, a gauche des ressources : onglets a largeur naturelle.
    Top,

    /// Ancree au bas de l'ecran, pleine largeur : disposition compacte (navigateur mobile,
    /// fenetre etroite, ou reglage « menus en bas »).
    Bottom,
}

/// <summary>
/// Barre d'onglets.
///
/// Chaque onglet est un <see cref="TabButton"/> qui porte lui-meme son etat visuel : la liste
/// etant mise a jour en place par le ViewModel, les boutons ne sont pas recrees a chaque
/// rafraichissement et conservent survol et animation.
/// </summary>
public sealed class TabBarView : ItemsControl
{
    /// Hauteur de la barre ancree en bas — UILayoutService.MobileTabBarHeight.
    public const double BottomBarHeight = 44;

    /// <summary>
    /// Obligatoire. Les ControlTheme d'Avalonia sont indexes sur le type EXACT : une classe
    /// derivee ne recoit pas le template de sa classe de base. Sans cette redirection,
    /// Presenter reste null, aucun onglet n'est instancie, et la barre disparait — sans lever
    /// la moindre erreur. C'est ce qui avait fait disparaitre toute la barre d'onglets.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(ItemsControl);

    public TabBarView(TabBarViewModel viewModel, TabBarPlacement placement = TabBarPlacement.Top)
    {
        DataContext = viewModel;
        this[!ItemsSourceProperty] = new Binding(nameof(TabBarViewModel.Tabs));

        // Chaque exemplaire ne s'affiche que dans la disposition qui le concerne : sans cela les
        // deux barres seraient visibles en meme temps.
        this[!IsVisibleProperty] = new Binding(placement == TabBarPlacement.Bottom
            ? nameof(TabBarViewModel.ShowAtBottom)
            : nameof(TabBarViewModel.ShowAtTop));

        ItemsPanel = placement == TabBarPlacement.Bottom
            ? new FuncTemplate<Panel?>(() => new EvenColumnsPanel())
            : new FuncTemplate<Panel?>(() => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
            });

        ItemTemplate = new FuncDataTemplate<TabItemViewModel>(
            (_, _) => new TabButton(viewModel, placement), supportsRecycling: true);
    }
}

/// <summary>
/// Repartit ses enfants en colonnes de largeur egale sur toute la largeur disponible, comme le
/// faisait l'ancien rendu Skia de la barre du bas (<c>canvasWidth / nbOnglets</c>).
///
/// Ni StackPanel ni UniformGrid ne conviennent : le premier laisse chaque onglet a sa largeur
/// naturelle et deborde des l'ouverture de l'inframonde, le second raisonne en lignes et
/// colonnes alors qu'il n'y a jamais qu'une ligne, dont la hauteur doit etre celle de la barre.
/// </summary>
internal sealed class EvenColumnsPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        int count = 0;
        foreach (var child in Children) if (child.IsVisible) count++;
        if (count == 0) return default;

        // Une largeur infinie n'arrive qu'en mesure hors contrainte : on retombe alors sur la
        // largeur naturelle des onglets plutot que de produire une colonne infinie.
        double columnWidth = double.IsInfinity(availableSize.Width)
            ? double.PositiveInfinity
            : availableSize.Width / count;

        double height = 0;
        double naturalWidth = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Measure(new Size(columnWidth, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            naturalWidth += child.DesiredSize.Width;
        }

        return new Size(
            double.IsInfinity(columnWidth) ? naturalWidth : availableSize.Width,
            height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = 0;
        foreach (var child in Children) if (child.IsVisible) count++;
        if (count == 0) return finalSize;

        double columnWidth = finalSize.Width / count;
        int index = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;
            child.Arrange(new Rect(index * columnWidth, 0, columnWidth, finalSize.Height));
            index++;
        }

        return finalSize;
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

    public TabButton(TabBarViewModel owner, TabBarPlacement placement = TabBarPlacement.Top)
    {
        _owner = owner;
        bool atBottom = placement == TabBarPlacement.Bottom;

        // L'onglet actif se distingue par son fond, et un onglet qui reclame l'attention par
        // son orange : sans cela le survol les remplacerait par la couleur du theme.
        Classes.Add(GameControlStyles.ToneButton);

        _label = new TextBlock
        {
            FontSize = 12,
            // L'interligne doit rester >= a la hauteur de glyphe (~1.2 x FontSize) : plus bas,
            // Avalonia rogne le bas de chaque ligne (jambages, et meme la ligne de base).
            // C'est la hauteur du bouton qui absorbe les libelles sur deux lignes
            // ("Infra\nmonde"), pas un interligne ecrase.
            LineHeight = 15,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // En bas, c'est la barre qui impose la largeur : chaque onglet prend sa colonne, et une
        // largeur minimale ferait deborder la barre des que le nombre d'onglets grimpe (onze au
        // maximum, contre 400 px de large sur un telephone).
        MinWidth = atBottom ? 0 : 62;
        // Deux lignes de 15 px + la bordure : 34 px, ce qui tient dans les 50 px de la barre.
        // En bas, l'onglet remplit la hauteur de la barre.
        if (atBottom) HorizontalAlignment = HorizontalAlignment.Stretch;
        else Height = 34;
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
