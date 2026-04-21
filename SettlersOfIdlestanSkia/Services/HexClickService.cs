using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Service gérant la détection des clics sur les hexagones.
/// Traduit les coordonnées écran en coordonnées hexagonales et déclenche les actions appropriées.
/// </summary>
public class HexClickService
{
    private readonly GameControllerService _gameControllerService;
    private readonly HarvestService _harvestService;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private IHexConverter? _hexConverter;

    /// <summary>
    /// Événement déclenché quand un hexagone est cliqué.
    /// </summary>
    public event EventHandler<HexClickedEventArgs>? HexClicked;

    public HexClickService(
        GameControllerService gameControllerService,
        HarvestService harvestService,
        InputHandlingService inputService,
        CameraService cameraService)
    {
        _gameControllerService = gameControllerService ?? throw new ArgumentNullException(nameof(gameControllerService));
        _harvestService = harvestService ?? throw new ArgumentNullException(nameof(harvestService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));

        // S'abonne aux événements de pointeur
        _inputService.PointerPressed += OnPointerPressed;
    }

    /// <summary>
    /// Initialise le service avec un convertisseur hexagonal.
    /// </summary>
    public void Initialize(IHexConverter hexConverter)
    {
        _hexConverter = hexConverter ?? throw new ArgumentNullException(nameof(hexConverter));
    }

    /// <summary>
    /// Gère le clic du pointeur sur le canvas.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_hexConverter == null || _gameControllerService.CurrentGameState == null)
            return;

        try
        {
            // Convertit les coordonnées écran en coordonnées monde en appliquant l'inverse de la transformation de caméra
            var worldCoords = ScreenToWorldCoordinates(e.Position);

            // Convertit les coordonnées monde en coordonnées hexagonales
            var (q, r) = _hexConverter.PixelToAxial(worldCoords.X, worldCoords.Y);
            var hexCoord = new HexCoord(q, r);

            // Déclenche l'événement
            OnHexClicked(hexCoord);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erreur lors du traitement du clic: {ex.Message}");
        }
    }

    /// <summary>
    /// Convertit les coordonnées écran en coordonnées monde en appliquant l'inverse de la transformation de caméra.
    /// </summary>
    private SKPoint ScreenToWorldCoordinates(SKPoint screenPoint)
    {
        var cameraPos = _cameraService.Position;
        var zoomLevel = _cameraService.ZoomLevel;
        
        // Inverse la transformation de caméra
        // La caméra translate puis scale
        // Donc pour inverser: unscale puis untranslate
        float worldX = (screenPoint.X / zoomLevel) - (cameraPos.X / zoomLevel);
        float worldY = (screenPoint.Y / zoomLevel) - (cameraPos.Y / zoomLevel);

        return new SKPoint(worldX, worldY);
    }

    /// <summary>
    /// Gère le clic sur un hexagone - déclenche la récolte.
    /// </summary>
    private void OnHexClicked(HexCoord hexCoord)
    {
        // Déclenche l'événement pour les observateurs
        HexClicked?.Invoke(this, new HexClickedEventArgs { HexCoord = hexCoord });

        // Essaie une récolte manuelle
        bool harvestSucceeded = _harvestService.TryManualHarvest(hexCoord);

        System.Diagnostics.Debug.WriteLine(
            $"Clic sur hex ({hexCoord.Q}, {hexCoord.R}) - Récolte: {(harvestSucceeded ? "Succès" : "Échoué")}");
    }

    /// <summary>
    /// Se désabonne des événements.
    /// </summary>
    public void Cleanup()
    {
        _inputService.PointerPressed -= OnPointerPressed;
    }
}

/// <summary>
/// Arguments d'événement pour les clics sur hexagones.
/// </summary>
public class HexClickedEventArgs : EventArgs
{
    public required HexCoord HexCoord { get; init; }
}
