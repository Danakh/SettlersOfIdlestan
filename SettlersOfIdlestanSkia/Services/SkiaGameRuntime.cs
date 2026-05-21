using SkiaSharp;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using Microsoft.Extensions.DependencyInjection;

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
    private IFileSystemService? _fileSystemService;

    private bool _isDisposed;
    private bool _isGameInitialized;
    private bool _isCanvasInitialized;

    private SKSize _lastCanvasSize;
    private bool _isPointerDown;
    private bool _isPanning;
    private long _activePanPointerId;
    private SKPoint _lastPanPoint;
    private SKPoint _panStartPoint;
    private const float PanStartThresholdSquared = 16f;
    private const float ZoomStep = 1.12f;

    private readonly System.Diagnostics.Stopwatch _tickStopwatch = new();
    private readonly System.Diagnostics.Stopwatch _fpsStopwatch = new();
    private int _frameCount;

    private RuntimeDebugStats? _pendingDebugStats;

    private double _autoSaveTimer = 0;
    private const double AutoSaveInterval = 5.0; // 5 seconds

    public void Initialize(IFileSystemService fileSystemService)
    {
        lock (_sync)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(SkiaGameRuntime));

            if (_isGameInitialized)
                return;

            _fileSystemService = fileSystemService;
        }

        _resourceManager = new ResourceManager();
        _inputService = new InputHandlingService();
        _renderService = new RenderService();
        _cameraService = new CameraService();
        _localizationService = new LocalizationService();


        _gameControllerService = new GameControllerService();
        // Tentative de chargement auto-save
        var autoJson = fileSystemService.LoadAuto().Result;
        if (!string.IsNullOrEmpty(autoJson))
        {
            try
            {
                _gameControllerService.ImportMainState(autoJson);
            }
            catch
            {
                _gameControllerService.InitializeNewGame();
            }
        }
        else
        {
            _gameControllerService.InitializeNewGame();
        }

        _harvestService = new HarvestService(_gameControllerService);

        // Enregistrement des renderers (back to front)
        TooltipRenderer tooltipRenderer = new TooltipRenderer(_localizationService, _gameControllerService);

        IslandMainRenderer islandMainRenderer;
        _constructionInteractionService = new ConstructionInteractionService(
            _gameControllerService,
            _harvestService,
            _inputService,
            _cameraService,
            _gameControllerService.CityBuildingService);
        islandMainRenderer = new IslandMainRenderer(_constructionInteractionService, tooltipRenderer);
        _constructionInteractionService.AttachRenderer(islandMainRenderer);
        _renderService.RegisterRenderer(islandMainRenderer);

        // Connecte les événements de récolte au système de particules
        ConnectHarvestEventsToParticles(islandMainRenderer);

        // Ajout du panneau latéral des bâtiments sélectionnés
        var selectedCityPanelRenderer = new SelectedCityPanelRenderer(_gameControllerService.CityBuildingService, _localizationService, _inputService);

        _renderService.RegisterRenderer(selectedCityPanelRenderer);

        // Crée le menu avant le renderer et le passe en paramètre
        var aboutRenderer = new AboutRenderer(_inputService, _localizationService);
        var settingsMenu = new SettingsMenu(_gameControllerService.MainGameController, _inputService, _localizationService, aboutRenderer, fileSystemService);
        _renderService.RegisterRenderer(new PlayerResourcesOverlayRenderer(_inputService, _localizationService, settingsMenu, _resourceManager));
        _renderService.RegisterRenderer(new DebugOverlayRenderer(_inputService, _cameraService, islandMainRenderer, _localizationService));
        _renderService.RegisterRenderer(aboutRenderer);

        _renderService.RegisterRenderer(tooltipRenderer);

        _isGameInitialized = true;

        _tickStopwatch.Restart();
        _fpsStopwatch.Restart();
        _frameCount = 0;
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

            _autoSaveTimer += deltaTime;
            if (_autoSaveTimer >= AutoSaveInterval)
            {
                _autoSaveTimer = 0;
                if (_fileSystemService != null && _gameControllerService.MainGameController.CurrentMainState != null)
                {
                    var json = _gameControllerService.MainGameController.ExportMainState();
                    _fileSystemService.SaveAuto(json);
                }
            }

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
            _isPointerDown = true;
            _isPanning = false;
            _activePanPointerId = pointerId;
            _panStartPoint = new SKPoint(x, y);
            _lastPanPoint = _panStartPoint;
            _inputService?.HandlePointerPressed(x, y, pointerId);
        }
    }

    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        lock (_sync)
        {
            if (_isPointerDown && pointerId == _activePanPointerId && _cameraService != null)
            {
                var point = new SKPoint(x, y);
                var startDx = point.X - _panStartPoint.X;
                var startDy = point.Y - _panStartPoint.Y;
                if (!_isPanning && startDx * startDx + startDy * startDy >= PanStartThresholdSquared)
                {
                    _isPanning = true;
                }

                if (_isPanning)
                {
                    _cameraService.Pan(point.X - _lastPanPoint.X, point.Y - _lastPanPoint.Y);
                }

                _lastPanPoint = point;
            }

            _inputService?.HandlePointerMoved(x, y, pointerId);
        }
    }

    public void HandlePointerReleased(float x, float y, int pointerId = 0)
    {
        lock (_sync)
        {
            var wasPanning = _isPanning && pointerId == _activePanPointerId;
            _isPointerDown = false;
            _isPanning = false;

            if (!wasPanning)
            {
                _inputService?.HandlePointerReleased(x, y, pointerId);
            }
        }
    }

    public void HandleZoom(float wheelDelta, float x, float y)
    {
        lock (_sync)
        {
            if (_cameraService == null || wheelDelta == 0)
                return;

            var zoomFactor = wheelDelta > 0 ? ZoomStep : 1f / ZoomStep;
            _cameraService.ZoomAt(_cameraService.ZoomLevel * zoomFactor, new SKPoint(x, y));
            _inputService?.HandleZoom(wheelDelta, x, y);
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

    private void ConnectHarvestEventsToParticles(IslandMainRenderer islandMainRenderer)
    {
        var particleSystem = islandMainRenderer.GetHarvestParticleSystem();
        
        _harvestService!.OnHarvestCompleted += (sender, args) =>
        {
            var gameState = _gameControllerService?.CurrentGameState;
            if (gameState?.CurrentIslandState == null)
                return;

            // Obtient le centre de l'hex source
            var (hexX, hexY) = islandMainRenderer.AxialToIsland(args.HexCoord.Q, args.HexCoord.R);
            var hexCenter = new SKPoint(hexX, hexY);

            // Utilise la position de la ville fournie par l'événement
            SKPoint cityCenter = islandMainRenderer.VertexToIslandPoint(args.CityPosition);

            // Détermine la couleur basée sur le type de ressource récolté
            var resourceColors = IslandMainRenderer.ResourceColors;

            var particleColor = resourceColors.TryGetValue(args.Resource, out var color) 
                ? color 
                : SKColors.Gold;

            // Émet une particule
            particleSystem.EmitParticle(hexCenter, cityCenter, particleColor);
        };
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

