using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Renderers.Island;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Renderers.Overlay.Popup;
using SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;

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
    private LocalizationService? _localizationService;
    private IFileSystemService? _fileSystemService;
    private IslandMainRenderer? _islandMainRenderer;
    private OverlayRenderer? _overlayRenderer;
    private bool _prestigeTransitionPending;
    private bool _allowDebugMode;
    private IntroAnimationRenderer? _introRenderer;
    private bool _wasIntroActive;
    private WonderSelectionService? _wonderSelectionService;
    private WonderService? _wonderService;
    private TutorialRenderer? _tutorialRenderer;
    private TutorialService? _tutorialService;
    private MilitaryInteractionService? _militaryInteractionService;
    private PlayerResourcesOverlayRenderer? _playerResourcesOverlayRenderer;
    private CorruptSavePopupRenderer? _corruptSavePopup;
    private bool _corruptSavePending;
    private string? _corruptSaveJson;
    private GameOverPopupRenderer? _gameOverPopup;
    private bool _gameOverPending;

    public event Action? QuitRequested;

    private bool _isDisposed;
    private bool _isGameInitialized;
    private bool _isCanvasInitialized;

    private SKSize _lastCanvasSize;
    private bool _isPointerDown;
    private bool _isPanning;
    private bool _isPanSuppressedAtStart;
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

    private Func<int> _currentLayer => () => _gameControllerService?.CurrentGameState?.CurrentWorldState?.CurrentViewedLayer ?? 0;

    public void Initialize(IFileSystemService fileSystemService, bool allowDebugMode = false)
    {
        var autoJson = fileSystemService.LoadAuto().GetAwaiter().GetResult();
        InitializeCore(fileSystemService, autoJson, allowDebugMode);
    }

    public async Task InitializeAsync(IFileSystemService fileSystemService, bool allowDebugMode = false)
    {
        var autoJson = await fileSystemService.LoadAuto();
        InitializeCore(fileSystemService, autoJson, allowDebugMode);
    }

    private void InitializeCore(IFileSystemService fileSystemService, string? autoJson, bool allowDebugMode)
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(SkiaGameRuntime));

        if (_isGameInitialized)
            return;

        _fileSystemService = fileSystemService;
        _allowDebugMode = allowDebugMode;

        _resourceManager      = new ResourceManager();
        _inputService         = new InputHandlingService();
        _renderService        = new RenderService();
        _cameraService        = new CameraService();
        _localizationService  = new LocalizationService();
        _gameControllerService = new GameControllerService();

        bool isNewGame = false;

        try
        {
            if (!string.IsNullOrEmpty(autoJson))
            {
                _gameControllerService.ImportMainState(autoJson);
                isNewGame = false;
            }
            else
            {
                _gameControllerService.InitializeNewGame();
                isNewGame = true;
            }

            SetupRenderers(isNewGame, allowDebugMode);
        }
        catch
        {
            _corruptSaveJson    = autoJson;
            _corruptSavePending = true;

            // Nettoyer l'état partiellement initialisé
            _constructionInteractionService?.Cleanup();
            _constructionInteractionService = null;
            _militaryInteractionService?.Cleanup();
            _militaryInteractionService = null;
            _renderService.Dispose();
            _renderService = new RenderService();
            _inputService  = new InputHandlingService();

            // Réinitialiser avec un jeu vierge puis reconstruire les renderers
            _gameControllerService.InitializeNewGame();
            SetupRenderers(false, allowDebugMode);
        }

        if (_corruptSavePending)
        {
            _corruptSavePopup = new CorruptSavePopupRenderer(
                _localizationService!,
                _fileSystemService!,
                _corruptSaveJson!,
                onStartFresh: () => { _corruptSavePending = false; },
                onQuit:       () => { QuitRequested?.Invoke(); });
            _corruptSavePopup.Open();
        }

        _isGameInitialized = true;

        _tickStopwatch.Restart();
        _fpsStopwatch.Restart();
        _frameCount = 0;
    }

    private void SetupRenderers(bool isNewGame, bool allowDebugMode)
    {
        // Synchronise la langue depuis les settings sauvegardés
        var savedLanguage = _gameControllerService!.CurrentGameState?.Settings?.Language;
        if (savedLanguage.HasValue)
            _localizationService!.SetLanguage(savedLanguage.Value);

        _harvestService = new HarvestService(_gameControllerService);

        var tooltipRenderer = new TooltipRenderer(_localizationService!, _gameControllerService, _resourceManager!);

        _constructionInteractionService = new ConstructionInteractionService(
            _gameControllerService,
            _harvestService,
            _inputService!,
            _cameraService!,
            _gameControllerService.CityBuildingService!);
        var islandMainRenderer = new IslandMainRenderer(_constructionInteractionService, tooltipRenderer, _gameControllerService.MainGameController.HarvestController, _resourceManager!, _gameControllerService.MainGameController.MilitaryController, _currentLayer);
        _islandMainRenderer = islandMainRenderer;
        _constructionInteractionService.AttachRenderer(islandMainRenderer);
        _renderService!.RegisterRenderer(islandMainRenderer);
        islandMainRenderer.SuppressCities = () => _introRenderer?.IsActive == true;

        _militaryInteractionService = new MilitaryInteractionService(
            _gameControllerService,
            _gameControllerService.MainGameController.MilitaryController,
            _inputService!,
            _cameraService!);
        _militaryInteractionService.AttachRenderer(islandMainRenderer);
        islandMainRenderer.ConnectMilitaryInteractionService(_militaryInteractionService);

        _wonderSelectionService = new WonderSelectionService();
        _wonderSelectionService.ConnectWonderController(_gameControllerService.MainGameController.WonderController);
        _wonderSelectionService.Entered += OnWonderSelectionEntered;
        _wonderSelectionService.WonderPlacementConfirmed += OnWonderPlacementConfirmed;
        _wonderSelectionService.Cancelled += OnWonderSelectionCancelled;

        var wonderSelectionRenderer = new WonderPlacementRenderer(
            _wonderSelectionService, _inputService!, _cameraService!, _localizationService!);
        _renderService.RegisterRenderer(wonderSelectionRenderer);

        _introRenderer = new IntroAnimationRenderer(_resourceManager!);
        _renderService.RegisterRenderer(_introRenderer);

        islandMainRenderer.ConnectHarvestEvents(_harvestService!, _gameControllerService!, () => _prestigeTransitionPending, () => _overlayRenderer?.IsIslandTabActive ?? true, () => _gameControllerService.MainGameController.CurrentMainState?.Settings.ShowHarvestParticles ?? true);
        islandMainRenderer.ConnectMilitaryEvents(_gameControllerService.MainGameController.MilitaryController, _gameControllerService!, () => _prestigeTransitionPending, () => _overlayRenderer?.IsIslandTabActive ?? true);

        var selectedCityPanelRenderer = new SelectedCityPanelRenderer(_gameControllerService.CityBuildingService!, _localizationService!, _inputService!, _resourceManager!);
        _wonderService = new WonderService();
        _constructionInteractionService.AttachWonderService(_wonderService);
        islandMainRenderer.ConnectWonderService(_wonderService);
        var selectedWonderPanelRenderer = new SelectedWonderPanelRenderer(_wonderService, _inputService!, _localizationService!, _resourceManager!);

        var aboutRenderer        = new AboutRenderer(_inputService!, _localizationService!);
        var settingsPopupRenderer = new SettingsPopupRenderer(_gameControllerService.MainGameController, _localizationService!);
        DebugPanelRenderer? debugPanelRenderer = null;
        if (allowDebugMode)
            debugPanelRenderer = new DebugPanelRenderer(_inputService!, _localizationService!);
        var settingsMenu = new SettingsMenu(_gameControllerService.MainGameController, _inputService!, _localizationService!, aboutRenderer, settingsPopupRenderer, _fileSystemService!, _gameControllerService.CityBuildingService!, allowDebugMode, debugPanelRenderer, StartNewGameIntro);

        _playerResourcesOverlayRenderer = new PlayerResourcesOverlayRenderer(_localizationService!, _resourceManager!);
        var playerResourcesOverlayRenderer = _playerResourcesOverlayRenderer;
        playerResourcesOverlayRenderer.ConnectLowStock(null, _gameControllerService.PlayerCivilization!);

        var tradeRenderer        = new TradeRenderer(_gameControllerService, _localizationService!, tooltipRenderer, _resourceManager!);
        var prestigeRenderer     = new PrestigeRenderer(_gameControllerService, _localizationService!, RequestPrestige, tooltipRenderer);
        var prestigeMapRenderer  = new PrestigeMapRenderer(_gameControllerService, _localizationService!, tooltipRenderer);
        var prestigeHistoryRenderer = new PrestigeHistoryRenderer(_gameControllerService, _localizationService!);
        var timeControlRenderer  = new TimeControlRenderer(_gameControllerService, _inputService!, _localizationService!);
        var researchRenderer     = new ResearchRenderer(_gameControllerService, _localizationService!, _inputService!);
        var eventLogRenderer     = new EventLogRenderer(_gameControllerService, _localizationService!);
        var automationRenderer   = new AutomationRenderer(_gameControllerService, _localizationService!);

        _overlayRenderer = new OverlayRenderer(
            _inputService!,
            _gameControllerService,
            _localizationService!,
            playerResourcesOverlayRenderer,
            settingsMenu,
            settingsPopupRenderer,
            selectedCityPanelRenderer,
            selectedWonderPanelRenderer,
            tradeRenderer,
            prestigeRenderer,
            prestigeMapRenderer,
            prestigeHistoryRenderer,
            timeControlRenderer,
            researchRenderer,
            eventLogRenderer,
            automationRenderer,
            tooltipRenderer);
        _overlayRenderer.ConnectWonderService(_wonderSelectionService);
        _renderService.RegisterRenderer(_overlayRenderer);
        _constructionInteractionService.ShouldSuppressHover = pos =>
            (_overlayRenderer?.IsPointBlockedByUI(pos) ?? false)
            || (_wonderSelectionService?.IsActive == true)
            || (_militaryInteractionService?.ShouldSuppressConstruction == true);

        if (allowDebugMode)
        {
            _renderService.RegisterRenderer(new DebugOverlayRenderer(_inputService!, _cameraService!, islandMainRenderer, _localizationService!));
            _renderService.RegisterRenderer(new AutoplayerDebugRenderer(_gameControllerService, _inputService!));
            _renderService.RegisterRenderer(debugPanelRenderer!);
        }
        _renderService.RegisterRenderer(aboutRenderer);
        _tutorialRenderer = new TutorialRenderer(_localizationService!, _inputService!);
        _renderService.RegisterRenderer(_tutorialRenderer);
        _renderService.RegisterRenderer(tooltipRenderer);

        _tutorialService = new TutorialService(_tutorialRenderer);
        if (_gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState tutorialState)
        {
            if (isNewGame)
                _tutorialService.InitializeForNewGame(tutorialState);
            else
                _tutorialService.InitializeForLoadedGame(tutorialState);
        }

        if (isNewGame && _gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState introState)
            StartNewGameIntro(introState);

        _gameOverPopup = new GameOverPopupRenderer(_localizationService!, HandleGameOverRestart);

        var militaryController = _gameControllerService.MainGameController.MilitaryController;
        var monsterController  = _gameControllerService.MainGameController.MonsterFeatureController;
        militaryController.CityDestroyed          += OnCityDestroyedCheckGameOver;
        monsterController.CityDestroyedByMonster  += OnCityDestroyedCheckGameOver;
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

        float prevCenterX = _isCanvasInitialized
            ? _cameraService.Position.X + _lastCanvasSize.Width / 2 / _cameraService.ZoomLevel
            : 0f;
        float prevCenterY = _isCanvasInitialized
            ? _cameraService.Position.Y + _lastCanvasSize.Height / 2 / _cameraService.ZoomLevel
            : 0f;

        _cameraService.Initialize(canvasSize);

        if (!_isCanvasInitialized)
            CenterCameraOnStartingCity();
        else
            _cameraService.CenterOn(prevCenterX, prevCenterY);

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

        bool introActive = _introRenderer?.IsActive == true;
        if (introActive)
            _gameControllerService.CurrentGameState?.Clock?.Pause();
        else if (_wasIntroActive)
            _gameControllerService.CurrentGameState?.Clock?.Resume();
        _wasIntroActive = introActive;

        if (_prestigeTransitionPending && _islandMainRenderer?.IsBlackFadeComplete == true)
            CompletePrestigeTransition();

        if (_gameOverPending)
        {
            _gameOverPending = false;
            _gameControllerService.CurrentGameState?.Clock?.Pause();
            _gameOverPopup?.Open();
        }

        if (_gameControllerService.CurrentGameState is { } tutorialState)
            _tutorialService?.Update(tutorialState);

        _frameCount++;

        _autoSaveTimer += deltaTime;
        if (_autoSaveTimer >= AutoSaveInterval)
        {
            _autoSaveTimer = 0;
            if (!_corruptSavePending && _fileSystemService != null && _gameControllerService.MainGameController.CurrentMainState != null)
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
        var civ = _gameControllerService?.CurrentGameState?.CurrentWorldState?.Civilizations.FirstOrDefault();
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

        _renderService.RenderFrame(canvas, gameState!, _cameraService);

        _corruptSavePopup?.Render(canvas, _lastCanvasSize);
        _gameOverPopup?.Render(canvas, _lastCanvasSize);
    }

    public void HandlePointerPressed(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        if (_corruptSavePopup?.IsOpen == true) { _corruptSavePopup.HandlePointerPressed(new SKPoint(x, y), button); return; }
        if (_gameOverPopup?.IsOpen == true) { _gameOverPopup.HandlePointerPressed(new SKPoint(x, y), button); return; }
        if (_introRenderer?.IsActive == true) return;
        _isPointerDown = true;
        _isPanning = false;
        _isPanSuppressedAtStart = _overlayRenderer?.IsPointBlockedByUI(new SKPoint(x, y)) ?? false;
        _activePanPointerId = pointerId;
        _panStartPoint = new SKPoint(x, y);
        _lastPanPoint = _panStartPoint;
        _inputService?.HandlePointerPressed(x, y, pointerId, button);
        // Supprime le pan si on presse sur une cité alliée avec soldats
        if (_militaryInteractionService?.IsPotentialDragFromCity == true)
            _isPanSuppressedAtStart = true;
    }

    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        if (_corruptSavePopup?.IsOpen == true) return;
        if (_gameOverPopup?.IsOpen == true) return;
        if (_introRenderer?.IsActive == true) return;
        if (_isPointerDown && !_isPanSuppressedAtStart && (_overlayRenderer?.IsIslandTabActive ?? true) && pointerId == _activePanPointerId && _cameraService != null)
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
        if (_corruptSavePopup?.IsOpen == true) return;
        if (_gameOverPopup?.IsOpen == true) return;
        if (_introRenderer?.IsActive == true) return;
        var wasPanning = _isPanning && pointerId == _activePanPointerId;
        _isPointerDown = false;
        _isPanning = false;

        if (!wasPanning)
            _inputService?.HandlePointerReleased(x, y, pointerId, button);
    }

    public void HandleZoom(float wheelDelta, float x, float y)
    {
        if (_introRenderer?.IsActive == true) return;
        if (_cameraService == null || wheelDelta == 0)
            return;

        bool overUI = _overlayRenderer?.IsPointBlockedByUI(new SKPoint(x, y)) ?? false;
        if (!overUI && (_overlayRenderer?.IsIslandTabActive ?? true))
        {
            var zoomFactor = wheelDelta > 0 ? ZoomStep : 1f / ZoomStep;
            _cameraService.ZoomAt(_cameraService.ZoomLevel * zoomFactor, new SKPoint(x, y));
        }
        _inputService?.HandleZoom(wheelDelta, x, y);
    }

    public void HandleKeyReleased(string key) => _inputService?.HandleKeyReleased(key);

    public void HandleKeyPressed(string key)
    {
        _inputService?.HandleKeyPressed(key);

        if (key == "C" && _allowDebugMode)
            DebugAddResources();
    }

    private void DebugAddResources()
    {
        var mainState = _gameControllerService?.MainGameController?.CurrentMainState;
        if (mainState?.CurrentWorldState?.Civilizations.Count > 0)
        {
            var civ = mainState.CurrentWorldState.Civilizations[0];
            foreach (var resource in Enum.GetValues<SettlersOfIdlestan.Model.IslandMap.Resource>())
                civ.AddResource(resource, 100);
        }
    }

    public void NotifyPageVisible(double hiddenSeconds)
    {
        _gameControllerService?.AddOfflineSeconds(hiddenSeconds);
    }

    public void NotifyError(Exception ex)
    {
        var eventLog = _gameControllerService?.CurrentGameState?.CurrentWorldState?.EventLog;
        eventLog?.Add(SettlersOfIdlestan.Model.Game.GameEventType.RuntimeError, ex.Message);
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

    private void OnCityDestroyedCheckGameOver(object? sender, SettlersOfIdlestan.Controller.Military.CityDestroyedEventArgs e)
    {
        var playerCiv = _gameControllerService?.PlayerCivilization;
        if (playerCiv != null && playerCiv.Cities.Count == 0)
            _gameOverPending = true;
    }

    private void HandleGameOverRestart()
    {
        var prevCiv = _gameControllerService?.PlayerCivilization;
        _gameControllerService?.RestartIsland();
        if (_playerResourcesOverlayRenderer != null && _gameControllerService?.PlayerCivilization != null)
            _playerResourcesOverlayRenderer.ConnectLowStock(prevCiv, _gameControllerService.PlayerCivilization);
        _gameControllerService?.CityBuildingService?.ClearSelectedCity();
        _wonderService?.ClearSelectedWonder();
        _constructionInteractionService?.ClearHover();
        CenterCameraOnStartingCity();
        _overlayRenderer?.Show();
        ResetPointerState();
        _gameControllerService?.CurrentGameState?.Clock?.Resume();
    }

    private void ResetPointerState()
    {
        _isPointerDown = false;
        _isPanning = false;
    }

    private void StartNewGameIntro()
    {
        if (_introRenderer == null || _gameControllerService?.CurrentGameState is not SettlersOfIdlestan.Model.Game.MainGameState state)
            return;
        ResetPointerState();
        _introRenderer.StartIntro(state);
        state.Clock?.Pause();
        CenterCameraOnStartingCity();
        _tutorialService?.InitializeForNewGame(state);
    }

    private void StartNewGameIntro(SettlersOfIdlestan.Model.Game.MainGameState state)
    {
        if (_introRenderer == null) return;
        ResetPointerState();
        _introRenderer.StartIntro(state);
        state.Clock?.Pause();
    }

    private void OnWonderSelectionEntered(object? sender, EventArgs e)
    {
        _gameControllerService?.CurrentGameState?.Clock?.Pause();
        _overlayRenderer?.Hide();
    }

    private void OnWonderPlacementConfirmed(object? sender, HexCoord hex)
    {
        _gameControllerService?.MainGameController.WonderController.PlaceWonder(hex);
        _gameControllerService?.CurrentGameState?.Clock?.Resume();
        _overlayRenderer?.Show(suppressNextPress: true);
    }

    private void OnWonderSelectionCancelled(object? sender, EventArgs e)
    {
        _gameControllerService?.CurrentGameState?.Clock?.Resume();
        _overlayRenderer?.Show(suppressNextPress: true);
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

        var prevCiv = _gameControllerService.PlayerCivilization;
        _gameControllerService.PerformPrestige();
        if (_playerResourcesOverlayRenderer != null && _gameControllerService.PlayerCivilization != null)
            _playerResourcesOverlayRenderer.ConnectLowStock(prevCiv, _gameControllerService.PlayerCivilization);
        _gameControllerService.CityBuildingService?.ClearSelectedCity();
        _wonderService?.ClearSelectedWonder();
        _constructionInteractionService?.ClearHover();

        CenterCameraOnStartingCity();

        _islandMainRenderer?.EndBlackFade();
        _overlayRenderer?.Show();
        if (_gameControllerService.CurrentGameState?.Settings?.PauseAfterPrestige != false)
            _gameControllerService.CurrentGameState?.Clock?.Pause();
        _prestigeTransitionPending = false;
    }

    private void CenterCameraOnStartingCity()
    {
        if (_cameraService == null) return;

        const float DefaultZoom = 1.0f;
        const float HexSize = 40f;
        float sqrt3 = (float)Math.Sqrt(3);

        var WorldState = _gameControllerService?.CurrentGameState?.CurrentWorldState;
        var playerCity = WorldState?.PlayerCivilization?.Cities?.FirstOrDefault();

        if (playerCity != null)
        {
            var v = playerCity.Position;
            float x1 = HexSize * sqrt3 * (v.Hex1.Q + v.Hex1.R / 2f), y1 = HexSize * -3f / 2f * v.Hex1.R;
            float x2 = HexSize * sqrt3 * (v.Hex2.Q + v.Hex2.R / 2f), y2 = HexSize * -3f / 2f * v.Hex2.R;
            float x3 = HexSize * sqrt3 * (v.Hex3.Q + v.Hex3.R / 2f), y3 = HexSize * -3f / 2f * v.Hex3.R;
            _cameraService.SetZoom(DefaultZoom, keepCenteredOnScreen: false);
            _cameraService.CenterOn((x1 + x2 + x3) / 3f, (y1 + y2 + y3) / 3f);
        }
        else
        {
            var hexCoords = WorldState?.Layers.GetValueOrDefault(IslandMap.SurfaceLayer)?.Map?.Tiles?.Keys ?? Enumerable.Empty<HexCoord>();
            _cameraService.FitMapToView(hexCoords);
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _corruptSavePopup?.Dispose();
        _corruptSavePopup = null;
        _constructionInteractionService?.Cleanup();
        _militaryInteractionService?.Cleanup();
        _renderService?.Dispose();
        _resourceManager?.Dispose();

        _constructionInteractionService = null;
        _militaryInteractionService = null;
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
