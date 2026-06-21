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
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Renderers.Overlay.Panels;
using SettlersOfIdlestan.Controller.Achievements;
using SettlersOfIdlestan.Model.Achievements;
using SettlersOfIdlestan.Model.Tasks;

namespace SettlersOfIdlestanSkia.Screens;

/// <summary>
/// Écran de jeu — contient toute la logique de rendu et de simulation de la partie.
/// Créé quand le joueur lance ou reprend une partie depuis le TitleScreen.
/// </summary>
public sealed class GameScreen : IDisposable
{
    private readonly ResourceManager _resourceManager;
    private InputHandlingService _inputService;
    private RenderService _renderService;
    private readonly GameControllerService _gameControllerService;
    private readonly CameraService _cameraService;
    private readonly LocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly UILayoutService _uiLayoutService;

    private HarvestService? _harvestService;
    private ConstructionInteractionService? _constructionInteractionService;
    private IslandMainRenderer? _islandMainRenderer;
    private OverlayRenderer? _overlayRenderer;
    private IntroAnimationRenderer? _introRenderer;
    private bool _wasIntroActive;
    private TargetSelectionService? _targetSelectionService;
    private MonumentService? _monumentService;
    private TutorialRenderer? _tutorialRenderer;
    private TutorialService? _tutorialService;
    private MilitaryInteractionService? _militaryInteractionService;
    private PlayerResourcesOverlayRenderer? _playerResourcesOverlayRenderer;
    private NotificationToastRenderer? _notificationToastRenderer;
    private CorruptSavePopupRenderer? _corruptSavePopup;
    private bool _corruptSavePending;
    private string? _corruptSaveJson;
    private GameOverPopupRenderer? _gameOverPopup;
    private bool _gameOverPending;
    private HardResetPopupRenderer? _hardResetPopup;
    private DebugPanelRenderer? _debugPanelRenderer;
    private DemoEndPopupRenderer? _demoEndPopup;
    private bool _prestigeTransitionPending;
    private bool _demoReplayPending;
    private bool _corruptedPrestigePending;

    private bool _isDisposed;
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
    private readonly System.Diagnostics.Stopwatch _fpsStopwatch  = new();
    private int _frameCount;
    private RuntimeDebugStats? _pendingDebugStats;

    private double _autoSaveTimer;
    private const double AutoSaveInterval = 5.0;

    private Func<int> _currentLayer => () => _gameControllerService.CurrentGameState?.CurrentWorldState?.CurrentViewedLayer ?? 0;

    /// <summary>Déclenché après confirmation d'un hard reset — retourne à l'écran titre.</summary>
    public event Action? ReturnToTitleRequested;

    /// <summary>Déclenché lors du "Quit" sur la popup de sauvegarde corrompue.</summary>
    public event Action? QuitRequested;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<int, int>? DebugWindowResizeRequested;

    public GameSettings? GetCurrentSettings() =>
        _gameControllerService.MainGameController.CurrentMainState?.Settings;

    public GameScreen(
        IFileSystemService fileSystemService,
        LocalizationService localizationService,
        UILayoutService uiLayoutService,
        ResourceManager resourceManager,
        string? saveJson,
        bool allowDebugMode,
        bool demoMode = false,
        StoreController? storeController = null)
    {
        _fileSystemService    = fileSystemService;
        _localizationService  = localizationService;
        _uiLayoutService      = uiLayoutService;
        _resourceManager      = resourceManager;
        _inputService         = new InputHandlingService();
        _renderService        = new RenderService();
        _cameraService        = new CameraService();
        _gameControllerService = new GameControllerService();

        bool isNewGame;

        try
        {
            if (!string.IsNullOrEmpty(saveJson))
            {
                _gameControllerService.ImportMainState(saveJson);
                isNewGame = false;
            }
            else
            {
                _gameControllerService.InitializeNewGame();
                isNewGame = true;
            }

            MainGameState? gs = _gameControllerService.CurrentGameState;
            if (gs != null)
            {
                gs.Settings.DemoMode = demoMode;
                gs.IsDemoSave = demoMode;
                ApplyManualUiScale(gs.Settings.UiScale);
            }

            SetupRenderers(isNewGame, allowDebugMode);
        }
        catch
        {
            _corruptSaveJson    = saveJson;
            _corruptSavePending = true;

            _constructionInteractionService?.Cleanup();
            _constructionInteractionService = null;
            _militaryInteractionService?.Cleanup();
            _militaryInteractionService = null;
            _renderService.Dispose();
            _renderService = new RenderService();
            _inputService  = new InputHandlingService();

            _gameControllerService.InitializeNewGame();
            SetupRenderers(false, allowDebugMode);
        }

        if (_corruptSavePending)
        {
            _corruptSavePopup = new CorruptSavePopupRenderer(
                _localizationService,
                _fileSystemService,
                _corruptSaveJson!,
                onStartFresh: () => { _corruptSavePending = false; },
                onQuit:       () => { QuitRequested?.Invoke(); });
            _corruptSavePopup.Open();
        }

        storeController?.Connect(_gameControllerService.MainGameController.AchievementController);
        ShowStoreConnectionNotifications(storeController);

        _tickStopwatch.Restart();
        _fpsStopwatch.Restart();
        _frameCount = 0;
    }

