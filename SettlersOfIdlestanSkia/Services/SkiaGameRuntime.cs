using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Services;

public sealed class SkiaGameRuntime : IDisposable
{
    private ResourceManager? _resourceManager;
    private InputHandlingService? _inputService;
    private RenderService? _renderService;
    private GameControllerService? _gameControllerService;
    private CameraService? _cameraService;
    private HarvestService? _harvestService;
    private ConstructionInteractionService? _constructionInteractionService;
    private ILocalizationService? _localizationService;
    private IFileSystemService? _fileSystemService;
    private IslandMainRenderer? _islandMainRenderer;
    private OverlayRenderer? _overlayRenderer;
    private bool _prestigeTransitionPending;

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
    private const double AutoSaveInterval = 5.0;

    public void Initialize(IFileSystemService fileSystemService)
    {
        var autoJson = fileSystemService.LoadAuto().GetAwaiter().GetResult();
        InitializeCore(fileSystemService, autoJson);
    }

    public async Task InitializeAsync(IFileSystemService fileSystemService)
    {
        var autoJson = await fileSystemService.LoadAuto();
        InitializeCore(fileSystemService, autoJson);
    }

    private void InitializeCore(IFileSystemService fileSystemService, string? autoJson)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SkiaGameRuntime));

        if (_isGameInitialized)
            return;

        _fileSystemService = fileSystemService;

        _resourceManager = new ResourceManager();
        _inputService = new InputHandlingService();
        _renderService = new RenderService();
        _cameraService = new CameraService();
        _localizationService = new LocalizationService();

        _gameControllerService = new GameControllerService();
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

        TooltipRenderer tooltipRenderer = new TooltipRenderer(_localizationService, _gameControllerService);

        _constructionInteractionService = new ConstructionInteractionService(
            _gameControllerService,
            _harvestService,
            _inputService,
            _cameraService,
            _gameControllerService.CityBuildingService);
        var islandMainRenderer = new IslandMainRenderer(_constructionInteractionService, tooltipRenderer, _gameControllerService.MainGameController.HarvestController);
        _islandMainRenderer = islandMainRenderer;
        _constructionInteractionService.AttachRenderer(islandMainRenderer);
        _renderService.RegisterRenderer(islandMainRenderer);

        ConnectHarvestEventsToParticles(islandMainRenderer);

        var selectedCityPanelRenderer = new SelectedCityPanelRenderer(_gameControllerService.CityBuildingService, _localizationService, _inputService);

        var aboutRenderer = new AboutRenderer(_inputService, _localizationService);
        var settingsMenu = new SettingsMenu(_gameControllerService.MainGameController, _inputService, _localizationService, aboutRenderer, fileSystemService);
        var playerResourcesOverlayRenderer = new PlayerResourcesOverlayRenderer(_localizationService, _resourceManager);
        var tradeRenderer = new TradeRenderer(_gameControllerService, _localizationService, tooltipRenderer);
        var prestigeRenderer = new PrestigeRenderer(_gameControllerService, _localizationService, RequestPrestige);
        var prestigeMapRenderer = new PrestigeMapRenderer(_gameControllerService, _localizationService, tooltipRenderer);
        var prestigeHistoryRenderer = new PrestigeHistoryRenderer(_gameControllerService, _localizationService);
        var timeControlRenderer = new TimeControlRenderer(_gameControllerService, _inputService);
        var researchRenderer = new ResearchRenderer(_gameControllerService, _localizationService, _inputService);
        _overlayRenderer = new OverlayRenderer(
            _inputService,
            _gameControllerService,
            _localizationService,
            playerResourcesOverlayRenderer,
            settingsMenu,
            selectedCityPanelRenderer,
            tradeRenderer,
            prestigeRenderer,
            prestigeMapRenderer,
            prestigeHistoryRenderer,
            timeControlRenderer,
            researchRenderer);
        _renderService.RegisterRenderer(_overlayRenderer);
        _constructionInteractionService.ShouldSuppressHover = () => _overlayRenderer.IsAnyOverlayOpen;
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
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SkiaGameRuntime));

        if (!_isGameInitialized)
            throw new InvalidOperationException($"{nameof(SkiaGameRuntime)} n'est pas initialisé.");

        if (_isCanvasInitialized && canvasSize == _lastCanvasSize)
            return;

        if (_cameraService == null || _renderService == null)
            throw new InvalidOperationException("Camera/RenderService non initialisé.");

        _cameraService.Initialize(canvasSize);

        var hexCoords = _gameControllerService?.CurrentGameState?.CurrentIslandState?.Map?.Tiles?.Keys
                        ?? Enumerable.Empty<HexCoord>();
        _cameraService.FitMapToView(hexCoords);

        _renderService.Initialize(canvasSize);

        _lastCanvasSize = canvasSize;
        _isCanvasInitialized = true;
    }

    public void Tick()
    {
        if (_isDisposed || !_isGameInitialized || _gameControllerService == null)
            return;

        var elapsed = _tickStopwatch.Elapsed.TotalSeconds;
        _tickStopwatch.Restart();

        var deltaTime = (float)Math.Clamp(elapsed, 0f, 0.1f);
        _gameControllerService.Update(deltaTime);

        if (_prestigeTransitionPending && _islandMainRenderer?.IsBlackFadeComplete == true)
            CompletePrestigeTransition();

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
                roadCount: roadCount);
        }
    }

    private (int cityCount, int roadCount) GetCityRoadCounts()
    {
        var civ = _gameControllerService?.CurrentGameState?.CurrentIslandState?.Civilizations.FirstOrDefault();
        return civ == null ? (0, 0) : (civ.Cities.Count, civ.Roads.Count);
    }

    public void Render(SKCanvas canvas)
    {
        if (_isDisposed || !_isGameInitialized || _renderService == null || _cameraService == null)
            return;

        var gameState = _gameControllerService?.CurrentGameState;
        if (gameState == null)
            return;

        if (_islandMainRenderer != null && _overlayRenderer != null)
            _islandMainRenderer.IsVisible = _overlayRenderer.IsIslandTabActive;

        _renderService.RenderFrame(canvas, gameState, _cameraService);
    }

    public void HandlePointerPressed(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        _isPointerDown = true;
        _isPanning = false;
        _activePanPointerId = pointerId;
        _panStartPoint = new SKPoint(x, y);
        _lastPanPoint = _panStartPoint;
        _inputService?.HandlePointerPressed(x, y, pointerId, button);
    }

    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        if (_isPointerDown && pointerId == _activePanPointerId && _cameraService != null)
        {
            var point = new SKPoint(x, y);
            var startDx = point.X - _panStartPoint.X;
            var startDy = point.Y - _panStartPoint.Y;
            if (!_isPanning && startDx * startDx + startDy * startDy >= PanStartThresholdSquared)
                _isPanning = true;

            if (_isPanning)
                _cameraService.Pan(point.X - _lastPanPoint.X, point.Y - _lastPanPoint.Y);

            _lastPanPoint = point;
        }

        _inputService?.HandlePointerMoved(x, y, pointerId);
    }

    public void HandlePointerReleased(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        var wasPanning = _isPanning && pointerId == _activePanPointerId;
        _isPointerDown = false;
        _isPanning = false;

        if (!wasPanning)
            _inputService?.HandlePointerReleased(x, y, pointerId, button);
    }

    public void HandleZoom(float wheelDelta, float x, float y)
    {
        if (_cameraService == null || wheelDelta == 0)
            return;

        var zoomFactor = wheelDelta > 0 ? ZoomStep : 1f / ZoomStep;
        _cameraService.ZoomAt(_cameraService.ZoomLevel * zoomFactor, new SKPoint(x, y));
        _inputService?.HandleZoom(wheelDelta, x, y);
    }

    public bool TryGetDebugStats(out RuntimeDebugStats stats)
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

    private void RequestPrestige()
    {
        if (_prestigeTransitionPending || _islandMainRenderer == null || _overlayRenderer == null)
            return;

        _overlayRenderer.Hide();
        _islandMainRenderer.BeginBlackFade(0.5f);
        _prestigeTransitionPending = true;
    }

    private void CompletePrestigeTransition()
    {
        if (_gameControllerService == null || _cameraService == null)
            return;

        _gameControllerService.PerformPrestige();
        _constructionInteractionService?.ClearHover();

        var hexCoords = _gameControllerService.CurrentIslandState?.Map?.Tiles?.Keys ?? Enumerable.Empty<HexCoord>();
        _cameraService.FitMapToView(hexCoords);

        _islandMainRenderer?.EndBlackFade();
        _overlayRenderer?.Show();
        _gameControllerService.CurrentGameState?.Clock?.Pause();
        _overlayRenderer?.SwitchToPrestigeTab();
        _prestigeTransitionPending = false;
    }

    private void ConnectHarvestEventsToParticles(IslandMainRenderer islandMainRenderer)
    {
        var particleSystem = islandMainRenderer.GetHarvestParticleSystem();

        _harvestService!.OnHarvestCompleted += (sender, args) =>
        {
            if (_prestigeTransitionPending)
                return;

            if (_gameControllerService?.CurrentGameState?.CurrentIslandState == null)
                return;

            var (hexX, hexY) = islandMainRenderer.AxialToIsland(args.HexCoord.Q, args.HexCoord.R);
            var hexCenter = new SKPoint(hexX, hexY);
            SKPoint cityCenter = islandMainRenderer.VertexToIslandPoint(args.CityPosition);

            var resourceColors = IslandMainRenderer.ResourceColors;
            var particleColor = resourceColors.TryGetValue(args.Resource, out var color) ? color : SKColors.Gold;

            particleSystem.EmitParticle(hexCenter, cityCenter, particleColor);
        };

        _harvestService!.OnMarketResourceGenerated += (sender, args) =>
        {
            if (_prestigeTransitionPending)
                return;

            if (_gameControllerService?.CurrentGameState?.CurrentIslandState == null)
                return;

            SKPoint cityCenter = islandMainRenderer.VertexToIslandPoint(args.CityPosition);
            SKPoint above = new SKPoint(cityCenter.X, cityCenter.Y - 30f);

            var resourceColors = IslandMainRenderer.ResourceColors;
            var particleColor = resourceColors.TryGetValue(args.Resource, out var color) ? color : SKColors.Gold;

            particleSystem.EmitParticle(cityCenter, above, particleColor, 0.8f);
        };
    }

    public void Dispose()
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

public readonly record struct RuntimeDebugStats(
    float fps,
    float cameraX,
    float cameraY,
    int cityCount,
    int roadCount);
