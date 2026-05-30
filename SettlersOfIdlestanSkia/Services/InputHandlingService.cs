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
    public void HandlePointerPressed(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        PointerPressed?.Invoke(this, new PointerEventArgs 
        { 
            Position = new SKPoint(x, y), 
            PointerId = pointerId,
            Button = button,
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
    public void HandlePointerReleased(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        PointerReleased?.Invoke(this, new PointerEventArgs 
        { 
            Position = new SKPoint(x, y), 
            PointerId = pointerId,
            Button = button,
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

    /// <summary>
    /// Événement déclenché lors de l'appui sur une touche clavier.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyPressed;

    /// <summary>
    /// Événement déclenché lors du relâchement d'une touche clavier.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyReleased;

    /// <summary>
    /// Traite un événement clavier (appui).
    /// </summary>
    public void HandleKeyPressed(string key)
    {
        KeyPressed?.Invoke(this, new KeyEventArgs { Key = key, Timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Traite un événement clavier (relâchement).
    /// </summary>
    public void HandleKeyReleased(string key)
    {
        KeyReleased?.Invoke(this, new KeyEventArgs { Key = key, Timestamp = DateTime.UtcNow });
    }
}

/// <summary>
/// Arguments d'événement pour les événements de pointeur.
/// </summary>
public class PointerEventArgs : EventArgs
{
    public required SKPoint Position { get; init; }
    public int PointerId { get; init; }
    public PointerButton Button { get; init; } = PointerButton.Left;
    public required DateTime Timestamp { get; init; }
}

public enum PointerButton
{
    Left,
    Middle,
    Right,
    Unknown
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

/// <summary>
/// Arguments d'événement pour les événements clavier.
/// </summary>
public class KeyEventArgs : EventArgs
{
    public required string Key { get; init; }
    public required DateTime Timestamp { get; init; }
}
