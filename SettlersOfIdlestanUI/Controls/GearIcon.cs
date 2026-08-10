using Avalonia;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Icone d'engrenage des parametres.
///
/// Il n'existe pas de SVG pour cette icone : l'ancien renderer la construisait
/// geometriquement. On reprend sa construction exacte — 8 dents, rayon de dent a 1,3x le
/// rayon, moyeu a 0,3x — et surtout son rendu : un TRAIT de 1,5 px, pas un aplat.
///
/// Le detail qui fait tout : les dents forment une seule et meme figure. La derniere ligne
/// d'une dent finit exactement ou commence la suivante (meme angle, meme rayon), si bien que
/// la couronne se referme d'un seul trait continu. Dessinees en figures fermees et remplies,
/// les memes coordonnees donnent huit quartiers separes — une fleur pleine, pas un engrenage.
/// </summary>
internal static class GearIcon
{
    private const int TeethCount = 8;
    private const double StrokeWidth = 1.5;

    public static AvaloniaPath Create(double size, IBrush brush)
    {
        double cx = size / 2, cy = size / 2;
        double radius = size / 2.5;
        double teethRadius = radius * 1.3;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Point(cx, cy, Angle(0), radius), isFilled: false);
            for (int i = 0; i < TeethCount; i++)
            {
                ctx.LineTo(Point(cx, cy, Angle(i + 0.4), teethRadius));
                ctx.LineTo(Point(cx, cy, Angle(i + 0.6), teethRadius));
                ctx.LineTo(Point(cx, cy, Angle(i + 1), radius));
            }
            ctx.EndFigure(isClosed: true);

            AddCircle(ctx, cx, cy, radius * 0.3);
        }

        return new AvaloniaPath
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = StrokeWidth,
            StrokeJoin = PenLineJoin.Round,
            Width = size,
            Height = size,
        };
    }

    private static double Angle(double index) => 360.0 / TeethCount * index * Math.PI / 180.0;

    private static Point Point(double cx, double cy, double angle, double radius) =>
        new(cx + Math.Cos(angle) * radius, cy + Math.Sin(angle) * radius);

    private static void AddCircle(StreamGeometryContext ctx, double cx, double cy, double r)
    {
        ctx.BeginFigure(new Point(cx - r, cy), isFilled: false);
        ctx.ArcTo(new Point(cx + r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
        ctx.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: true);
    }
}
