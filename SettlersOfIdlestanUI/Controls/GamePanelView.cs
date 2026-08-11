using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>Bord de la fenetre auquel un panneau lateral est ancre.</summary>
public enum GamePanelSide
{
    /// Panneaux contextuels (ville, monument) : l'onglet de repli est sur leur bord gauche.
    Right,

    /// Panneau civilisation : l'onglet de repli est sur son bord droit.
    Left,
}

/// <summary>
/// Chrome commun des panneaux lateraux : fond, bordure, titre, bouton de fermeture, onglet de
/// repli et defilement. Equivalent Avalonia de PanelRendererBase.
///
/// Le defilement est un ScrollViewer natif : plus de calcul manuel de nombre de lignes
/// visibles, de position de curseur ni de rectangle de piste. L'ancien code devait en plus
/// exposer ShouldSuppressInput pour qu'un panneau ignore les clics quand un popup etait
/// dessine par-dessus — l'arbre visuel s'en charge desormais.
/// </summary>
public sealed class GamePanelView : ContentControl
{
    public const double DefaultWidth = 280;

    private readonly ToggleButton _collapseTab;
    private readonly Border _body;
    private readonly GamePanelSide _side;

    /// <param name="onClose">Null pour un panneau sans bouton de fermeture.</param>
    public GamePanelView(string title, Action? onClose, GamePanelSide side = GamePanelSide.Right)
    {
        _side = side;

        // La largeur est posee par ApplyCollapsed : elle suit l'etat de repli.
        HorizontalAlignment = side == GamePanelSide.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;

        TitleBlock = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };

        HeaderHost = new DockPanel { LastChildFill = true, Height = 32, Margin = new Thickness(10, 0) };
        var header = HeaderHost;

        if (onClose != null)
        {
            var close = new Button
            {
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                Content = new TextBlock
                {
                    Text = "✕",
                    FontSize = 12,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                },
                Background = new SolidColorBrush(Color.FromArgb(220, 200, 80, 80)),
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
            };
            close.Click += (_, _) => onClose();
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
        }

        header.Children.Add(TitleBlock);

        ContentHost = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        // Zone ancree en bas du panneau, hors du defilement : onglets, totaux... Dans le
        // ScrollViewer ils defileraient avec la liste et sortiraient de l'ecran.
        FooterHost = new StackPanel { Orientation = Orientation.Vertical };

        var stack = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(FooterHost, Dock.Bottom);
        stack.Children.Add(header);
        stack.Children.Add(FooterHost);
        stack.Children.Add(ContentHost);

        _body = new Border
        {
            Width = DefaultWidth,
            Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 40)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 200, 200, 220)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(0, 0, 0, 10),
            Child = stack,
        };

        _collapseTab = new ToggleButton
        {
            Width = CollapseTabWidth,
            Height = 32,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0),
            Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 40)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, 200, 200, 220)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
        };
        // Conserve fond sombre et bordure claire au survol : voir GameControlStyles.PanelTab.
        _collapseTab.Classes.Add(GameControlStyles.PanelTab);
        _collapseTab.IsCheckedChanged += (_, _) =>
        {
            ApplyCollapsed();
            CollapsedChanged?.Invoke(IsCollapsed);
        };
        ApplyCollapsed();

        // L'onglet se place du cote interieur du panneau : a sa gauche quand il est ancre a
        // droite, a sa droite quand il est ancre a gauche.
        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_collapseTab, side == GamePanelSide.Left ? Dock.Right : Dock.Left);
        layout.Children.Add(_collapseTab);
        layout.Children.Add(_body);

        base.Content = layout;
    }

    private const double CollapseTabWidth = 22;

    /// Titre du panneau, mis a jour par la vue qui l'utilise.
    public TextBlock TitleBlock { get; }

    /// Barre de titre, ouverte aux vues qui y ajoutent leurs propres commandes.
    public DockPanel HeaderHost { get; }

    /// <summary>
    /// Notifie le repli declenche par l'utilisateur. Ne se declenche pas sur un
    /// <see cref="SetCollapsed"/> de meme valeur : l'etat n'a alors pas change.
    /// </summary>
    public Action<bool>? CollapsedChanged { get; set; }

    /// <summary>
    /// Impose l'etat de repli depuis l'exterieur, quand il est detenu ailleurs que dans la vue —
    /// le panneau civilisation se replie tout seul en disposition mobile quand un panneau
    /// lateral droit s'ouvre.
    /// </summary>
    public void SetCollapsed(bool collapsed) => _collapseTab.IsChecked = collapsed;

    /// Zone defilante ou la vue place son contenu.
    public ScrollViewer ContentHost { get; }

    /// Zone fixe en bas du panneau, hors defilement.
    public StackPanel FooterHost { get; }

    /// <summary>
    /// Borne la hauteur de la zone defilante. A poser sur le ScrollViewer lui-meme : une
    /// contrainte placee plus haut dans l'arbre laisse le contenu s'etirer et deborder au lieu
    /// de declencher le defilement.
    /// </summary>
    public void SetMaxContentHeight(double height)
    {
        if (height > 0) ContentHost.MaxHeight = height;
    }

    /// <summary>Contenu du panneau — redirige vers la zone defilante, pas vers le chrome.</summary>
    public new object? Content
    {
        get => ContentHost.Content;
        set => ContentHost.Content = value;
    }

    private bool IsCollapsed => _collapseTab.IsChecked == true;

    private void ApplyCollapsed()
    {
        _body.IsVisible = !IsCollapsed;

        // Le corps replie est masque mais la largeur reservee restait celle du panneau ouvert :
        // l'onglet, ancre du cote interieur, se retrouvait a 280 px du bord de la fenetre, comme
        // suspendu au milieu de la carte. On rend cette place pour qu'il revienne au bord.
        Width = IsCollapsed ? CollapseTabWidth : DefaultWidth + CollapseTabWidth;

        // La fleche montre le sens du mouvement que le clic va produire, pas le bord d'ancrage :
        // un panneau ancre a droite s'ouvre vers la gauche et se replie vers la droite, un
        // panneau ancre a gauche fait l'inverse.
        bool pointsLeft = IsCollapsed ? _side == GamePanelSide.Right : _side == GamePanelSide.Left;

        _collapseTab.Content = new AvaloniaPath
        {
            Data = Geometry.Parse(pointsLeft ? "M 6,0 L 0,5 L 6,10 Z" : "M 0,0 L 6,5 L 0,10 Z"),
            Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
