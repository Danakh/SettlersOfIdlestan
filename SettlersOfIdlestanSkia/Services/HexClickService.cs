using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers;

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
    private readonly HexBasedRenderer _renderer;

    /// <summary>
    /// Événement déclenché quand un hexagone est cliqué.
    /// </summary>
    public event EventHandler<HexClickedEventArgs>? HexClicked;

    public HexClickService(
        GameControllerService gameControllerService,
        HarvestService harvestService,
        InputHandlingService inputService,
        CameraService cameraService,
        HexBasedRenderer renderer)
    {
        _gameControllerService = gameControllerService ?? throw new ArgumentNullException(nameof(gameControllerService));
        _harvestService = harvestService ?? throw new ArgumentNullException(nameof(harvestService));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));

        // S'abonne aux événements de pointeur
        _inputService.PointerPressed += OnPointerPressed;
    }

    /// <summary>
    /// Gère le clic du pointeur sur le canvas.
    /// </summary>
    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_gameControllerService.CurrentGameState == null)
            return;

        try
        {
            var canvasSize = _cameraService.CanvasSize; // Ajoute une propriété publique CanvasSize dans CameraService si besoin
            var cameraPos = _cameraService.Position;
            var zoomLevel = _cameraService.ZoomLevel;
            (int q, int r) = _renderer.ScreenToHex(e.Position, canvasSize, zoomLevel, cameraPos);
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