    private void SetupRenderers(bool isNewGame, bool allowDebugMode)
    {
        var gameSettings = _gameControllerService.CurrentGameState?.Settings;
        if (gameSettings != null)
        {
            if (isNewGame)
                gameSettings.Language = _localizationService.CurrentLanguage;
            else
                _localizationService.SetLanguage(gameSettings.Language);
        }

        _harvestService = new HarvestService(_gameControllerService);

        var tooltipRenderer = new TooltipRenderer(_localizationService, _gameControllerService, _resourceManager);

        _constructionInteractionService = new ConstructionInteractionService(
            _gameControllerService,
            _harvestService,
            _inputService,
            _cameraService,
            _gameControllerService.CityBuildingService!);

        var islandMainRenderer = new IslandMainRenderer(
            _constructionInteractionService, tooltipRenderer, _localizationService,
            _gameControllerService.MainGameController.HarvestController,
            _resourceManager,
            _gameControllerService.MainGameController.MilitaryController,
            _currentLayer);
        _islandMainRenderer = islandMainRenderer;
        _constructionInteractionService.AttachRenderer(islandMainRenderer);
        _renderService.RegisterRenderer(islandMainRenderer);
        islandMainRenderer.SuppressCities = () => _introRenderer?.IsActive == true;

        _militaryInteractionService = new MilitaryInteractionService(
            _gameControllerService,
            _gameControllerService.MainGameController.MilitaryController,
            _inputService,
            _cameraService);
        _militaryInteractionService.AttachRenderer(islandMainRenderer);
        islandMainRenderer.ConnectMilitaryInteractionService(_militaryInteractionService);

        _targetSelectionService = new TargetSelectionService();
        _targetSelectionService.Entered   += OnTargetSelectionEntered;
        _targetSelectionService.Confirmed += OnTargetSelectionConfirmed;
        _targetSelectionService.Cancelled += OnTargetSelectionCancelled;

        var targetSelectionRenderer = new TargetSelectionRenderer(_targetSelectionService, _inputService, _cameraService, _localizationService);
        _renderService.RegisterRenderer(targetSelectionRenderer);

        _introRenderer = new IntroAnimationRenderer(_resourceManager);
        _renderService.RegisterRenderer(_introRenderer);

        islandMainRenderer.ConnectHarvestEvents(
            _harvestService, _gameControllerService,
            () => _prestigeTransitionPending,
            () => _overlayRenderer?.IsIslandTabActive ?? true,
            () => _gameControllerService.MainGameController.CurrentMainState?.Settings.ShowHarvestParticles ?? true);
        islandMainRenderer.ConnectMilitaryEvents(
            _gameControllerService.MainGameController.MilitaryController,
            _gameControllerService,
            () => _prestigeTransitionPending,
            () => _overlayRenderer?.IsIslandTabActive ?? true);

        var selectedCityPanelRenderer = new SelectedCityPanelRenderer(
            _gameControllerService.CityBuildingService!, _localizationService, _inputService, _resourceManager);
        selectedCityPanelRenderer.LayoutService = _uiLayoutService;

        _monumentService = new MonumentService();
        _constructionInteractionService.AttachMonumentService(_monumentService);
        islandMainRenderer.ConnectMonumentService(_monumentService);

        var selectedMonumentPanelRenderer = new SelectedMonumentPanelRenderer(_monumentService, _inputService, _localizationService, _resourceManager);

        var settingsPopupRenderer = new SettingsPopupRenderer(_gameControllerService.MainGameController, _localizationService, _fileSystemService, allowDebugMode);
        settingsPopupRenderer.FullscreenToggleRequested  += v => FullscreenToggleRequested?.Invoke(v);
        settingsPopupRenderer.UiScaleChanged             += ApplyManualUiScale;
        settingsPopupRenderer.DebugWindowResizeRequested += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);

        DebugPanelRenderer? debugPanelRenderer = null;
        if (allowDebugMode)
        {
            _debugPanelRenderer = new DebugPanelRenderer(_inputService, _localizationService, _uiLayoutService);
            debugPanelRenderer  = _debugPanelRenderer;
        }

        _hardResetPopup = new HardResetPopupRenderer(
            _localizationService,
            _fileSystemService,
            onConfirm: () => ReturnToTitleRequested?.Invoke());

        var settingsMenu = new SettingsMenu(
            _gameControllerService.MainGameController,
            _inputService, _localizationService,
            settingsPopupRenderer,
            _fileSystemService, _gameControllerService.CityBuildingService!,
            allowDebugMode, debugPanelRenderer,
            StartNewGameIntro, _uiLayoutService,
            onReturnToMenu: () => ReturnToTitleRequested?.Invoke(),
            onRestartIsland: HandleGameOverRestart);

        _playerResourcesOverlayRenderer = new PlayerResourcesOverlayRenderer(_localizationService, _resourceManager);
        _playerResourcesOverlayRenderer.ConnectLowStock(null, _gameControllerService.PlayerCivilization!);

