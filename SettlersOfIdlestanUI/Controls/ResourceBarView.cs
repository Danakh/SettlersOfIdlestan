using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Barre horizontale des ressources du joueur.
///
/// Le debordement est gere par un ScrollViewer natif : il remplace les fleches de pagination
/// de l'ancien renderer, qui devaient en plus etre declarees dans IsPointBlockedByUI pour ne
/// pas laisser fuir les clics. Le glisser, lui, est reconduit tel quel (voir OnPointerPressed),
/// avec la molette en plus.
///
/// L'ascenseur du theme Fluent, lui, n'est pas repris : epais et opaque, il mange le tiers de
/// la hauteur d'une barre de 50 px. On garde le ScrollViewer pour sa logique de defilement et
/// on redessine l'indicateur fin de l'ancien rendu (<see cref="ScrollIndicator"/>).
///
/// Le repli sur une seconde ligne quand la largeur manque est du ressort de TopBarView : cette
/// vue ne fait defiler que ce qui deborde encore d'une ligne entiere.
/// </summary>
public sealed class ResourceBarView : UserControl
{
    /// Pastille + espacement : le pas d'un cran de molette, comme les fleches de pagination
    /// de l'ancien renderer faisaient defiler d'exactement une pastille.
    internal const double PillStep = ResourcePill.PillWidth + ItemSpacing;

    private const double ItemSpacing = 16;

    /// Deplacement en deca duquel un appui reste un tapotement plutot qu'un glissement.
    private const double TapMaxDistance = 6;

    private static readonly Cursor GrabCursor = new(StandardCursorType.SizeWestEast);

    private readonly ScrollViewer _scroller;

    private double _dragOriginX;
    private double _dragOriginOffset;
    private bool _isDragging;

    private Point _pressPosition;
    private bool _hasPress;

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
                Spacing = ItemSpacing,
                VerticalAlignment = VerticalAlignment.Center,
            }),
            ItemTemplate = new FuncDataTemplate<ResourceItemViewModel>(
                (_, _) => new ResourcePill(icons, viewModel.GetTooltip), supportsRecycling: true),
        };

        _scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = items,
        };

        Content = new Panel { Children = { _scroller, new ScrollIndicator(_scroller) } };

        // En tunnel : le ScrollViewer voit la molette avant nous en bulle, et ne convertit pas
        // une molette verticale — la seule que produit une souris ordinaire — en defilement
        // horizontal. Sans cela la barre ne defilerait qu'au doigt.
        AddHandler(PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel);
    }

    /// Ce dont la barre peut encore defiler : 0 tant que tout le contenu tient.
    private double MaxOffset => Math.Max(0, _scroller.Extent.Width - _scroller.Viewport.Width);

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        double notches = e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;
        if (notches == 0 || MaxOffset <= 0) return;

        ScrollTo(_scroller.Offset.X - notches * PillStep);
        e.Handled = true;
    }

    /// <summary>
    /// Saisie de la barre a la souris. Le tactile a deja son geste natif — avec son inertie —
    /// et le laisser passer ici le ferait defiler deux fois plus vite.
    ///
    /// Aucun seuil de deplacement a respecter avant de saisir : rien n'est cliquable dans la
    /// barre, les pastilles ne portent qu'une infobulle. Un appui qui ne deplace pas la barre
    /// est en revanche relu comme un tapotement, seul moyen d'ouvrir cette infobulle au doigt
    /// (voir <see cref="ShowTooltipAt"/>).
    /// </summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        _pressPosition = e.GetPosition(this);
        _hasPress = true;

        if (e.Pointer.Type != PointerType.Mouse || MaxOffset <= 0) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _isDragging = true;
        _dragOriginX = e.GetPosition(this).X;
        _dragOriginOffset = _scroller.Offset.X;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        // Le curseur annonce la saisie possible, comme le faisait le glisser de l'ancien rendu.
        Cursor = MaxOffset > 0 ? GrabCursor : null;

        if (!_isDragging) return;

        // Rapporte a l'origine du geste et non a l'evenement precedent : un cumul de deltas
        // arrondis derive, et la barre finit decalee du contenu saisi.
        ScrollTo(_dragOriginOffset - (e.GetPosition(this).X - _dragOriginX));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e.Pointer);

        if (!_hasPress) return;
        _hasPress = false;

        var position = e.GetPosition(this);
        double dx = position.X - _pressPosition.X;
        double dy = position.Y - _pressPosition.Y;
        if (dx * dx + dy * dy > TapMaxDistance * TapMaxDistance) return;

        ShowTooltipAt(position);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;

        // Le geste est parti en defilement — le ScrollViewer a saisi le pointeur et le
        // relachement ne nous reviendra pas. Ce n'etait donc pas un tapotement.
        _hasPress = false;
    }

    /// <summary>
    /// Un tapotement sur une pastille en affiche l'infobulle. Au doigt c'est le seul moyen de la
    /// consulter : il n'y a pas de survol, donc rien qui declenche jamais ToolTipService.
    ///
    /// La pastille est retrouvee par test de collision et non via <c>e.Source</c> : quand le
    /// glisser a la souris a saisi le pointeur, l'evenement est route vers la barre et Source
    /// designe cette derniere, et non la pastille touchee.
    /// </summary>
    private void ShowTooltipAt(Point position)
    {
        if (this.InputHitTest(position) is Visual hit
            && hit.FindAncestorOfType<ResourcePill>(includeSelf: true) is { } pill)
            pill.ShowTappedTooltip();
        else
            TapTooltip.Hide();
    }

    private void EndDrag(IPointer pointer)
    {
        if (!_isDragging) return;
        _isDragging = false;
        pointer.Capture(null);
    }

    private void ScrollTo(double offsetX) =>
        _scroller.Offset = _scroller.Offset.WithX(Math.Clamp(offsetX, 0, MaxOffset));
}

