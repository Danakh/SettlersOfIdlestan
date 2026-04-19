using SkiaSharp;

namespace SettlersOfIdlestanSkia.Core;

/// <summary>
/// Interface de base pour les renderers du jeu.
/// Chaque renderer est responsable d'un aspect du rendu (plateau, UI, animations, etc.)
/// </summary>
public interface IGameRenderer
{
    /// <summary>
    /// Initialise le renderer avec le canvas et ses dimensions.
    /// Appelé une seule fois au démarrage.
    /// </summary>
    void Initialize(SKSize canvasSize);

    /// <summary>
    /// Rend un frame du jeu.
    /// </summary>
    /// <param name="canvas">Le canvas SkiaSharp sur lequel dessiner</param>
    /// <param name="gameState">L'état actuel du jeu</param>
    /// <param name="deltaTime">Temps écoulé depuis le dernier frame en secondes</param>
    void Render(SKCanvas canvas, GameRenderContext context);

    /// <summary>
    /// Libère les ressources du renderer.
    /// </summary>
    void Dispose();
}
