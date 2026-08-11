using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Boutons − / + de zoom, premier element d'overlay sorti du rendu Skia.
///
/// Sert de modele aux panneaux suivants : le controle se contente d'appeler le runtime,
/// et c'est l'arbre visuel Avalonia — et non une liste maintenue a la main — qui garantit
/// qu'un clic sur ces boutons n'atteint jamais la carte.
/// </summary>
public sealed class ZoomControlView : UserControl
{
    /// <param name="onZoomIn">Action de zoom avant — injectee plutot que prise sur le runtime
    /// pour que le controle reste testable sans demarrer une partie complete.</param>
    /// <param name="onZoomOut">Action de zoom arriere.</param>
    public ZoomControlView(Action onZoomIn, Action onZoomOut)
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(0, 0, 10, 10);

        // Ne capte le pointeur que sur les boutons eux-memes : sans cela, le conteneur
        // avalerait les clics de la zone vide autour et bloquerait le pan de la carte.
        Background = null;

        var zoomOut = CreateButton("-", onZoomOut);
        var zoomIn = CreateButton("+", onZoomIn);

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { zoomOut, zoomIn },
        };
    }

    private static Button CreateButton(string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(220, 35, 35, 50)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 130)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
