using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Layout;
using Avalonia.Media;

namespace SettlersOfIdlestanUI.Controls;

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

    /// <param name="onClose">Null pour un panneau sans bouton de fermeture.</param>
    public GamePanelView(string title, Action? onClose)
    {
        Width = DefaultWidth + CollapseTabWidth;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;

        TitleBlock = new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var header = new DockPanel { LastChildFill = true, Height = 32, Margin = new Thickness(10, 0) };

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

        var stack = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);
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
        _collapseTab.IsCheckedChanged += (_, _) => ApplyCollapsed();
        ApplyCollapsed();

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(_collapseTab, Dock.Left);
        layout.Children.Add(_collapseTab);
        layout.Children.Add(_body);

        base.Content = layout;
    }

    private const double CollapseTabWidth = 22;

    /// Titre du panneau, mis a jour par la vue qui l'utilise.
    public TextBlock TitleBlock { get; }

    /// Zone defilante ou la vue place son contenu.
    public ScrollViewer ContentHost { get; }

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
        _collapseTab.Content = new AvaloniaPath
        {
            Data = Geometry.Parse(IsCollapsed ? "M 0,0 L 6,5 L 0,10 Z" : "M 6,0 L 0,5 L 6,10 Z"),
            Fill = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
