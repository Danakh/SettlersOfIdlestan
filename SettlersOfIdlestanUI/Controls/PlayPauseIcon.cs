using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Triangle de lecture et double barre de pause du bouton de controle du temps.
///
/// Dessines geometriquement plutot qu'ecrits en caracteres (« ▶ », « ❚❚ ») : ces glyphes
/// n'existent pas dans toutes les polices embarquees, et un glyphe manquant se rend en carre
/// vide — sur le seul bouton qui doit dire d'un coup d'oeil si la partie tourne.
///
/// Contour sombre autour du remplissage blanc : le fond du bouton oscille entre le dore et le
/// noir en pause, et le blanc seul disparaitrait presque sur le dore.
/// </summary>
internal static class PlayPauseIcon
{
    private const double Size = 12;
    private const double BarWidth = 4;
    private const double StrokeWidth = 1;

    private static readonly SolidColorBrush Fill = new(Colors.White);
    private static readonly SolidColorBrush Outline = new(Color.FromRgb(25, 25, 32));

    /// <summary>Triangle plein pointant a droite — la partie tourne.</summary>
    public static Control CreatePlay()
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Legerement plus etroit que haut : un triangle inscrit dans un carre parait
            // trop lourd a cote de la double barre.
            ctx.BeginFigure(new Point(1, 0), isFilled: true);
            ctx.LineTo(new Point(Size - 1, Size / 2));
            ctx.LineTo(new Point(1, Size));
            ctx.EndFigure(isClosed: true);
        }

        return new AvaloniaPath
        {
            Data = geometry,
            Fill = Fill,
            Stroke = Outline,
            StrokeThickness = StrokeWidth,
            StrokeJoin = PenLineJoin.Round,
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>Deux barres verticales — la partie est en pause.</summary>
    public static Control CreatePause() => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 3,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Children = { Bar(), Bar() },
    };

    private static Rectangle Bar() => new()
    {
        Width = BarWidth,
        Height = Size,
        Fill = Fill,
        Stroke = Outline,
        StrokeThickness = StrokeWidth,
        RadiusX = 1,
        RadiusY = 1,
    };
}
