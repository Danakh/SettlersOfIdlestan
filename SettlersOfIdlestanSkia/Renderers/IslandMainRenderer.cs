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
    private readonly IConstructionHoverProvider _constructionHoverProvider;
    private readonly TooltipRenderer _tooltipRenderer;


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

    public IslandMainRenderer(IConstructionHoverProvider constructionHoverProvider, TooltipRenderer tooltipRenderer)
    {
        _gameBoardRenderer = new GameBoardRenderer();
        _roadRenderer = new RoadRenderer(tooltipRenderer);
        _cityRenderer = new CityRenderer(tooltipRenderer);
        _harvestParticleSystem = new HarvestParticleSystem();
        _harvestRenderer = new HarvestRenderer(_harvestParticleSystem);
        _constructionHoverProvider = constructionHoverProvider;
        _tooltipRenderer = tooltipRenderer;
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
        string? potentialTooltip = null;
        SKPoint tooltipPosition = SKPoint.Empty;
        var state = _constructionHoverProvider!.HoverState;

        // TODO mettre dans le using
        _tooltipRenderer.SetIslandRenderContext(this, context);

        using (ApplyCameraTransform(canvas, context))
        {
            _gameBoardRenderer.Render(canvas, context);
            _roadRenderer.Render(canvas, context);
            _cityRenderer.Render(canvas, context);
            _harvestRenderer.Render(canvas, context);

            _roadRenderer.RenderConstructionHighlights(canvas, state);
            _cityRenderer.RenderConstructionHighlights(canvas, state);
        }
    }

    public SKPoint ScreenToIsland(SKPoint screenPoint, SKSize canvasSize, float zoomLevel, SKPoint cameraPos)
    {
        return new SKPoint(
            screenPoint.X / zoomLevel + cameraPos.X,
            screenPoint.Y / zoomLevel + cameraPos.Y);
    }

    public SKPoint IslandToScreen(SKPoint islandPoint, float zoomLevel, SKPoint cameraPos)
    {
        return new SKPoint(
            (islandPoint.X - cameraPos.X) * zoomLevel,
            (islandPoint.Y - cameraPos.Y) * zoomLevel);
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
        _tooltipRenderer.Dispose();
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
