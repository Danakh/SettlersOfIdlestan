using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer pour afficher les routes entre les villes.
/// </summary>
public class RoadRenderer : HexBasedRenderer, IGameRenderer
{
    private SKPaint? _roadPaint;
    private bool _disposed;

    // Couleurs pour les civilisations (à étendre selon le nombre de civs)
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(255, 0, 0),     // Rouge - Civ 0
        new SKColor(0, 0, 255),     // Bleu - Civ 1
        new SKColor(0, 200, 0),     // Vert - Civ 2
        new SKColor(255, 200, 0),   // Orange - Civ 3
    };

    public void Initialize(SKSize canvasSize)
    {
        _roadPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null || _roadPaint == null)
            return;

        if (context.GameState is MainGameState mainGameState)
        {
            var islandState = mainGameState.CurrentIslandState;
            if (islandState != null)
            {
                // Dessine les routes de chaque civilisation
                foreach (var civilization in islandState.Civilizations)
                {
                    DrawRoads(canvas, islandState.Map, civilization.Roads, civilization.Index);
                }
            }
        }
    }

    /// <summary>
    /// Dessine les routes d'une civilisation.
    /// </summary>
    private void DrawRoads(SKCanvas canvas, SettlersOfIdlestan.Model.IslandMap.IslandMap map, 
        List<SettlersOfIdlestan.Model.Road.Road> roads, int civilizationIndex)
    {
        if (_roadPaint == null || roads.Count == 0)
            return;

        // Sélectionne la couleur de la civilisation
        var color = CivilizationColors[civilizationIndex % CivilizationColors.Length];
        _roadPaint.Color = color;

        foreach (var road in roads)
        {
            DrawRoadSegmentOnEdge(canvas, road.Position, _roadPaint);
        }
    }

    private void DrawRoadSegmentOnEdge(SKCanvas canvas, SettlersOfIdlestan.Model.HexGrid.Edge edge, SKPaint paint)
    {
        var vertices = edge.GetVertices();
        if (vertices.Length < 2)
            return;

        var v1 = VertexToIsland(vertices[0]);
        var v2 = VertexToIsland(vertices[1]);

        // Segment aligné sur l'edge, légèrement raccourci pour laisser voir les villes.
        const float trimFactor = 0.18f;
        var start = new SKPoint(
            v1.X + (v2.X - v1.X) * trimFactor,
            v1.Y + (v2.Y - v1.Y) * trimFactor);
        var end = new SKPoint(
            v2.X - (v2.X - v1.X) * trimFactor,
            v2.Y - (v2.Y - v1.Y) * trimFactor);

        canvas.DrawLine(start, end, paint);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _roadPaint?.Dispose();
        _disposed = true;
    }
}
