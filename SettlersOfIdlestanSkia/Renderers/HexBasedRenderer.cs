using SkiaSharp;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Classe utilitaire pour gérer les transformations de caméra sur le canvas.
/// Automatise le Save/Restore du canvas.
/// </summary>
public class CameraTransformScope : IDisposable
{
    private readonly SKCanvas _canvas;
    private bool _disposed;

    public CameraTransformScope(SKCanvas canvas, SKSize canvasSize, float zoomLevel)
    {
        _canvas = canvas;
        _canvas.Save();
        
        // Applique la transformation de caméra
        _canvas.Translate(canvasSize.Width / 2, canvasSize.Height / 2);
        _canvas.Scale(zoomLevel, zoomLevel);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _canvas.Restore();
        _disposed = true;
    }
}

/// <summary>
/// Classe mère pour tous les renderers basés sur une grille hexagonale.
/// Centralise la gestion de la taille des hexagones, les conversions de coordonnées et les transformations de caméra.
/// </summary>
public abstract class HexBasedRenderer : IGameRenderer, IHexConverter
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
    /// Applique la transformation de caméra au canvas.
    /// Retourne un scope qui gère automatiquement le Save/Restore du canvas.
    /// </summary>
    protected CameraTransformScope ApplyCameraTransform(SKCanvas canvas, GameRenderContext context)
    {
        return new CameraTransformScope(canvas, CanvasSize, context.ZoomLevel);
    }

    /// <summary>
    /// Convertit des coordonnées hexagonales (Q, R) en coordonnées pixel (x, y).
    /// Utilisé pour tous les calculs de position dans la grille hexagonale.
    /// </summary>
    public (float x, float y) AxialToPixel(int q, int r)
    {
        float x = HexSize * (3f / 2 * q);
        float y = HexSize * (float)System.Math.Sqrt(3) / 2 * q + HexSize * (float)System.Math.Sqrt(3) * r;

        return (x, y);
    }

    /// <summary>
    /// Convertit des coordonnées pixel (x, y) en coordonnées hexagonales axiales (q, r).
    /// Utilise l'inverse de la transformation AxialToPixel.
    /// </summary>
    public (int q, int r) PixelToAxial(float x, float y)
    {
        // Applique l'offset d'origine inverse
        float q = (2f / 3 * x) / HexSize;
        float r = (-1f / 3 * x + (float)System.Math.Sqrt(3) / 3 * y) / HexSize;

        // Arrondit aux coordonnées hexagonales les plus proches
        return RoundAxialCoordinates(q, r);
    }

    /// <summary>
    /// Arrondit des coordonnées axiales floatantes aux coordonnées entières les plus proches.
    /// </summary>
    private (int q, int r) RoundAxialCoordinates(float q, float r)
    {
        float s = -q - r;
        
        float rq = (float)System.Math.Round(q);
        float rr = (float)System.Math.Round(r);
        float rs = (float)System.Math.Round(s);
        
        float qDiff = System.Math.Abs(rq - q);
        float rDiff = System.Math.Abs(rr - r);
        float sDiff = System.Math.Abs(rs - s);
        
        if (qDiff > rDiff && qDiff > sDiff)
        {
            rq = -rr - rs;
        }
        else if (rDiff > sDiff)
        {
            rr = -rq - rs;
        }
        
        return ((int)rq, (int)rr);
    }

    /// <summary>
    /// Vérifie si un point (x, y) en coordonnées pixel se trouve à l'intérieur d'un hexagone.
    /// Utilise l'algorithme "point in polygon" pour les hexagones réguliers.
    /// </summary>
    public bool IsPointInHexagon(float px, float py, float hexCenterX, float hexCenterY, float size = HexSize)
    {
        var points = GetHexagonPoints(hexCenterX, hexCenterY, size);
        return IsPointInPolygon(px, py, points);
    }

    /// <summary>
    /// Vérifie si un point se trouve à l'intérieur d'un polygone en utilisant l'algorithme ray casting.
    /// </summary>
    private bool IsPointInPolygon(float x, float y, SKPoint[] polygon)
    {
        int n = polygon.Length;
        bool inside = false;

        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = polygon[i].X, yi = polygon[i].Y;
            float xj = polygon[j].X, yj = polygon[j].Y;

            bool intersect = ((yi > y) != (yj > y)) && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
            if (intersect)
                inside = !inside;
        }

        return inside;
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
    /// Calcule le point milieu entre deux hexagones adjacents pour positionner une route sur l'edge.
    /// </summary>
    protected SKPoint EdgeToPixel(int q1, int r1, int q2, int r2)
    {
        var (x1, y1) = AxialToPixel(q1, r1);
        var (x2, y2) = AxialToPixel(q2, r2);

        return new SKPoint((x1 + x2) / 2, (y1 + y2) / 2);
    }

    /// <summary>
    /// Convertit un point écran en coordonnées hexagonales, en appliquant la même transformation que le rendu (origine, zoom, pan).
    /// </summary>
    public (int q, int r) ScreenToHex(SKPoint screenPoint, SKSize canvasSize, float zoomLevel, SKPoint cameraPos)
    {
        // Applique la transformation inverse du rendu
        // 1. Translate l'origine écran au centre du canvas
        float x = (screenPoint.X - canvasSize.Width / 2f) / zoomLevel + cameraPos.X;
        float y = (screenPoint.Y - canvasSize.Height / 2f) / zoomLevel + cameraPos.Y;
        // 2. Convertit en coordonnées hexagonales
        return PixelToAxial(x, y);
    }

    /// <summary>
    /// Libère les ressources du renderer.
    /// </summary>
    public abstract void Dispose();
}
