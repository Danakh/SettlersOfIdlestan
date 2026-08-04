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
/// reservait la place de chacun pour les autres. Ici c'est le DockPanel qui s'en charge, et
/// le debordement des ressources devient un simple defilement natif.
///
/// La hauteur est volontairement figee a <see cref="BarHeight"/> = UILayoutService.TopBarHeight :
/// le menu parametres et les panneaux lateraux sont toujours dessines en Skia et s'ancrent
/// dessous. Une hauteur differente les decalerait.
/// </summary>
public sealed class TopBarView : UserControl
{
    public const double BarHeight = 50;

    public TopBarView(
        TabBarViewModel tabs,
        ResourceBarViewModel resources,
        TimeControlViewModel time,
        SvgIconCache icons,
        Func<string, string> localize,
        Func<string, object[], string> localizeFormat,
        Action onGearClicked)
    {
        Height = BarHeight;
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
        var resourceBar = new ResourceBarView(resources, icons) { VerticalAlignment = VerticalAlignment.Center };

        var layout = new DockPanel { LastChildFill = true, Margin = new Thickness(8, 0) };
        DockPanel.SetDock(tabBar, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        layout.Children.Add(tabBar);
        layout.Children.Add(right);
        // Ajoutees en dernier : les ressources occupent la place restante et se retractent
        // d'elles-memes plutot que de pousser les onglets ou le bloc temps hors de l'ecran.
        layout.Children.Add(resourceBar);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
            BorderBrush = Brushes.Gold,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = layout,
        };
    }
}
