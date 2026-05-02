using SkiaSharp;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Orchestrateur centralisé côté "moteur" Skia:
/// - instancie services + renderers,
/// - gère l'initialisation caméra/fit sur le premier canvas,
/// - fournit Tick/Render pour un host (MAUI Desktop pour l'instant).
/// </summary>
public sealed class SkiaGameRuntime : IDisposable
{
    private readonly object _sync = new();

    private ResourceManager? _resourceManager;
    private InputHandlingService? _inputService;
    private RenderService? _renderService;
    private GameControllerService? _gameControllerService;
    private CameraService? _cameraService;
    private HarvestService? _harvestService;
    private ConstructionInteractionService? _constructionInteractionService;
    private ILocalizationService? _localizationService;

    private bool _isDisposed;
    private bool _isGameInitialized;
    private bool _isCanvasInitialized;

    private SKSize _lastCanvasSize;

    private readonly System.Diagnostics.Stopwatch _tickStopwatch = new();
    private readonly System.Diagnostics.Stopwatch _fpsStopwatch = new();
    private int _frameCount;

    private RuntimeDebugStats? _pendingDebugStats;

    public void Initialize()
    {
        lock (_sync)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SkiaGameRuntime));

            if (_isGameInitialized)
                return;

            _resourceManager = new ResourceManager();
            _inputService = new InputHandlingService();
            _renderService = new RenderService();
            _gameControllerService = new GameControllerService();
            _cameraService = new CameraService();
            _harvestService = new HarvestService(_gameControllerService);
            _localizationService = new LocalizationService();

            var gameState = _gameControllerService.InitializeNewGame();
            if (gameState == null)
                throw new InvalidOperationException("Impossible de créer le jeu.");

            // Enregistrement des renderers (back to front)
            IslandMainRenderer islandMainRenderer;
            _constructionInteractionService = new ConstructionInteractionService(
                _gameControllerService,
                _harvestService,
                _inputService,
                _cameraService,
                _gameControllerService.CityBuildingService);
            islandMainRenderer = new IslandMainRenderer(_constructionInteractionService);
            _constructionInteractionService.AttachRenderer(islandMainRenderer);
            _renderService.RegisterRenderer(islandMainRenderer);

            // Ajout du panneau latéral des bâtiments sélectionnés
            var selectedCityPanelRenderer = new SelectedCityPanelRenderer(_localizationService, _gameControllerService.CityBuildingService, _inputService);

            _renderService.RegisterRenderer(selectedCityPanelRenderer);

            // Crée le menu avant le renderer et le passe en paramètre
            var settingsMenu = new SettingsMenu(_gameControllerService.MainGameController, _inputService, _localizationService);
            _renderService.RegisterRenderer(new PlayerResourcesOverlayRenderer(_inputService, settingsMenu, _resourceManager));
            _renderService.RegisterRenderer(new DebugOverlayRenderer(_inputService, _cameraService, islandMainRenderer, _localizationService));

            _isGameInitialized = true;

            _tickStopwatch.Restart();
            _fpsStopwatch.Restart();
            _frameCount = 0;
        }
    }

    public void EnsureCanvasInitialized(SKSize canvasSize)
    {
        lock (_sync)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SkiaGameRuntime));

            if (!_isGameInitialized)
                throw new InvalidOperationException($"{nameof(SkiaGameRuntime)} n'est pas initialisé.");

            if (_isCanvasInitialized && canvasSize == _lastCanvasSize)
                return;

            if (_cameraService == null || _renderService == null)
                throw new InvalidOperationException("Camera/RenderService non initialisé.");

            _cameraService.Initialize(canvasSize);

            var gameState = _gameControllerService?.CurrentGameState;
            var hexCoords = gameState?.CurrentIslandState?.Map?.Tiles?.Keys ??
                             Enumerable.Empty<HexCoord>();
            _cameraService.FitMapToView(hexCoords);

            _renderService.Initialize(canvasSize);

            _lastCanvasSize = canvasSize;
            _isCanvasInitialized = true;
        }
    }

    public void Tick()
    {
        lock (_sync)
        {
            if (_isDisposed || !_isGameInitialized)
                return;

            if (_gameControllerService == null)
                return;

            // DeltaTime pour l'avancement du "clock" du jeu.
            var elapsed = _tickStopwatch.Elapsed.TotalSeconds;
            _tickStopwatch.Restart();

            // Clamp: évite des "sauts" si le host freeze.
            var deltaTime = (float)Math.Clamp(elapsed, 0f, 0.1f);
            _gameControllerService.Update(deltaTime);

            _frameCount++;

            var fpsElapsed = _fpsStopwatch.Elapsed.TotalSeconds;
            if (fpsElapsed >= 0.5)
            {
                var fps = (float)(_frameCount / fpsElapsed);
                _fpsStopwatch.Restart();
                _frameCount = 0;

                var cameraPos = _cameraService?.Position ?? SKPoint.Empty;
                var (cityCount, roadCount) = GetCityRoadCounts();

                _pendingDebugStats = new RuntimeDebugStats(
                    fps: fps,
                    cameraX: cameraPos.X,
                    cameraY: cameraPos.Y,
                    cityCount: cityCount,
                    roadCount: roadCount
                );
            }
        }
    }

    private (int cityCount, int roadCount) GetCityRoadCounts()
    {
        var gameState = _gameControllerService?.CurrentGameState;
        if (gameState?.CurrentIslandState == null)
            return (0, 0);

        var civ = gameState.CurrentIslandState.Civilizations.FirstOrDefault();
        if (civ == null)
            return (0, 0);

        return (civ.Cities.Count, civ.Roads.Count);
    }

    public void Render(SKCanvas canvas)
    {
        lock (_sync)
        {
            if (_isDisposed || !_isGameInitialized || _renderService == null || _cameraService == null)
                return;

            var gameState = _gameControllerService?.CurrentGameState;
            if (gameState == null)
                return;

            _renderService.RenderFrame(canvas, gameState, _cameraService);
        }
    }

    public void HandlePointerPressed(float x, float y, int pointerId = 0)
    {
        lock (_sync)
        {
            _inputService?.HandlePointerPressed(x, y, pointerId);
        }
    }

    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        lock (_sync)
        {
            _inputService?.HandlePointerMoved(x, y, pointerId);
        }
    }

    public void HandlePointerReleased(float x, float y, int pointerId = 0)
    {
        lock (_sync)
        {
            _inputService?.HandlePointerReleased(x, y, pointerId);
        }
    }

    public bool TryGetDebugStats(out RuntimeDebugStats stats)
    {
        lock (_sync)
        {
            if (_pendingDebugStats is { } pending)
            {
                stats = pending;
                _pendingDebugStats = null;
                return true;
            }

            stats = default;
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_isDisposed)
                return;

            _constructionInteractionService?.Cleanup();
            _renderService?.Dispose();
            _resourceManager?.Dispose();

            _constructionInteractionService = null;
            _renderService = null;
            _resourceManager = null;
            _inputService = null;
            _cameraService = null;
            _gameControllerService = null;
            _harvestService = null;

            _isDisposed = true;
        }
    }
}

public readonly record struct RuntimeDebugStats(
    float fps,
    float cameraX,
    float cameraY,
    int cityCount,
    int roadCount);

