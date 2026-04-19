using SkiaSharp;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Classe mère pour tous les renderers basés sur une grille hexagonale.
/// Centralise la gestion de la taille des hexagones et les conversions de coordonnées.
/// </summary>
public abstract class HexBasedRenderer : IGameRenderer
{
    /// <summary>
    /// Taille des hexagones en pixels.
    /// </summary>
    protected const float HexSize = 40f;

    protected SKSize CanvasSize { get; set; }
    protected bool Disposed { get; set; }

    /// <summary>
    /// Initialise le renderer avec les dimensions du canvas.
    /// </summary>
    public abstract void Initialize(SKSize canvasSize);

    /// <summary>
    /// Rend un frame.
    /// </summary>
    public abstract void Render(SKCanvas canvas, GameRenderContext context);

    /// <summary>
    /// Convertit des coordonnées axiales (q, r) en coordonnées pixel (x, y).
    /// Utilisé pour tous les calculs de position dans la grille hexagonale.
    /// </summary>
    protected (float x, float y) AxialToPixel(int q, int r)
    {
        float x = HexSize * (3f / 2 * q);
        float y = HexSize * (float)System.Math.Sqrt(3) / 2 * q + HexSize * (float)System.Math.Sqrt(3) * r;

        return (x, y);
    }

    /// <summary>
    /// Convertit un Vertex (défini par 3 hexagones) en coordonnées pixel.
    /// Position du vertex = moyenne des centres des 3 hexagones.
    /// </summary>
    protected SKPoint VertexToPixel(SettlersOfIdlestan.Model.HexGrid.Vertex vertex)
    {
        var (x1, y1) = AxialToPixel(vertex.Hex1.Q, vertex.Hex1.R);
        var (x2, y2) = AxialToPixel(vertex.Hex2.Q, vertex.Hex2.R);
        var (x3, y3) = AxialToPixel(vertex.Hex3.Q, vertex.Hex3.R);

        return new SKPoint((x1 + x2 + x3) / 3, (y1 + y2 + y3) / 3);
    }

    /// <summary>
    /// Crée une liste de points pour un hexagone régulier centré à (centerX, centerY).
    /// </summary>
    protected SKPoint[] GetHexagonPoints(float centerX, float centerY, float size = HexSize)
    {
        var points = new SKPoint[6];

        for (int i = 0; i < 6; i++)
        {
            float angle = (float)System.Math.PI / 3 * i;
            points[i] = new SKPoint(
                centerX + size * (float)System.Math.Cos(angle),
                centerY + size * (float)System.Math.Sin(angle)
            );
        }

        return points;
    }

    /// <summary>
    /// Convertit une liste de points en chemin SKPath.
    /// </summary>
    protected SKPath PointsToPath(SKPoint[] points)
    {
        var path = new SKPath();
        if (points.Length > 0)
        {
            path.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++)
                path.LineTo(points[i]);
            path.Close();
        }
        return path;
    }

    /// <summary>
    /// Libère les ressources du renderer.
    /// </summary>
    public abstract void Dispose();
}
