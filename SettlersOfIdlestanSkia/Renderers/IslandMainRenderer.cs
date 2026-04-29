using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer principal de l'île.
/// Il applique la transformation caméra et orchestre les renderers hexagonaux.
/// C'est aussi l'unique point de conversion écran -> Island.
/// </summary>
public class IslandMainRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly GameBoardRenderer _gameBoardRenderer;
    private readonly RoadRenderer _roadRenderer;
    private readonly CityRenderer _cityRenderer;
    private readonly IConstructionHoverProvider? _constructionHoverProvider;

    private readonly SKPaint _buildableVertexPaint = new()
    {
        Color = new SKColor(60, 160, 255, 120),
        Style = SKPaintStyle.Fill,
        IsAntialias = true
    };
    private readonly SKPaint _buildableEdgePaint = new()
    {
        Color = new SKColor(60, 160, 255, 120),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true
    };
    private readonly SKPaint _hoverVertexPaint = new()
    {
        Color = new SKColor(255, 235, 59, 220),
        Style = SKPaintStyle.Fill,
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

    public IslandMainRenderer(IConstructionHoverProvider? constructionHoverProvider = null)
    {
        _gameBoardRenderer = new GameBoardRenderer();
        _roadRenderer = new RoadRenderer();
        _cityRenderer = new CityRenderer();
        _constructionHoverProvider = constructionHoverProvider;
    }

    public void Initialize(SKSize canvasSize)
    {
        _gameBoardRenderer.Initialize(canvasSize);
        _roadRenderer.Initialize(canvasSize);
        _cityRenderer.Initialize(canvasSize);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        using (ApplyCameraTransform(canvas, context))
        {
            _gameBoardRenderer.Render(canvas, context);
            _roadRenderer.Render(canvas, context);
            _cityRenderer.Render(canvas, context);
            RenderConstructionHighlights(canvas);
        }
    }

    private void RenderConstructionHighlights(SKCanvas canvas)
    {
        if (_constructionHoverProvider == null)
            return;

        var state = _constructionHoverProvider.HoverState;

        foreach (var vertex in state.BuildableVertices)
        {
            var pt = VertexToIsland(vertex);
            canvas.DrawCircle(pt, 5f, _buildableVertexPaint);
        }

        foreach (var edge in state.BuildableEdges)
        {
            var center = EdgeToIslandPoint(edge);
            var vertices = edge.GetVertices();
            var v1 = VertexToIsland(vertices[0]);
            var v2 = VertexToIsland(vertices[1]);
            var dx = v2.X - v1.X;
            var dy = v2.Y - v1.Y;
            var length = MathF.Sqrt(dx * dx + dy * dy);
            if (length <= 0.001f)
                continue;

            var perpX = -dy / length;
            var perpY = dx / length;
            const float segment = 10f;
            canvas.DrawLine(
                center.X - perpX * segment / 2,
                center.Y - perpY * segment / 2,
                center.X + perpX * segment / 2,
                center.Y + perpY * segment / 2,
                _buildableEdgePaint);
        }

        if (state.HoveredVertex != null)
        {
            var pt = VertexToIsland(state.HoveredVertex);
            canvas.DrawCircle(pt, 7f, _hoverVertexPaint);
        }

        if (state.HoveredEdge != null)
        {
            var center = EdgeToIslandPoint(state.HoveredEdge);
            var vertices = state.HoveredEdge.GetVertices();
            var v1 = VertexToIsland(vertices[0]);
            var v2 = VertexToIsland(vertices[1]);
            var dx = v2.X - v1.X;
            var dy = v2.Y - v1.Y;
            var length = MathF.Sqrt(dx * dx + dy * dy);
            if (length > 0.001f)
            {
                var perpX = -dy / length;
                var perpY = dx / length;
                const float segment = 12f;
                canvas.DrawLine(
                    center.X - perpX * segment / 2,
                    center.Y - perpY * segment / 2,
                    center.X + perpX * segment / 2,
                    center.Y + perpY * segment / 2,
                    _hoverEdgePaint);
            }
        }
    }

    public SKPoint ScreenToIsland(SKPoint screenPoint, SKSize canvasSize, float zoomLevel, SKPoint cameraPos)
    {
        return new SKPoint(
            screenPoint.X / zoomLevel + cameraPos.X,
            screenPoint.Y / zoomLevel + cameraPos.Y);
    }

    public (int q, int r) ScreenToHex(SKPoint screenPoint, SKSize canvasSize, float zoomLevel, SKPoint cameraPos)
    {
        var islandPoint = ScreenToIsland(screenPoint, canvasSize, zoomLevel, cameraPos);
        var hex = IslandToHexCoord(islandPoint);
        return (hex.Q, hex.R);
    }

    public SKPoint VertexToIslandPoint(Vertex vertex) => VertexToIsland(vertex);

    public SKPoint EdgeToIslandPoint(Edge edge)
    {
        var (h1, h2) = edge.GetHexes();
        return EdgeToIsland(h1.Q, h1.R, h2.Q, h2.R);
    }

    public void Dispose()
    {
        _gameBoardRenderer.Dispose();
        _roadRenderer.Dispose();
        _cityRenderer.Dispose();
        _buildableVertexPaint.Dispose();
        _buildableEdgePaint.Dispose();
        _hoverVertexPaint.Dispose();
        _hoverEdgePaint.Dispose();
    }

    private CameraTransformScope ApplyCameraTransform(SKCanvas canvas, GameRenderContext context)
    {
        return new CameraTransformScope(canvas, context.CanvasSize, context.ZoomLevel);
    }

    private sealed class CameraTransformScope : IDisposable
    {
        private readonly SKCanvas _canvas;
        private bool _disposed;

        public CameraTransformScope(SKCanvas canvas, SKSize canvasSize, float zoomLevel)
        {
            _canvas = canvas;
            _canvas.Save();
            _canvas.Translate(canvasSize.Width / 2, canvasSize.Height / 2);
            _canvas.Scale(zoomLevel, zoomLevel);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _canvas.Restore();
            _disposed = true;
        }
    }
}
