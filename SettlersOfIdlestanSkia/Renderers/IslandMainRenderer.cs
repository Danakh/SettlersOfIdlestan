using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
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
    private readonly HarvestRenderer _harvestRenderer;
    private readonly HarvestParticleSystem _harvestParticleSystem;
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
    private readonly SKPaint _hoverCityPaint = new()
    {
        Color = new SKColor(255, 255, 255, 220),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 2,
        IsAntialias = true
    };
    private readonly SKPaint _selectedCityPaint = new()
    {
        Color = new SKColor(255, 215, 0, 230),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 3,
        IsAntialias = true
    };

    /// <summary>
    /// Dictionnaire de couleurs pour les ressources (pour les particules de récolte).
    /// </summary>
    internal static readonly Dictionary<Resource, SKColor> ResourceColors = new()
    {
        { Resource.Food, new SKColor(255, 192, 203) },       // Rose
        { Resource.Wood, new SKColor(139, 69, 19) },          // Marron
        { Resource.Brick, new SKColor(210, 105, 30) },        // Brique orange-marron
        { Resource.Stone, new SKColor(128, 128, 128) },         // Gris
        { Resource.Ore, new SKColor(69, 69, 69) },         // Gris foncé
        { Resource.Gold, new SKColor(255, 215, 0) },         // Or
        { Resource.Glass, new SKColor(173, 216, 230) },         // Bleu clair
        { Resource.Crystal, new SKColor(147, 112, 219) } // Violet
    };

    public IslandMainRenderer(IConstructionHoverProvider? constructionHoverProvider = null)
    {
        _gameBoardRenderer = new GameBoardRenderer();
        _roadRenderer = new RoadRenderer();
        _cityRenderer = new CityRenderer();
        _harvestParticleSystem = new HarvestParticleSystem();
        _harvestRenderer = new HarvestRenderer(_harvestParticleSystem);
        _constructionHoverProvider = constructionHoverProvider;
    }

    /// <summary>
    /// Retourne le système de particules de récolte pour abonnement aux événements.
    /// </summary>
    public HarvestParticleSystem GetHarvestParticleSystem() => _harvestParticleSystem;

    public void Initialize(SKSize canvasSize)
    {
        _gameBoardRenderer.Initialize(canvasSize);
        _roadRenderer.Initialize(canvasSize);
        _cityRenderer.Initialize(canvasSize);
        _harvestRenderer.Initialize(canvasSize);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        using (ApplyCameraTransform(canvas, context))
        {
            _gameBoardRenderer.Render(canvas, context);
            _roadRenderer.Render(canvas, context);
            _cityRenderer.Render(canvas, context);
            _harvestRenderer.Render(canvas, context);
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
            DrawEdgeHighlight(canvas, edge, _buildableEdgePaint, 0.18f);
        }

        if (state.HoveredVertex != null)
        {
            var pt = VertexToIsland(state.HoveredVertex);
            canvas.DrawCircle(pt, 7f, _hoverVertexPaint);
        }

        if (state.HoveredEdge != null)
        {
            DrawEdgeHighlight(canvas, state.HoveredEdge, _hoverEdgePaint, 0.14f);
        }

        if (state.HoveredCityVertex != null)
        {
            var pt = VertexToIsland(state.HoveredCityVertex);
            canvas.DrawCircle(pt, 9f, _hoverCityPaint);
        }

        if (state.SelectedCityVertex != null)
        {
            var pt = VertexToIsland(state.SelectedCityVertex);
            canvas.DrawCircle(pt, 12f, _selectedCityPaint);
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
        _harvestRenderer.Dispose();
        _buildableVertexPaint.Dispose();
        _buildableEdgePaint.Dispose();
        _hoverVertexPaint.Dispose();
        _hoverEdgePaint.Dispose();
        _hoverCityPaint.Dispose();
        _selectedCityPaint.Dispose();
    }

    private CameraTransformScope ApplyCameraTransform(SKCanvas canvas, GameRenderContext context)
    {
        return new CameraTransformScope(canvas, context.ZoomLevel, context.CameraPosition);
    }

    private sealed class CameraTransformScope : IDisposable
    {
        private readonly SKCanvas _canvas;
        private bool _disposed;

        public CameraTransformScope(SKCanvas canvas, float zoomLevel, SKPoint cameraPos)
        {
            _canvas = canvas;
            _canvas.Save();
            _canvas.Translate(-cameraPos.X * zoomLevel, -cameraPos.Y * zoomLevel);
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