        var tradeRenderer           = new TradePopupRenderer(_gameControllerService, _localizationService, tooltipRenderer, _resourceManager);
        var prestigeRenderer        = new PrestigeRenderer(_gameControllerService, _localizationService, RequestPrestige, tooltipRenderer);
        var prestigeMapRenderer     = new PrestigeMapRenderer(_gameControllerService, _localizationService, tooltipRenderer);
        var prestigeHistoryRenderer = new PrestigeHistoryRenderer(_gameControllerService, _localizationService);
        var timeControlRenderer     = new TimeControlRenderer(_gameControllerService, _inputService, _localizationService);
        var researchRenderer        = new ResearchRenderer(_gameControllerService, _localizationService, _inputService);
        var eventLogRenderer        = new EventLogRenderer(_gameControllerService, _localizationService);
        var automationRenderer      = new AutomationRenderer(_gameControllerService, _localizationService);
        var ritualsRenderer         = new RitualsRenderer(_gameControllerService, _localizationService, tooltipRenderer, _targetSelectionService);
        var ascensionRenderer       = new AscensionRenderer(_gameControllerService, _localizationService, tooltipRenderer);

        _overlayRenderer = new OverlayRenderer(
            _inputService, _gameControllerService, _localizationService,
            _playerResourcesOverlayRenderer, settingsMenu, settingsPopupRenderer,
            selectedCityPanelRenderer, selectedMonumentPanelRenderer,
            tradeRenderer, prestigeRenderer, prestigeMapRenderer, prestigeHistoryRenderer,
            timeControlRenderer, researchRenderer, eventLogRenderer, automationRenderer,
            ritualsRenderer, ascensionRenderer, tooltipRenderer, _uiLayoutService, allowDebugMode);
        _overlayRenderer.ConnectTargetSelectionService(_targetSelectionService);
        _overlayRenderer.ConnectZoomCallbacks(
            () => _cameraService.SetZoom(_cameraService.ZoomLevel * ZoomStep),
            () => _cameraService.SetZoom(_cameraService.ZoomLevel / ZoomStep));
        _renderService.RegisterRenderer(_overlayRenderer);

        _constructionInteractionService.ShouldSuppressHover = pos =>
            (_overlayRenderer.IsPointBlockedByUI(pos))
            || (_targetSelectionService.IsActive)
            || (_militaryInteractionService.ShouldSuppressConstruction);

        if (allowDebugMode)
        {
            _renderService.RegisterRenderer(new DebugOverlayRenderer(_inputService, _cameraService, islandMainRenderer, _localizationService));
            _renderService.RegisterRenderer(new AutoplayerDebugRenderer(_gameControllerService, _inputService));
        }

        _tutorialRenderer = new TutorialRenderer(_localizationService, _inputService);
        _tutorialRenderer.LayoutService = _uiLayoutService;
        _renderService.RegisterRenderer(_tutorialRenderer);
        _renderService.RegisterRenderer(tooltipRenderer);

        _tutorialService = new TutorialService(_tutorialRenderer);
        if (_gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState tutorialState)
        {
            if (isNewGame) _tutorialService.InitializeForNewGame(tutorialState);
            else           _tutorialService.InitializeForLoadedGame(tutorialState);
        }

        _notificationToastRenderer = new NotificationToastRenderer(_uiLayoutService);
        _renderService.RegisterRenderer(_notificationToastRenderer);

        _gameControllerService.MainGameController.AchievementController.OnAchievementUnlocked += OnAchievementUnlocked;

        if (isNewGame && _gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState introState)
            StartNewGameIntro(introState);

        _gameOverPopup = new GameOverPopupRenderer(_localizationService, HandleGameOverRestart);
        _demoEndPopup  = new DemoEndPopupRenderer(_localizationService, DoDemoReplay);

