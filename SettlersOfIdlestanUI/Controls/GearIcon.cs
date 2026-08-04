using Avalonia;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;

namespace SettlersOfIdlestanUI.Controls;

/// <summary>
/// Icone d'engrenage des parametres.
///
/// Il n'existe pas de SVG pour cette icone : l'ancien renderer la construisait
/// geometriquement. On reprend exactement la meme construction (8 dents, rayon de dent a
/// 1,3x le rayon) pour que l'icone reste identique apres migration.
/// </summary>
internal static class GearIcon
{
    private const int TeethCount = 8;

    public static AvaloniaPath Create(double size, IBrush brush)
    {
        double cx = size / 2, cy = size / 2;
        double radius = size / 2.5;
        double teethRadius = radius * 1.3;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (int i = 0; i < TeethCount; i++)
            {
                double a1 = Angle(i), a2 = Angle(i + 0.4), a3 = Angle(i + 0.6), a4 = Angle(i + 1);

                ctx.BeginFigure(Point(cx, cy, a1, radius), isFilled: true);
                ctx.LineTo(Point(cx, cy, a2, teethRadius));
                ctx.LineTo(Point(cx, cy, a3, teethRadius));
                ctx.LineTo(Point(cx, cy, a4, radius));
                ctx.EndFigure(isClosed: true);
            }

            // Moyeu central, evide par la regle de remplissage alternee.
            AddCircle(ctx, cx, cy, radius * 0.45);
            ctx.SetFillRule(FillRule.EvenOdd);
        }

        return new AvaloniaPath
        {
            Data = geometry,
            Fill = brush,
            Width = size,
            Height = size,
        };
    }

    private static double Angle(double index) => 360.0 / TeethCount * index * Math.PI / 180.0;

    private static Point Point(double cx, double cy, double angle, double radius) =>
        new(cx + Math.Cos(angle) * radius, cy + Math.Sin(angle) * radius);

    private static void AddCircle(StreamGeometryContext ctx, double cx, double cy, double r)
    {
        ctx.BeginFigure(new Point(cx - r, cy), isFilled: true);
        ctx.ArcTo(new Point(cx + r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
        ctx.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: true);
    }
}