/// <summary>
/// Ascenseur horizontal de 3 px, repris tel quel de l'ancien rendu Skia : un rail et un pouce,
/// sans zone de saisie. Purement indicatif — le defilement se fait a la molette ou au doigt.
/// </summary>
internal sealed class ScrollIndicator : Control
{
    private const double BarThickness = 3;

    private static readonly IBrush TrackBrush = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
    private static readonly IBrush ThumbBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));

    private readonly ScrollViewer _scroller;

    public ScrollIndicator(ScrollViewer scroller)
    {
        _scroller = scroller;

        Height = BarThickness;
        Margin = new Thickness(0, 0, 0, 2);
        VerticalAlignment = VerticalAlignment.Bottom;

        // Pose par-dessus le contenu : sans cela il volerait les clics des pastilles.
        IsHitTestVisible = false;

        // Rien ne redessine un Control sur un changement de proprietes d'un autre controle.
        scroller.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.OffsetProperty
                || e.Property == ScrollViewer.ExtentProperty
                || e.Property == ScrollViewer.ViewportProperty)
                InvalidateVisual();
        };
    }

    public override void Render(DrawingContext context)
    {
        double viewport = _scroller.Viewport.Width;
        double extent = _scroller.Extent.Width;
        double width = Bounds.Width;

        // Rien a montrer tant que tout tient : l'ancien renderer sortait sur la meme condition.
        if (viewport <= 0 || width <= 0 || extent <= viewport + 0.5) return;

        double radius = BarThickness / 2;
        context.DrawRectangle(TrackBrush, null,
            new RoundedRect(new Rect(0, 0, width, BarThickness), radius));

        double thumbWidth = width * (viewport / extent);
        double thumbX = _scroller.Offset.X / (extent - viewport) * (width - thumbWidth);
        context.DrawRectangle(ThumbBrush, null,
            new RoundedRect(new Rect(thumbX, 0, thumbWidth, BarThickness), radius));
    }
}

/// <summary>Pastille d'une ressource : icone a gauche, quantite/max a droite.</summary>
internal sealed class ResourcePill : Border
{
    internal const double PillWidth = 66;

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
    private readonly Func<string, string?> _tooltip;
    private readonly Image _icon;
    private readonly TextBlock _quantity;
    private ResourceItemViewModel? _item;
    private CancellationTokenSource? _flickerCancellation;

    public ResourcePill(SvgIconCache icons, Func<string, string?> tooltip)
    {
        _icons = icons;
        _tooltip = tooltip;

        _icon = new Image { Width = IconPixelSize, Height = IconPixelSize, VerticalAlignment = VerticalAlignment.Center };
        _quantity = new TextBlock
        {
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            Foreground = NormalText,
        };

        Width = PillWidth;
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
        UpdateTooltip();
    }

    /// <summary>
    /// Les taux affiches sont rafraichis a l'entree du pointeur. Ils le sont aussi des le
    /// changement de DataContext, et pas seulement ici : ToolTipService ne s'abonne au survol
    /// qu'au moment ou Tip passe de null a non-null, donc une pastille sans infobulle initiale
    /// resterait muette pendant tout le premier survol.
    /// </summary>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateTooltip();
    }

    /// <summary>
    /// Affiche l'infobulle sur un tapotement. Les taux sont rafraichis au passage, comme ils le
    /// sont a l'entree du pointeur : sans survol prealable, rien ne les aurait recalcules.
    /// </summary>
    internal void ShowTappedTooltip()
    {
        UpdateTooltip();
        TapTooltip.Show(this);
    }

    private void UpdateTooltip()
    {
        if (_item == null) return;
        ToolTip.SetTip(this, _tooltip(_item.IconName));
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