        _gameControllerService.MainGameController.CityBuilderController.OnCityDestroyed += OnCityDestroyedCheckGameOver;
    }

    /// <summary>Définit l'échelle UI automatique détectée par la plateforme hôte (densité d'écran, grande résolution…).</summary>
    public void SetUiScale(float scale)
    {
        if (_uiLayoutService != null) _uiLayoutService.AutoUiScale = scale;
        SyncRenderServiceUiScale();
    }

    /// <summary>Applique le multiplicateur d'échelle manuel choisi par le joueur (slider des paramètres).</summary>
    public void ApplyManualUiScale(float multiplier)
    {
        if (_uiLayoutService != null)
            _uiLayoutService.ManualUiScaleMultiplier = Math.Clamp(multiplier, SettingsContentPanel.UiScaleMin, SettingsContentPanel.UiScaleMax);
        SyncRenderServiceUiScale();
    }

    private void SyncRenderServiceUiScale()
    {
        if (_renderService != null && _uiLayoutService != null)
            _renderService.UiScale = _uiLayoutService.UiScale;
    }

    public void EnsureCanvasInitialized(SKSize canvasSize)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(GameScreen));

        if (_isCanvasInitialized && canvasSize == _lastCanvasSize) return;

        float prevCenterX = _isCanvasInitialized ? _cameraService.Position.X + _lastCanvasSize.Width  / 2 / _cameraService.ZoomLevel : 0f;
        float prevCenterY = _isCanvasInitialized ? _cameraService.Position.Y + _lastCanvasSize.Height / 2 / _cameraService.ZoomLevel : 0f;

        _cameraService.Initialize(canvasSize);

        if (!_isCanvasInitialized) CenterCameraOnStartingCity();
        else                       _cameraService.CenterOn(prevCenterX, prevCenterY);

        _renderService.Initialize(canvasSize);
        _lastCanvasSize      = canvasSize;
        _isCanvasInitialized = true;
    }

    public void Tick()
    {
        if (_isDisposed) return;

        var elapsed   = _tickStopwatch.Elapsed.TotalSeconds;
        _tickStopwatch.Restart();
        var deltaTime = (float)Math.Clamp(elapsed, 0f, 0.1f);

        _gameControllerService.Update(deltaTime);
        DrainEventToasts();

        bool introActive = _introRenderer?.IsActive == true;
        if (introActive)       _gameControllerService.CurrentGameState?.Clock?.Pause();
        else if (_wasIntroActive) _gameControllerService.CurrentGameState?.Clock?.Resume();
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
            if (!_corruptSavePending && _gameControllerService.MainGameController.CurrentMainState != null)
            {
                var json = _gameControllerService.MainGameController.ExportMainState();
                _fileSystemService.SaveAuto(json);
            }
        }

        var fpsElapsed = _fpsStopwatch.Elapsed.TotalSeconds;
        if (fpsElapsed >= 0.5)
        {
            var fps        = (float)(_frameCount / fpsElapsed);
            _fpsStopwatch.Restart();
            _frameCount    = 0;
            var cameraPos  = _cameraService.Position;
            var (cityCount, roadCount) = GetCityRoadCounts();
            _pendingDebugStats = new RuntimeDebugStats(fps, cameraPos.X, cameraPos.Y, cityCount, roadCount);
        }
    }

    private (int cityCount, int roadCount) GetCityRoadCounts()
    {
        var civ = _gameControllerService.CurrentGameState?.CurrentWorldState?.Civilizations.FirstOrDefault();
        return civ == null ? (0, 0) : (civ.Cities.Count, civ.Roads.Count);
    }

    public void Render(SKCanvas canvas)
    {
        if (_isDisposed || _renderService == null) return;

        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null) return;

        if (_islandMainRenderer != null && _overlayRenderer != null)
            _islandMainRenderer.IsVisible = _overlayRenderer.IsIslandTabActive;

        _renderService.RenderFrame(canvas, gameState, _cameraService);

        float uiScale = _uiLayoutService.UiScale;
        _debugPanelRenderer?.Render(canvas, _lastCanvasSize, uiScale);
        _corruptSavePopup?.Render(canvas, _lastCanvasSize, uiScale);
        _gameOverPopup?.Render(canvas, _lastCanvasSize, uiScale);
        _hardResetPopup?.Render(canvas, _lastCanvasSize, uiScale);
        _demoEndPopup?.Render(canvas, _lastCanvasSize, uiScale);
    }

    public void HandlePointerPressed(float x, float y, int pointerId, PointerButton button)
    {
        if (_hardResetPopup?.IsOpen == true)  { _hardResetPopup.HandlePointerPressed(new SKPoint(x, y), button);  return; }
        if (_corruptSavePopup?.IsOpen == true) { _corruptSavePopup.HandlePointerPressed(new SKPoint(x, y), button); return; }
        if (_gameOverPopup?.IsOpen == true)    { _gameOverPopup.HandlePointerPressed(new SKPoint(x, y), button);   return; }
        if (_demoEndPopup?.IsOpen == true)     { _demoEndPopup.HandlePointerPressed(new SKPoint(x, y), button);    return; }
        if (_introRenderer?.IsActive == true)  return;
        if (_notificationToastRenderer?.HandlePointerPressed(new SKPoint(x, y)) == true) return;

        _isPointerDown        = true;
        _isPanning            = false;
        _isPanSuppressedAtStart = _overlayRenderer?.IsPointBlockedByUI(new SKPoint(x, y)) ?? false;
        _activePanPointerId   = pointerId;
        _panStartPoint        = new SKPoint(x, y);
        _lastPanPoint         = _panStartPoint;
        _inputService.HandlePointerPressed(x, y, pointerId, button);

        if (_militaryInteractionService?.IsPotentialDragFromCity == true)
            _isPanSuppressedAtStart = true;
    }

    public void HandlePointerMoved(float x, float y, int pointerId)
    {
        if (_hardResetPopup?.IsOpen  == true) return;
        if (_corruptSavePopup?.IsOpen == true) return;
        if (_gameOverPopup?.IsOpen   == true) return;
        if (_demoEndPopup?.IsOpen    == true) return;
        if (_introRenderer?.IsActive == true) return;

        if (_isPointerDown && !_isPanSuppressedAtStart && (_overlayRenderer?.IsIslandTabActive ?? true)
            && pointerId == _activePanPointerId && _cameraService != null)
        {
            var point  = new SKPoint(x, y);
            var dx     = point.X - _panStartPoint.X;
            var dy     = point.Y - _panStartPoint.Y;
            if (!_isPanning && dx * dx + dy * dy >= PanStartThresholdSquared) _isPanning = true;
            if (_isPanning) _cameraService.Pan(point.X - _lastPanPoint.X, point.Y - _lastPanPoint.Y);
            _lastPanPoint = point;
        }

        _inputService.HandlePointerMoved(x, y, pointerId);
    }

    public void HandlePointerReleased(float x, float y, int pointerId, PointerButton button)
    {
        if (_hardResetPopup?.IsOpen  == true) return;
        if (_corruptSavePopup?.IsOpen == true) return;
        if (_gameOverPopup?.IsOpen   == true) return;
        if (_demoEndPopup?.IsOpen    == true) return;
        if (_introRenderer?.IsActive == true) return;

        bool wasPanning = _isPanning && pointerId == _activePanPointerId;
        _isPointerDown  = false;
        _isPanning      = false;

        if (!wasPanning) _inputService.HandlePointerReleased(x, y, pointerId, button);
    }

    public void HandleZoom(float wheelDelta, float x, float y)
    {
        if (_introRenderer?.IsActive == true || _cameraService == null || wheelDelta == 0) return;

        bool overUI = _overlayRenderer?.IsPointBlockedByUI(new SKPoint(x, y)) ?? false;
        if (!overUI && (_overlayRenderer?.IsIslandTabActive ?? true))
        {
            var zoomFactor = wheelDelta > 0 ? ZoomStep : 1f / ZoomStep;
            _cameraService.ZoomAt(_cameraService.ZoomLevel * zoomFactor, new SKPoint(x, y));
        }
        _inputService.HandleZoom(wheelDelta, x, y);
    }

    public void HandlePinch(float scaleRatio, float x, float y, float panDeltaX, float panDeltaY)
    {
        if (_introRenderer?.IsActive == true || _cameraService == null || scaleRatio <= 0f) return;

        bool overUI = _overlayRenderer?.IsPointBlockedByUI(new SKPoint(x, y)) ?? false;
        if (!overUI && (_overlayRenderer?.IsIslandTabActive ?? true))
        {
            if (panDeltaX != 0f || panDeltaY != 0f) _cameraService.Pan(panDeltaX, panDeltaY);
            _cameraService.ZoomAt(_cameraService.ZoomLevel * scaleRatio, new SKPoint(x, y));
        }
    }

    public void HandleKeyReleased(string key) => _inputService.HandleKeyReleased(key);

    public void HandleKeyPressed(string key, bool allowDebugMode)
    {
        _inputService.HandleKeyPressed(key);
        if (key == "Space") TogglePause();
        if (key == "C"   && allowDebugMode) DebugAddResources();
        if (key == "F9"  && allowDebugMode) DebugExportIconCaptures();
        if (key == "F10" && allowDebugMode) DebugExportScreenshotWithInterface();
        if (key == "F11" && allowDebugMode) DebugExportScreenshotWithTitle();
        if (key == "F12" && allowDebugMode) DebugExportScreenshotRaw();
    }

    private void TogglePause()
    {
        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;
        if (clock.SpeedMultiplier == 0) clock.Resume();
        else clock.Pause();
    }

    /// <summary>
    /// Outil de debug (F9) : exporte la vue caméra actuelle directement à 256/128/64/32/16, en
    /// gardant le même cadrage (champ de vue) à chaque taille — le zoom est réduit proportionnellement
    /// à la taille du canvas (sinon une taille plus petite recadrerait sur le carré central, en montrant
    /// moins de scène). Le rendu reste natif à chaque taille (pas de downscale d'un grand screenshot),
    /// ce qui garde les contours/traits lisibles.
    /// </summary>
    private void DebugExportIconCaptures()
    {
        if (_islandMainRenderer == null) return;
        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null) return;

        var liveCanvasSize = _cameraService.CanvasSize;
        var canvasCenter = new SKPoint(liveCanvasSize.Width / 2f, liveCanvasSize.Height / 2f);
        var worldCenter = _cameraService.ScreenToWorld(canvasCenter);
        float liveZoom = _cameraService.ZoomLevel;
        string outputDir = FindExportDirectory();

        foreach (int size in new[] { 256, 128, 64, 32, 16 })
        {
            float zoom = liveZoom * size / liveCanvasSize.Width;
            var position = new SKPoint(
                worldCenter.X - size / 2f / zoom,
                worldCenter.Y - size / 2f / zoom);

            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            var context = new GameRenderContext
            {
                GameState = gameState,
                DeltaTime = 0f,
                CanvasSize = new SKSize(size, size),
                CameraPosition = position,
                ZoomLevel = zoom,
                UiScale = 1f
            };

            _islandMainRenderer.Render(canvas, context);

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(outputDir, $"icon_export_{size}.png"));
            data.SaveTo(stream);
        }
    }

    /// <summary>Outil de debug (F10) : capture l'écran de jeu tel qu'affiché à l'écran, interface comprise
    /// (barres de ressources, onglets, tooltips…), à la résolution actuelle de la fenêtre.</summary>
    private void DebugExportScreenshotWithInterface()
    {
        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null) return;

        var canvasSize = _cameraService.CanvasSize;
        if (!TryCreateExportSurface(canvasSize, out var surface)) return;
        using (surface)
        {
            _renderService.RenderFrame(surface.Canvas, gameState, _cameraService);
            SaveExportPng(surface, "screenshot_interface.png");
        }
    }

    /// <summary>Outil de debug (F11) : capture la scène de jeu sans interface, avec le titre du jeu superposé
    /// comme sur l'écran titre (sur une ou deux lignes selon la largeur disponible).</summary>
    private void DebugExportScreenshotWithTitle()
    {
        if (_islandMainRenderer == null) return;
        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null) return;

        var canvasSize = _cameraService.CanvasSize;
        if (!TryCreateExportSurface(canvasSize, out var surface)) return;
        using (surface)
        {
            var canvas = surface.Canvas;
            var context = new GameRenderContext
            {
                GameState = gameState,
                DeltaTime = 0f,
                CanvasSize = canvasSize,
                CameraPosition = _cameraService.Position,
                ZoomLevel = _cameraService.ZoomLevel,
                UiScale = _uiLayoutService.UiScale
            };
            _islandMainRenderer.Render(canvas, context);

            DrawTitleOverlay(canvas, canvasSize, _uiLayoutService.UiScale);

            SaveExportPng(surface, "screenshot_title.png");
        }
    }

    /// <summary>Outil de debug (F12) : capture la scène de jeu sans interface</summary>
    private void DebugExportScreenshotRaw()
    {
        if (_islandMainRenderer == null) return;
        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null) return;

        var canvasSize = _cameraService.CanvasSize;
        if (!TryCreateExportSurface(canvasSize, out var surface)) return;
        using (surface)
        {
            var canvas = surface.Canvas;
            var context = new GameRenderContext
            {
                GameState = gameState,
                DeltaTime = 0f,
                CanvasSize = canvasSize,
                CameraPosition = _cameraService.Position,
                ZoomLevel = _cameraService.ZoomLevel,
                UiScale = _uiLayoutService.UiScale
            };
            _islandMainRenderer.Render(canvas, context);

            SaveExportPng(surface, "screenshot_raw.png");
        }
    }

    private static void DrawTitleOverlay(SKCanvas canvas, SKSize canvasSize, float s)
    {
        const string title = "Settlers of Idlestan";
        using var titleFont = new SKFont { Size = 80f * s, Typeface = SkiaFonts.Bold };
        using var titlePaint   = new SKPaint { Color = new SKColor(230, 190, 90), IsAntialias = true };
        using var dividerPaint = new SKPaint { Color = new SKColor(100, 85, 45), StrokeWidth = 2f * s, Style = SKPaintStyle.Stroke };

        // Halo doux + contour net en noir, dessinés sous le texte doré, pour garder le titre lisible
        // même sur un fond clair (tuiles d'eau/herbe) sans changer la police elle-même.
        float glowBlurRadius = 6f * s;
        using var glowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 130),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowBlurRadius)
        };
        using var outlinePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 5f * s,
            StrokeJoin = SKStrokeJoin.Round
        };

        float cx       = canvasSize.Width / 2f;
        float margin   = 40f * s;
        float maxWidth = Math.Max(10f, canvasSize.Width - margin * 2f);
        float fullWidth = titleFont.MeasureText(title);

        // Une seule ligne si elle tient dans la largeur disponible, sinon découpage par mot (1 ou 2 lignes).
        List<string> lines = fullWidth <= maxWidth
            ? [title]
            : SkiaTextUtils.MeasureWrappedText(title, maxWidth, titleFont).Lines;

        float lineH  = titleFont.Spacing;

        // Le contour noir et le halo dépassent du glyphe (demi-épaisseur du trait + rayon du flou) :
        // il faut en tenir compte dans la marge du haut, sinon le contour des lettres les plus hautes
        // (ascendantes) est rogné par le bord du canvas.
        titleFont.GetFontMetrics(out var fontMetrics);
        float topOverhang  = -fontMetrics.Top;
        float strokeMargin = outlinePaint.StrokeWidth / 2f;
        float glowMargin   = glowBlurRadius;
        float titleY = 10f * s + topOverhang + strokeMargin + glowMargin;
        foreach (var line in lines)
        {
            float lineW = titleFont.MeasureText(line);
            float lineX = cx - lineW / 2f;
            SkiaTextUtils.DrawText(canvas, line, lineX, titleY, titleFont, glowPaint);
            SkiaTextUtils.DrawText(canvas, line, lineX, titleY, titleFont, outlinePaint);
            SkiaTextUtils.DrawText(canvas, line, lineX, titleY, titleFont, titlePaint);
            titleY += lineH;
        }

        float divY     = titleY - lineH + 18f * s;
        float divHalfW = Math.Min(220f * s, cx - 20f * s);
        canvas.DrawLine(cx - divHalfW, divY, cx + divHalfW, divY, dividerPaint);
    }

    private static bool TryCreateExportSurface(SKSize canvasSize, out SKSurface surface)
    {
        // Ceiling plutôt que Round : un export plus petit que la fenêtre réelle coupe l'image,
        // alors qu'un export légèrement plus grand est inoffensif.
        int width  = (int)MathF.Ceiling(canvasSize.Width);
        int height = (int)MathF.Ceiling(canvasSize.Height);
        if (width <= 0 || height <= 0)
        {
            surface = null!;
            return false;
        }

        surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(DebugSettings.ExportTransparentBackground ? SKColors.Transparent : SKColors.Black);
        return true;
    }

    private static void SaveExportPng(SKSurface surface, string fileName)
    {
        string outputDir = FindExportDirectory();
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(Path.Combine(outputDir, fileName));
        data.SaveTo(stream);
    }

    private static string FindExportDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SettlersOfIdlestan.slnx")))
            {
                var exportDir = Path.Combine(dir.FullName, "assets", "export");
                Directory.CreateDirectory(exportDir);
                return exportDir;
            }
        }
        Directory.CreateDirectory(AppContext.BaseDirectory);
        return AppContext.BaseDirectory;
    }

    private void DebugAddResources()
    {
        var mainState = _gameControllerService.MainGameController.CurrentMainState;
        if (mainState?.CurrentWorldState?.Civilizations.Count > 0)
        {
            var civ = mainState.CurrentWorldState.Civilizations[0];
            foreach (var resource in Enum.GetValues<SettlersOfIdlestan.Model.IslandMap.Resource>())
                civ.AddResource(resource, 100);
        }
    }

    public void NotifyPageVisible(double hiddenSeconds) => _gameControllerService.AddOfflineSeconds(hiddenSeconds);

    public void NotifyError(Exception ex)
    {
        var eventLog = _gameControllerService.CurrentGameState?.CurrentWorldState?.EventLog;
        eventLog?.Add(SettlersOfIdlestan.Model.Game.GameEventType.RuntimeError, ex.Message);
    }

    public bool TryGetDebugStats(out RuntimeDebugStats stats)
    {
        if (_pendingDebugStats is { } pending)
        {
            stats              = pending;
            _pendingDebugStats = null;
            return true;
        }
        stats = default;
        return false;
    }

    private void DrainEventToasts()
    {
        var eventLog = _gameControllerService.CurrentGameState?.CurrentWorldState?.EventLog;
        if (eventLog == null || _notificationToastRenderer == null) return;
        while (eventLog.TryDequeueToast(out var entry))
            ShowEventToast(entry);
    }

    private void ShowEventToast(GameLogEntry entry)
    {
        if (_notificationToastRenderer == null) return;
        var (title, message, icon) = entry.Type switch
        {
            GameEventType.WonderLevelUp => (
                _localizationService.Get("event_wonder_levelup_title"),
                _localizationService.GetFormated("event_wonder_levelup_body", entry.Message ?? "?"),
                NotificationIcon.Achievement),
            GameEventType.CivilizationDiscovered => (
                _localizationService.Get("event_civilization_discovered_title"),
                _localizationService.Get("event_civilization_discovered_body"),
                NotificationIcon.Info),
            GameEventType.BanditHideoutDiscovered => (
                _localizationService.Get("event_bandit_hideout_title"),
                _localizationService.Get("event_bandit_hideout_body"),
                NotificationIcon.StoreFail),
            GameEventType.DragonDiscovered => (
                _localizationService.Get("event_dragon_discovered_title"),
                _localizationService.Get("event_dragon_discovered_body"),
                NotificationIcon.StoreFail),
            GameEventType.MinorDemonDiscovered => (
                _localizationService.Get("event_minor_demon_discovered_title"),
                _localizationService.Get("event_minor_demon_discovered_body"),
                NotificationIcon.StoreFail),
            _ => (entry.Type.ToString(), entry.Message ?? string.Empty, NotificationIcon.Info)
        };
        _notificationToastRenderer.ShowNotification(title, message, icon);
    }

    private void OnAchievementUnlocked(object? sender, AchievementId id)
    {
        if (_notificationToastRenderer == null) return;
        var def = AchievementDefinitions.All.FirstOrDefault(d => d.Id == id);
        string title   = _localizationService.Get("notification_achievement_title");
        string message = def != null ? _localizationService.Get(def.NameKey) : id.ToString();
        _notificationToastRenderer.ShowNotification(title, message, NotificationIcon.Achievement);
    }

    internal void ShowStoreConnectionNotifications(StoreController? storeController)
    {
        if (storeController == null || _notificationToastRenderer == null) return;
        foreach (var (name, status) in storeController.GetConnectionStatuses())
        {
            if (status == StoreConnectionStatus.Connected)
            {
                string msg = _localizationService.GetFormated("notification_store_connected", name);
                _notificationToastRenderer.ShowNotification(msg, string.Empty, NotificationIcon.StoreOk);
            }
            else if (status == StoreConnectionStatus.Failed)
            {
                string msg = _localizationService.GetFormated("notification_store_failed", name);
                _notificationToastRenderer.ShowNotification(msg, string.Empty, NotificationIcon.StoreFail);
            }
        }
    }

    private void OnCityDestroyedCheckGameOver(object? sender, SettlersOfIdlestan.Controller.Island.CityDestroyedEventArgs e)
    {
        var playerCiv = _gameControllerService.PlayerCivilization;
        if (playerCiv != null && playerCiv.Cities.Count == 0)
            _gameOverPending = true;
    }

    private void HandleGameOverRestart()
    {
        var prevCiv = _gameControllerService.PlayerCivilization;
        _gameControllerService.RestartIsland();
        if (_playerResourcesOverlayRenderer != null && _gameControllerService.PlayerCivilization != null)
            _playerResourcesOverlayRenderer.ConnectLowStock(prevCiv, _gameControllerService.PlayerCivilization);
        _gameControllerService.CityBuildingService?.ClearSelectedCity();
        _monumentService?.ClearSelectedInvestable();
        _constructionInteractionService?.ClearHover();
        CenterCameraOnStartingCity();
        _overlayRenderer?.Show();
        ResetPointerState();
        _gameControllerService.CurrentGameState?.Clock?.Resume();
    }

    private void ResetPointerState() { _isPointerDown = false; _isPanning = false; }

    private void StartNewGameIntro()
    {
        if (_introRenderer == null || _gameControllerService.CurrentGameState is not SettlersOfIdlestan.Model.Game.MainGameState state) return;
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

    private void OnTargetSelectionEntered(object? sender, EventArgs e)
    {
        _gameControllerService.CurrentGameState?.Clock?.Pause();
        _overlayRenderer?.SwitchToIslandTab();
        _overlayRenderer?.Hide();
    }

    private void OnTargetSelectionConfirmed(object? sender, EventArgs e)
    {
        _gameControllerService.CurrentGameState?.Clock?.Resume();
        _overlayRenderer?.Show(suppressNextPress: true);
    }

    private void OnTargetSelectionCancelled(object? sender, EventArgs e)
    {
        _gameControllerService.CurrentGameState?.Clock?.Resume();
        _overlayRenderer?.Show(suppressNextPress: true);
    }

    private void RequestPrestige(bool corrupted)
    {
        if (_prestigeTransitionPending || _islandMainRenderer == null || _overlayRenderer == null) return;

        var mainState = _gameControllerService.MainGameController.CurrentMainState;
        if (mainState?.Settings.DemoMode == true && (mainState.PrestigeState?.RunHistory.Count ?? 0) >= 2)
        {
            _demoEndPopup?.Open();
            return;
        }

        DoPrestige(corrupted);
    }

    private void DoPrestige(bool corrupted)
    {
        if (_prestigeTransitionPending || _islandMainRenderer == null || _overlayRenderer == null) return;
        _overlayRenderer.Hide();
        _islandMainRenderer.BeginBlackFade(0.5f);
        _prestigeTransitionPending = true;
        _corruptedPrestigePending = corrupted;
    }

    private void DoDemoReplay()
    {
        if (_prestigeTransitionPending || _islandMainRenderer == null || _overlayRenderer == null) return;
        _overlayRenderer.Hide();
        _islandMainRenderer.BeginBlackFade(0.5f);
        _prestigeTransitionPending = true;
        _demoReplayPending = true;
        _corruptedPrestigePending = false;
    }

    private void CompletePrestigeTransition()
    {
        if (_cameraService == null) return;

        var prevCiv = _gameControllerService.PlayerCivilization;
        bool corrupted = _corruptedPrestigePending;
        _corruptedPrestigePending = false;
        if (_demoReplayPending)
        {
            _demoReplayPending = false;
            _gameControllerService.PerformPrestigeAndRestartCurrentIsland(corrupted);
        }
        else
        {
            _gameControllerService.PerformPrestige(corrupted);
        }
        if (_playerResourcesOverlayRenderer != null && _gameControllerService.PlayerCivilization != null)
            _playerResourcesOverlayRenderer.ConnectLowStock(prevCiv, _gameControllerService.PlayerCivilization);
        _gameControllerService.CityBuildingService?.ClearSelectedCity();
        _monumentService?.ClearSelectedInvestable();
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
        float sqrt3 = (float)Math.Sqrt(3);

        var worldState = _gameControllerService.CurrentGameState?.CurrentWorldState;
        var playerCity = worldState?.PlayerCivilization?.Cities?.FirstOrDefault();

        if (playerCity != null)
        {
            var v  = playerCity.Position;
            float x1 = GameConstants.HexSize * sqrt3 * (v.Hex1.Q + v.Hex1.R / 2f), y1 = GameConstants.HexSize * -3f / 2f * v.Hex1.R;
            float x2 = GameConstants.HexSize * sqrt3 * (v.Hex2.Q + v.Hex2.R / 2f), y2 = GameConstants.HexSize * -3f / 2f * v.Hex2.R;
            float x3 = GameConstants.HexSize * sqrt3 * (v.Hex3.Q + v.Hex3.R / 2f), y3 = GameConstants.HexSize * -3f / 2f * v.Hex3.R;
            var canvas = _cameraService.CanvasSize;
            float maxDim    = Math.Max(canvas.Width, canvas.Height);
            float minZoom   = maxDim > 0f ? maxDim * 0.1f / (2f * GameConstants.HexSize) : DefaultZoom;
            float zoom      = Math.Max(DefaultZoom, minZoom);
            _cameraService.SetZoom(zoom, keepCenteredOnScreen: false);
            _cameraService.CenterOn((x1 + x2 + x3) / 3f, (y1 + y2 + y3) / 3f);
        }
        else
        {
            var hexCoords = worldState?.Layers.GetValueOrDefault(IslandMap.SurfaceLayer)?.Map?.Tiles?.Keys ?? Enumerable.Empty<HexCoord>();
            _cameraService.FitMapToView(hexCoords);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _debugPanelRenderer?.Dispose();
        _corruptSavePopup?.Dispose();
        _hardResetPopup?.Dispose();
        _demoEndPopup?.Dispose();
        _constructionInteractionService?.Cleanup();
        _militaryInteractionService?.Cleanup();
        _renderService.Dispose();
        _isDisposed = true;
    }
}
