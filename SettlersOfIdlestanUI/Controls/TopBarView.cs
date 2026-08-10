using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SettlersOfIdlestanUI.ViewModels;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Barre du haut : onglets a gauche, ressources au centre, temps et engrenage a droite.
///
/// Les quatre blocs migrent ensemble parce qu'ils partagent un meme layout : UILayoutService
/// reservait la place de chacun pour les autres. C'est desormais <see cref="TopBarLayout"/>
/// qui s'en charge, en reprenant sa regle de repli — quand les trois blocs ne tiennent pas
/// ensemble, les ressources passent sur leur propre ligne plutot que de se reduire a une
/// fenetre defilante de quelques pastilles.
///
/// La hauteur n'est donc plus constante : <see cref="TotalHeight"/> vaut <see cref="BarHeight"/>
/// ou une ligne de plus. Tout ce qui s'ancre sous la barre — panneaux lateraux, menu parametres,
/// vues plein ecran encore dessinees en Skia — doit suivre <see cref="TotalHeightChanged"/>.
/// </summary>
public sealed class TopBarView : UserControl
{
    /// Hauteur de la ligne principale (onglets, temps, engrenage) — UILayoutService.TopBarHeight.
    public const double BarHeight = 50;

    private readonly TopBarLayout _layout;

    /// Hauteur totale de la barre, seconde ligne comprise.
    public double TotalHeight => _layout.TotalHeight;

    /// Leve quand les ressources se replient sur leur propre ligne, ou en reviennent.
    public event Action<double>? TotalHeightChanged;

    public TopBarView(
        TabBarViewModel tabs,
        ResourceBarViewModel resources,
        TimeControlViewModel time,
        SvgIconCache icons,
        Func<string, string> localize,
        Func<string, object[], string> localizeFormat,
        Action onGearClicked)
    {
        VerticalAlignment = VerticalAlignment.Top;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var gear = new Button
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = GearIcon.Create(24, Brushes.Gold),
        };
        gear.Click += (_, _) => onGearClicked();

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TimeControlView(time, localize, localizeFormat),
                gear,
            },
        };

        var tabBar = new TabBarView(tabs) { VerticalAlignment = VerticalAlignment.Center };
        // Pas de VerticalAlignment ici : la barre occupe toute la hauteur de sa ligne, ce qui
        // pose son ascenseur au bas de la ligne et non au bas des pastilles.
        var resourceBar = new ResourceBarView(resources, icons);

        _layout = new TopBarLayout(tabBar, resourceBar, right) { Margin = new Thickness(8, 0) };
        _layout.TotalHeightChanged += h => TotalHeightChanged?.Invoke(h);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
            BorderBrush = Brushes.Gold,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = _layout,
        };
    }
}

/// <summary>
/// Disposition des trois blocs de la barre du haut.
///
/// Un DockPanel ne saurait pas la faire : il retrecirait les ressources jusqu'a une fenetre de
/// deux pastilles plutot que de leur donner une ligne entiere. La regle reprise de
/// UILayoutService est la meme depuis toujours — onglets + ressources + temps ne tenant pas
/// ensemble, ce sont les ressources qui descendent, et elles ne defilent que si une ligne
/// entiere ne leur suffit toujours pas.
/// </summary>
internal sealed class TopBarLayout : Panel
{
    /// Hauteur d'une ligne repliee — UILayoutService.SecondRowHeight.
    internal const double SecondRowHeight = 36;

    /// Respiration entre deux blocs voisins de la ligne principale.
    private const double BlockGap = 8;

    private readonly Control _tabs;
    private readonly Control _resources;
    private readonly Control _right;

    private bool _resourcesOnOwnRow;

    public TopBarLayout(Control tabs, Control resources, Control right)
    {
        _tabs = tabs;
        _resources = resources;
        _right = right;

        Children.Add(tabs);
        Children.Add(right);
        Children.Add(resources);
    }

    /// Vrai quand les ressources ont ete releguees sur leur propre ligne.
    public bool ResourcesOnOwnRow => _resourcesOnOwnRow;

    public double TotalHeight => TopBarView.BarHeight + (_resourcesOnOwnRow ? SecondRowHeight : 0);

    public event Action<double>? TotalHeightChanged;

    protected override Size MeasureOverride(Size availableSize)
    {
        var mainRow = new Size(double.PositiveInfinity, TopBarView.BarHeight);
        _tabs.Measure(mainRow);
        _right.Measure(mainRow);

        // Mesuree sans borne, la barre de ressources rend la largeur de tout son contenu :
        // c'est elle qu'il faut comparer a la place restante, pas la largeur qu'elle accepte
        // de prendre une fois clipee par son ScrollViewer (qui tient toujours).
        _resources.Measure(mainRow);
        double naturalResourceWidth = _resources.DesiredSize.Width;

        double sides = _tabs.DesiredSize.Width + _right.DesiredSize.Width + 2 * BlockGap;
        double width = double.IsInfinity(availableSize.Width)
            ? sides + naturalResourceWidth
            : availableSize.Width;

        SetResourcesOnOwnRow(naturalResourceWidth > width - sides);

        double rowHeight = _resourcesOnOwnRow ? SecondRowHeight : TopBarView.BarHeight;
        double rowWidth = _resourcesOnOwnRow ? width : Math.Max(0, width - sides);
        _resources.Measure(new Size(rowWidth, rowHeight));

        return new Size(width, TotalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double tabsWidth = _tabs.DesiredSize.Width;
        double rightWidth = _right.DesiredSize.Width;

        _tabs.Arrange(new Rect(0, 0, tabsWidth, TopBarView.BarHeight));
        _right.Arrange(new Rect(
            Math.Max(0, finalSize.Width - rightWidth), 0, rightWidth, TopBarView.BarHeight));

        if (_resourcesOnOwnRow)
        {
            _resources.Arrange(new Rect(0, TopBarView.BarHeight, finalSize.Width, SecondRowHeight));
        }
        else
        {
            double left = tabsWidth + BlockGap;
            double available = Math.Max(0, finalSize.Width - rightWidth - BlockGap - left);
            _resources.Arrange(new Rect(left, 0, available, TopBarView.BarHeight));
        }

        return new Size(finalSize.Width, TotalHeight);
    }

    private void SetResourcesOnOwnRow(bool onOwnRow)
    {
        if (_resourcesOnOwnRow == onOwnRow) return;
        _resourcesOnOwnRow = onOwnRow;
        TotalHeightChanged?.Invoke(TotalHeight);
    }
}
