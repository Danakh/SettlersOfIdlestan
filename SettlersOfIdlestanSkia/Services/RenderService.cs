using SkiaSharp;
using System.Diagnostics;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service principal orchestrant le rendu de tous les éléments du jeu.
/// Responsable de la composition des différents renderers.
/// </summary>
public class RenderService : IDisposable
{
    private readonly List<IGameRenderer> _renderers = [];
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private float _totalTime;
    private bool _disposed;

    public RenderService()
    {
    }

    /// <summary>
    /// Récupère la liste des renderers enregistrés.
    /// </summary>
    public IReadOnlyList<IGameRenderer> Renderers => _renderers.AsReadOnly();

    /// <summary>
    /// Enregistre un renderer pour qu'il soit appelé à chaque frame.
    /// L'ordre d'enregistrement détermine l'ordre de rendu (back to front).
    /// </summary>
    public void RegisterRenderer(IGameRenderer renderer)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderService));
        
        _renderers.Add(renderer);
    }

    /// <summary>
    /// Initialise tous les renderers avec les dimensions du canvas.
    /// </summary>
    public void Initialize(SKSize canvasSize)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderService));

        foreach (var renderer in _renderers)
        {
            renderer.Initialize(canvasSize);
        }
    }

    /// <summary>
    /// Rend un frame en appelant tous les renderers dans l'ordre.
    /// </summary>
    public void RenderFrame(SKCanvas canvas, object gameState, CameraService? cameraService = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderService));

        if (canvas == null)
            throw new ArgumentNullException(nameof(canvas));

        if (gameState == null)
            throw new ArgumentNullException(nameof(gameState));

        // Calcul du deltaTime
        var elapsed = _frameTimer.Elapsed.TotalSeconds;
        _frameTimer.Restart();
        var deltaTime = (float)elapsed;
        _totalTime += deltaTime;

        var canvasSize = new SKSize(canvas.DeviceClipBounds.Width, canvas.DeviceClipBounds.Height);

        var context = new GameRenderContext
        {
            GameState = gameState,
            DeltaTime = deltaTime,
            CanvasSize = canvasSize,
            TotalTime = _totalTime,
            CameraPosition = cameraService?.Position ?? SKPoint.Empty,
            ZoomLevel = cameraService?.ZoomLevel ?? 1.0f
        };

        // Appelle chaque renderer dans l'ordre
        foreach (var renderer in _renderers)
        {
            renderer.Render(canvas, context);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var renderer in _renderers)
        {
            renderer.Dispose();
        }

        _renderers.Clear();
        _disposed = true;
    }
}
