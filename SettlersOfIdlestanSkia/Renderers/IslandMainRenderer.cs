using SkiaSharp;
using SettlersOfIdlestanSkia.Core;

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

    public IslandMainRenderer()
    {
        _gameBoardRenderer = new GameBoardRenderer();
        _roadRenderer = new RoadRenderer();
        _cityRenderer = new CityRenderer();
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

    public void Dispose()
    {
        _gameBoardRenderer.Dispose();
        _roadRenderer.Dispose();
        _cityRenderer.Dispose();
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
