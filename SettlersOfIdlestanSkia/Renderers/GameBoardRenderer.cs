using SkiaSharp;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer de base pour le plateau de jeu hexagonal.
/// Responsable du rendu des hexagones, du terrain et des ressources.
/// </summary>
public class GameBoardRenderer : IGameRenderer
{
    private const float HexSize = 40f;
    private const float Padding = 20f;

    private SKPaint? _hexBorderPaint;
    private SKPaint? _hexFillPaint;
    private SKPaint? _textPaint;
    private SKSize _canvasSize;
    private bool _disposed;

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;

        _hexBorderPaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _hexFillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null)
            return;

        // Clear the canvas with a light background
        canvas.DrawColor(new SKColor(238, 242, 245));

        // TODO: Récupérer la carte depuis gameState et rendre les hexagones
        // Pour maintenant, on dessine juste une grille de test
        DrawTestHexagonGrid(canvas);
    }

    /// <summary>
    /// Dessine une grille de test pour valider le système de rendu.
    /// À remplacer par le vrai rendu du plateau de jeu.
    /// </summary>
    private void DrawTestHexagonGrid(SKCanvas canvas)
    {
        for (int q = -3; q <= 3; q++)
        {
            for (int r = -3; r <= 3; r++)
            {
                var (x, y) = AxialToPixel(q, r);
                DrawHexagon(canvas, x, y, HexSize);
            }
        }
    }

    /// <summary>
    /// Dessine un hexagone centré à (centerX, centerY).
    /// </summary>
    private void DrawHexagon(SKCanvas canvas, float centerX, float centerY, float size)
    {
        var points = GetHexagonPoints(centerX, centerY, size);

        if (_hexFillPaint != null)
        {
            _hexFillPaint.Color = new SKColor(200, 220, 240);
            canvas.DrawPath(PointsToPath(points), _hexFillPaint);
        }

        if (_hexBorderPaint != null)
            canvas.DrawPath(PointsToPath(points), _hexBorderPaint);

        // Affiche les coordonnées
        if (_textPaint != null)
            canvas.DrawText($"({centerX:F0},{centerY:F0})", centerX, centerY, _textPaint);
    }

    /// <summary>
    /// Convertit des points en chemin SKPath.
    /// </summary>
    private SKPath PointsToPath(SKPoint[] points)
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
    /// Convertit des coordonnées axiales (q, r) en coordonnées pixel (x, y).
    /// </summary>
    private (float x, float y) AxialToPixel(int q, int r)
    {
        float x = HexSize * (3f / 2 * q);
        float y = HexSize * (float)System.Math.Sqrt(3) / 2 * q + HexSize * (float)System.Math.Sqrt(3) * r;

        return (x + Padding + _canvasSize.Width / 2, y + Padding + _canvasSize.Height / 2);
    }

    /// <summary>
    /// Génère les 6 points d'un hexagone régulier.
    /// </summary>
    private SKPoint[] GetHexagonPoints(float centerX, float centerY, float size)
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _hexBorderPaint?.Dispose();
        _hexFillPaint?.Dispose();
        _textPaint?.Dispose();

        _disposed = true;
    }
}
