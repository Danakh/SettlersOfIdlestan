using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Contexte de rendu contenant tous les éléments nécessaires pour rendre un frame.
/// </summary>
public class GameRenderContext
{
    /// <summary>
    /// L'état actuel du jeu.
    /// </summary>
    public required object GameState { get; init; }

    /// <summary>
    /// Temps écoulé depuis le dernier frame en secondes.
    /// </summary>
    public required float DeltaTime { get; init; }

    /// <summary>
    /// Les dimensions du canvas.
    /// </summary>
    public required SKSize CanvasSize { get; init; }

    /// <summary>
    /// La position de la caméra/vue (pour le zoom et le pan).
    /// </summary>
    public SKPoint CameraPosition { get; set; }

    /// <summary>
    /// Le niveau de zoom (1.0 = 100%).
    /// </summary>
    public float ZoomLevel { get; set; } = 1.0f;

    /// <summary>
    /// Temps total écoulé depuis le démarrage.
    /// </summary>
    public float TotalTime { get; set; }
}
