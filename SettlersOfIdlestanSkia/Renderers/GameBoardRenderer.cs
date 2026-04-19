using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer de base pour le plateau de jeu hexagonal.
/// Responsable du rendu des hexagones, du terrain et des ressources.
/// </summary>
public class GameBoardRenderer : IGameRenderer
{
    private const float HexSize = 40f;

    private SKPaint? _hexBorderPaint;
    private SKPaint? _hexFillPaint;
    private SKPaint? _textPaint;
    private SKSize _canvasSize;
    private bool _disposed;

    // Dictionnaire de couleurs pour les types de terrain
    private static readonly Dictionary<TerrainType, SKColor> TerrainColors = new()
    {
        { TerrainType.Mountain, new SKColor(139, 69, 19) },      // Marron
        { TerrainType.Forest, new SKColor(34, 139, 34) },        // Vert foncé
        { TerrainType.Pasture, new SKColor(144, 238, 144) },     // Vert clair
        { TerrainType.Hill, new SKColor(210, 180, 140) },        // Tan
        { TerrainType.Field, new SKColor(255, 215, 0) },         // Or
        { TerrainType.Desert, new SKColor(238, 214, 175) },      // Sable
        { TerrainType.Water, new SKColor(30, 144, 255) },        // Bleu
    };

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
            TextSize = 12,
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

        // Sauvegarde le state du canvas
        canvas.Save();

        try
        {
            // Applique la transformation de caméra :
            // 1. Translate pour mettre le centre du canvas au (0,0) du monde
            // 2. Applique le zoom
            // 3. La position de la caméra est déjà un offset complet (inclut le centrage)
            canvas.Translate(_canvasSize.Width / 2, _canvasSize.Height / 2);
            canvas.Scale(context.ZoomLevel, context.ZoomLevel);

            // Récupère le MainGameState et l'IslandState
            if (context.GameState is MainGameState mainGameState)
            {
                var islandState = mainGameState.CurrentIslandState;
                if (islandState != null)
                {
                    DrawIslandMap(canvas, islandState.Map);
                }
                else
                {
                    // Si pas d'îlot, on dessine une grille de test
                    DrawTestHexagonGrid(canvas);
                }
            }
            else
            {
                DrawTestHexagonGrid(canvas);
            }
        }
        finally
        {
            // Restaure le state du canvas
            canvas.Restore();
        }
    }

    /// <summary>
    /// Dessine la carte d'une île basée sur son IslandMap.
    /// </summary>
    private void DrawIslandMap(SKCanvas canvas, IslandMap map)
    {
        foreach (var (coord, tile) in map.Tiles)
        {
            var (x, y) = AxialToPixel(coord.Q, coord.R);
            DrawHexagonTile(canvas, x, y, HexSize, tile);
        }
    }

    /// <summary>
    /// Dessine un hexagone représentant une tuile avec sa couleur de terrain.
    /// </summary>
    private void DrawHexagonTile(SKCanvas canvas, float centerX, float centerY, float size, HexTile tile)
    {
        var points = GetHexagonPoints(centerX, centerY, size);

        if (_hexFillPaint != null)
        {
            // Utilise la couleur du type de terrain
            _hexFillPaint.Color = TerrainColors.TryGetValue(tile.TerrainType, out var color) 
                ? color 
                : new SKColor(200, 200, 200);
            
            canvas.DrawPath(PointsToPath(points), _hexFillPaint);
        }

        if (_hexBorderPaint != null)
            canvas.DrawPath(PointsToPath(points), _hexBorderPaint);

        // Affiche le numéro de production si applicable
        if (_textPaint != null && tile.ProductionNumber.HasValue)
        {
            _textPaint.Color = SKColors.White;
            canvas.DrawText(tile.ProductionNumber.ToString(), centerX, centerY, _textPaint);
        }
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
        {
            _textPaint.Color = SKColors.Black;
            canvas.DrawText($"({centerX:F0},{centerY:F0})", centerX, centerY, _textPaint);
        }
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

        return (x, y);
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
