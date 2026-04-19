using SkiaSharp;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Template pour créer un nouveau renderer.
/// Copiez ce fichier et adaptez pour vos besoins.
/// </summary>
public class TemplateRenderer : IGameRenderer
{
    private SKPaint? _paint;
    private SKSize _canvasSize;
    private bool _disposed;

    /// <summary>
    /// Appelé une seule fois lors de l'initialisation.
    /// Créez/initialisez vos ressources ici.
    /// </summary>
    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;

        // Créer les ressources (paints, fonts, etc.)
        _paint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
    }

    /// <summary>
    /// Appelé à chaque frame (~60 FPS).
    /// Dessinez les éléments sur le canvas ici.
    /// </summary>
    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        // Récupérez l'état du jeu (s'il y a un type spécifique)
        // var gameState = context.GameState as MyGameStateType;
        // if (gameState == null)
        //     return;

        // Utilisez context pour:
        // - context.DeltaTime : temps écoulé depuis le dernier frame
        // - context.TotalTime : temps total depuis le démarrage
        // - context.CanvasSize : dimensions du canvas
        // - context.CameraPosition : position caméra (zoom/pan)
        // - context.ZoomLevel : niveau de zoom

        // Example: Dessiner un rectangle
        if (_paint != null)
        {
            canvas.DrawRect(new SKRect(10, 10, 100, 100), _paint);
        }
    }

    /// <summary>
    /// Appelé lors de la destruction.
    /// Libérez les ressources (Dispose les SKPaint, SKFont, etc.)
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _paint?.Dispose();
        _disposed = true;
    }
}
