using SkiaSharp;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service de gestion de la caméra.
/// Responsable du positionnement et du zoom de la vue.
/// </summary>
public class CameraService
{
    private SKPoint _position = SKPoint.Empty;
    private float _zoomLevel = 1.0f;
    private SKSize _canvasSize;
    private bool _isDirty = true;

    public SKPoint Position => _position;
    public float ZoomLevel => _zoomLevel;

    /// <summary>
    /// Initialise la caméra avec les dimensions du canvas.
    /// </summary>
    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _isDirty = true;
    }

    /// <summary>
    /// Centre la caméra sur un point du monde.
    /// </summary>
    public void CenterOn(float worldX, float worldY)
    {
        _position = new SKPoint(
            worldX - _canvasSize.Width / 2 / _zoomLevel,
            worldY - _canvasSize.Height / 2 / _zoomLevel
        );
        _isDirty = true;
    }

    /// <summary>
    /// Définit le zoom et recentre si nécessaire.
    /// </summary>
    public void SetZoom(float zoom, bool keepCenteredOnScreen = true)
    {
        if (zoom < 0.1f || zoom > 10f)
            return;

        if (keepCenteredOnScreen)
        {
            // Maintient le centre de l'écran au même endroit
            var centerWorldX = _position.X + _canvasSize.Width / 2 / _zoomLevel;
            var centerWorldY = _position.Y + _canvasSize.Height / 2 / _zoomLevel;

            _zoomLevel = zoom;

            CenterOn(centerWorldX, centerWorldY);
        }
        else
        {
            _zoomLevel = zoom;
            _isDirty = true;
        }
    }

    /// <summary>
    /// Déplace la caméra d'un delta en pixels écran.
    /// </summary>
    public void Pan(float screenDeltaX, float screenDeltaY)
    {
        var worldDeltaX = screenDeltaX / _zoomLevel;
        var worldDeltaY = screenDeltaY / _zoomLevel;

        _position = new SKPoint(
            _position.X - worldDeltaX,
            _position.Y - worldDeltaY
        );
        _isDirty = true;
    }

    /// <summary>
    /// Convertit des coordonnées écran en coordonnées monde.
    /// </summary>
    public SKPoint ScreenToWorld(SKPoint screenCoord)
    {
        return new SKPoint(
            _position.X + screenCoord.X / _zoomLevel,
            _position.Y + screenCoord.Y / _zoomLevel
        );
    }

    /// <summary>
    /// Convertit des coordonnées monde en coordonnées écran.
    /// </summary>
    public SKPoint WorldToScreen(SKPoint worldCoord)
    {
        return new SKPoint(
            (worldCoord.X - _position.X) * _zoomLevel,
            (worldCoord.Y - _position.Y) * _zoomLevel
        );
    }

    /// <summary>
    /// Centre la caméra sur (0, 0) du monde (origin).
    /// </summary>
    public void CenterOnOrigin()
    {
        CenterOn(0, 0);
    }

    /// <summary>
    /// Indique si la caméra a été modifiée depuis la dernière lecture.
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set => _isDirty = value;
    }
}
