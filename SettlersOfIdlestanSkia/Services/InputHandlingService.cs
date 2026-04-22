using SkiaSharp;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service gérant les entrées utilisateur (clics, mouvements souris, touches clavier).
/// Traduit les événements bruts en actions de jeu.
/// </summary>
public class InputHandlingService
{
    /// <summary>
    /// Événement déclenché lors d'une pression du pointeur (souris/tactile).
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerPressed;

    /// <summary>
    /// Événement déclenché lors du mouvement du pointeur.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerMoved;

    /// <summary>
    /// Événement déclenché lors du relâchement du pointeur.
    /// </summary>
    public event EventHandler<PointerEventArgs>? PointerReleased;

    /// <summary>
    /// Événement déclenché lors du zoom (molette/pinch).
    /// </summary>
    public event EventHandler<ZoomEventArgs>? ZoomChanged;

    public SKPoint LastPointerPosition { get; private set; } = SKPoint.Empty;

    /// <summary>
    /// Traite un événement de pointeur pressé.
    /// </summary>
    public void HandlePointerPressed(float x, float y, int pointerId = 0)
    {
        PointerPressed?.Invoke(this, new PointerEventArgs 
        { 
            Position = new SKPoint(x, y), 
            PointerId = pointerId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Traite un événement de pointeur bougé.
    /// </summary>
    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        LastPointerPosition = new SKPoint(x, y);
        PointerMoved?.Invoke(this, new PointerEventArgs
        {
            Position = LastPointerPosition,
            PointerId = pointerId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Traite un événement de pointeur relâché.
    /// </summary>
    public void HandlePointerReleased(float x, float y, int pointerId = 0)
    {
        PointerReleased?.Invoke(this, new PointerEventArgs 
        { 
            Position = new SKPoint(x, y), 
            PointerId = pointerId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Traite un événement de zoom.
    /// </summary>
    public void HandleZoom(float zoomDelta, float centerX, float centerY)
    {
        ZoomChanged?.Invoke(this, new ZoomEventArgs 
        { 
            ZoomDelta = zoomDelta,
            Center = new SKPoint(centerX, centerY),
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// Arguments d'événement pour les événements de pointeur.
/// </summary>
public class PointerEventArgs : EventArgs
{
    public required SKPoint Position { get; init; }
    public int PointerId { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Arguments d'événement pour les événements de zoom.
/// </summary>
public class ZoomEventArgs : EventArgs
{
    public required float ZoomDelta { get; init; }
    public required SKPoint Center { get; init; }
    public required DateTime Timestamp { get; init; }
}
