using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer pour afficher les routes entre les villes.
/// </summary>
public class RoadRenderer : HexBasedRenderer, IGameRenderer
{
    private bool _disposed;

    private readonly TooltipRenderer _tooltipRenderer;

    private SKPaint? _roadPaint;

    private readonly SKPaint _buildableEdgePaint = new()
    {
        Color = new SKColor(60, 160, 255, 120),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true
    };
    private readonly SKPaint _hoverEdgePaint = new()
    {
        Color = new SKColor(255, 235, 59, 240),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 6,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true
    };

    // Couleurs pour les civilisations (à étendre selon le nombre de civs)
    private static readonly SKColor[] CivilizationColors = new[]
    {
        new SKColor(255, 0, 0),     // Rouge - Civ 0
        new SKColor(0, 0, 255),     // Bleu - Civ 1
        new SKColor(0, 200, 0),     // Vert - Civ 2
        new SKColor(255, 200, 0),   // Orange - Civ 3
    };

    public RoadRenderer(TooltipRenderer tooltipRenderer)
    {
        _tooltipRenderer = tooltipRenderer;
    }

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
                if (!islandState.VisibleIslandMaps.TryGetValue(islandState.PlayerCivilization.Index, out var visibleMap))
                    return;

                // Dessine les routes de chaque civilisation
                foreach (var civilization in islandState.Civilizations)
                {
                    DrawRoads(canvas, visibleMap, civilization.Roads, civilization.Index);
                }
            }
        }
    }

    internal void RenderConstructionHighlights(SKCanvas canvas, ConstructionHoverState state)
    {
        foreach (var edge in state.BuildableEdges)
        {
            DrawEdgeHighlight(canvas, edge, _buildableEdgePaint, 0.18f);
        }

        if (state.HoveredEdge != null)
        {
            DrawEdgeHighlight(canvas, state.HoveredEdge, _hoverEdgePaint, 0.14f);

            _tooltipRenderer.SetRoadConstructionTooltip(state.HoveredEdge);
        }
    }

    private void DrawEdgeHighlight(SKCanvas canvas, Edge edge, SKPaint paint, float trimFactor)
    {
        var vertices = edge.GetVertices();
        if (vertices.Length < 2)
            return;

        var v1 = VertexToIsland(vertices[0]);
        var v2 = VertexToIsland(vertices[1]);

        var start = new SKPoint(
            v1.X + (v2.X - v1.X) * trimFactor,
            v1.Y + (v2.Y - v1.Y) * trimFactor);
        var end = new SKPoint(
            v2.X - (v2.X - v1.X) * trimFactor,
            v2.Y - (v2.Y - v1.Y) * trimFactor);

        canvas.DrawLine(start, end, paint);
    }

    /// <summary>
    /// Dessine les routes d'une civilisation.
    /// </summary>
    private void DrawRoads(SKCanvas canvas, IslandMap visibleMap,
        List<Road> roads, int civilizationIndex)
    {
        if (_roadPaint == null || roads.Count == 0)
            return;

        // Sélectionne la couleur de la civilisation
        var color = CivilizationColors[civilizationIndex % CivilizationColors.Length];
        _roadPaint.Color = color;

        foreach (var road in roads)
        {
            if (!IsRoadVisible(road, visibleMap))
                continue;

            DrawRoadSegmentOnEdge(canvas, road.Position, _roadPaint);
        }
    }

    private static bool IsRoadVisible(Road road, IslandMap visibleMap)
    {
        var (h1, h2) = road.Position.GetHexes();
        return visibleMap.HasTile(h1) && visibleMap.HasTile(h2);
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
        _buildableEdgePaint.Dispose();
        _hoverEdgePaint.Dispose();

        _disposed = true;
    }
}
