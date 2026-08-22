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
    private readonly StoreController? _storeController;
    private readonly Action<Action>? _runSynchronized;

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
    private TooltipRenderer? _tooltipRenderer;
    private NotificationToastRenderer? _notificationToastRenderer;
    private CorruptSavePopupRenderer? _corruptSavePopup;
    private bool _corruptSavePending;
    private string? _corruptSaveJson;
    private GameOverPopupRenderer? _gameOverPopup;
    private bool _gameOverPending;
    private HardResetPopupRenderer? _hardResetPopup;
    private RestartIslandPopupRenderer? _restartIslandPopup;
    private DebugPanelRenderer? _debugPanelRenderer;
    private DemoEndPopupRenderer? _demoEndPopup;
    private bool _prestigeTransitionPending;
    private bool _demoReplayPending;
    private bool _corruptedPrestigePending;
    private int _speedBeforeTargetSelection = 1;

    private bool _isDisposed;
    private bool _isCanvasInitialized;
    private SKSize _lastCanvasSize;

    private bool _isPointerDown;
    private bool _isPanning;
    private bool _isPanSuppressedAtStart;
    private long _activePanPointerId;
    private SKPoint _lastPanPoint;
    private SKPoint _panStartPoint;
    private const float PanStartThresholdSquared = 100f;
    private const float ZoomStep = 1.12f;

    private readonly System.Diagnostics.Stopwatch _tickStopwatch = new();
    private readonly System.Diagnostics.Stopwatch _fpsStopwatch  = new();
    private int _frameCount;

    private double _autoSaveTimer;
    private const double AutoSaveInterval = 5.0;
    private const string CloudSaveFileName = "autosave.json";

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
        StoreController? storeController = null,
        string? statsJson = null,
        Action<Action>? runSynchronized = null)
    {
        _runSynchronized      = runSynchronized;
        _fileSystemService    = fileSystemService;
        _localizationService  = localizationService;
        _uiLayoutService      = uiLayoutService;
        _resourceManager      = resourceManager;
        _storeController      = storeController;
        _inputService         = new InputHandlingService();
        _renderService        = new RenderService();
        _cameraService        = new CameraService();
        _gameControllerService = new GameControllerService();

        // Stats à vie chargées depuis un fichier dédié, indépendant de la sauvegarde de partie,
        // pour qu'elles survivent à "Nouvelle partie" / hard reset.
        _gameControllerService.MainGameController.LifetimeStats = ParseLifetimeStats(statsJson);

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
                _uiLayoutService.SetMenuPosition(gs.Settings.ForceMenuPosition);
            }

            SetupRenderers(isNewGame, allowDebugMode);

            if (!isNewGame)
            {
                var playerCiv = _gameControllerService.PlayerCivilization;
                if (playerCiv != null && playerCiv.Cities.Count == 0)
                    _gameOverPending = true;
            }
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
            {
                gameSettings.Language = _localizationService.CurrentLanguage;
                // Hérite du choix fait sur l'écran-titre (le state d'une nouvelle partie a des settings par défaut).
                gameSettings.NumberFormat = SkiaTextUtils.NumberFormat;
            }
            else
            {
                _localizationService.SetLanguage(gameSettings.Language);
                SkiaTextUtils.NumberFormat = gameSettings.NumberFormat;
            }
        }

        _harvestService = new HarvestService(_gameControllerService);

        var tooltipRenderer = _tooltipRenderer =
            new TooltipRenderer(_localizationService, _gameControllerService, _resourceManager);

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
            _gameControllerService.MainGameController.MonsterFeatureController,
            _gameControllerService,
            () => _prestigeTransitionPending,
            () => _overlayRenderer?.IsIslandTabActive ?? true);
        islandMainRenderer.ConnectVolcanoEvents(
            _gameControllerService.MainGameController.VolcanoController,
            _gameControllerService,
            () => _prestigeTransitionPending,
            () => _overlayRenderer?.IsIslandTabActive ?? true);

        var selectedCityPanelRenderer = new SelectedCityPanelRenderer(
            _gameControllerService.CityBuildingService!, _localizationService, _inputService, _resourceManager);

        _monumentService = new MonumentService();
        _constructionInteractionService.AttachMonumentService(_monumentService);
        islandMainRenderer.ConnectMonumentService(_monumentService);

        var selectedMonumentPanelRenderer = new SelectedMonumentPanelRenderer(_monumentService, _inputService, _localizationService, _resourceManager, _gameControllerService, tooltipRenderer);

        var settingsPopupRenderer = new SettingsPopupRenderer(_gameControllerService.MainGameController, _localizationService, _fileSystemService, _uiLayoutService, allowDebugMode, _storeController);
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

        _restartIslandPopup = new RestartIslandPopupRenderer(
            _localizationService,
            onConfirm: HandleGameOverRestart);

        var settingsMenu = new SettingsMenu(
            _gameControllerService.MainGameController,
            _localizationService,
            settingsPopupRenderer,
            _fileSystemService, _gameControllerService.CityBuildingService!,
            allowDebugMode, debugPanelRenderer,
            StartNewGameIntro,
            onReturnToMenu: () => ReturnToTitleRequested?.Invoke(),
            // Recommencer l'île depuis le menu passe par une confirmation : le joueur perd sa
            // partie sans rien gagner. La modale de fin de partie, elle, redémarre directement —
            // il n'y a alors plus rien à perdre.
            onRestartIsland: () => _restartIslandPopup?.Open(),
            // Seul rappel du menu qui arrive après une attente du joueur (le sélecteur de
            // fichier) : il faut donc reprendre le verrou de l'hôte avant de toucher au modèle.
            onLoadGame: json => RunSynchronized(() => HandleLoadGame(json)));

        _playerResourcesOverlayRenderer = new PlayerResourcesOverlayRenderer();
        _playerResourcesOverlayRenderer.ConnectLowStock(null, _gameControllerService.PlayerCivilization!);

        var tradeRenderer           = new TradePopupRenderer(_gameControllerService, _localizationService, tooltipRenderer, _resourceManager);
        var prestigeRenderer        = new PrestigeRenderer(_gameControllerService, _localizationService, RequestPrestige, tooltipRenderer);
        var prestigeMapRenderer     = new PrestigeMapRenderer(_gameControllerService, _localizationService, tooltipRenderer, _uiLayoutService);
        var prestigeHistoryRenderer = new PrestigeHistoryRenderer(_gameControllerService, _localizationService, _uiLayoutService);
        var researchRenderer        = new ResearchRenderer(_gameControllerService, _localizationService, _inputService, _uiLayoutService);
        var eventLogRenderer        = new EventLogRenderer(_gameControllerService, _localizationService, _uiLayoutService);
        var automationRenderer      = new AutomationRenderer(_gameControllerService, _localizationService, _uiLayoutService);
        var ritualsRenderer         = new RitualsRenderer(_gameControllerService, _localizationService, tooltipRenderer, _uiLayoutService, _targetSelectionService);
        var ascensionRenderer       = new AscensionRenderer(_gameControllerService, _localizationService, tooltipRenderer, _uiLayoutService);

        HistoryTabRenderer? historyTabRenderer = null;
        if (allowDebugMode)
        {
            historyTabRenderer = new HistoryTabRenderer(
                _gameControllerService.MainGameController.CivilizationHistoryController,
                _uiLayoutService,
                _localizationService);
        }

        _overlayRenderer = new OverlayRenderer(
            _inputService, _gameControllerService, _localizationService,
            _playerResourcesOverlayRenderer, settingsMenu, settingsPopupRenderer,
            selectedCityPanelRenderer, selectedMonumentPanelRenderer,
            tradeRenderer, prestigeRenderer, prestigeMapRenderer, prestigeHistoryRenderer,
            researchRenderer, eventLogRenderer, automationRenderer,
            ritualsRenderer, ascensionRenderer, tooltipRenderer, _cameraService, _resourceManager,
            _uiLayoutService, allowDebugMode, historyTabRenderer);
        _overlayRenderer.ConnectTargetSelectionService(_targetSelectionService);
        _renderService.RegisterRenderer(_overlayRenderer);

        _constructionInteractionService.ShouldSuppressHover = pos =>
            (_overlayRenderer.IsPointBlockedByUI(pos))
            || (_targetSelectionService.IsActive)
            || (_militaryInteractionService.ShouldSuppressConstruction);

        _militaryInteractionService.ShouldSuppressInteraction = pos =>
            (_overlayRenderer.IsPointBlockedByUI(pos))
            || (_targetSelectionService.IsActive);

        if (allowDebugMode)
        {
            _renderService.RegisterRenderer(new DebugOverlayRenderer(_inputService, _cameraService, islandMainRenderer, _localizationService));
            _renderService.RegisterRenderer(new AutoplayerDebugRenderer(_gameControllerService, _inputService));
        }

        _tutorialRenderer = new TutorialRenderer(_localizationService, _inputService);
        _tutorialRenderer.LayoutService = _uiLayoutService;
        _renderService.RegisterRenderer(_tutorialRenderer);

        // Pas de RegisterRenderer : les infobulles sont dessinées par RenderTooltips, une passe
        // séparée que l'hôte place au-dessus des contrôles de l'overlay. Enregistrées ici, elles
        // finiraient sur le canevas de la carte, donc derrière les panneaux Avalonia.
        // Leur Initialize est appelé par EnsureCanvasInitialized, comme celui du RenderService.

        _tutorialService = new TutorialService(_tutorialRenderer);
        if (_gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState tutorialState)
        {
            if (isNewGame) _tutorialService.InitializeForNewGame(tutorialState);
            else           _tutorialService.InitializeForLoadedGame(tutorialState);
        }

        // Pas de RegisterRenderer : ce renderer ne dessine plus, la pile de toasts est une vue
        // Avalonia. Il reste la machine à états, avancée depuis Tick.
        _notificationToastRenderer = new NotificationToastRenderer(_uiLayoutService);

        _gameControllerService.MainGameController.AchievementController.OnAchievementUnlocked += OnAchievementUnlocked;

        if (isNewGame && _gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState introState)
            StartNewGameIntro(introState);

        _gameOverPopup = new GameOverPopupRenderer(_localizationService, HandleGameOverRestart);
        _demoEndPopup  = new DemoEndPopupRenderer(_localizationService, DoDemoReplay);

        _gameControllerService.MainGameController.CityBuilderController.OnCityDestroyed += OnCityDestroyedCheckGameOver;
    }

    /// <summary>Instantané des toasts pour une vue portée par l'hôte.</summary>
    public ToastListSnapshot GetToastSnapshot() =>
        _notificationToastRenderer?.GetSnapshot() ?? ToastListSnapshot.Empty;

    public void DismissToastFromHost(long id) => _notificationToastRenderer?.Dismiss(id);

    /// <summary>
    /// Instantané de la modale bloquante ouverte, pour une vue portée par l'hôte.
    /// Même ordre de priorité que le dispatch de <see cref="HandlePointerPressed"/> : ces modales
    /// s'excluent, mais rien n'interdit à deux d'entre elles d'être ouvertes simultanément.
    /// </summary>
    public ModalPopupSnapshot GetModalPopupSnapshot()
    {
        if (_hardResetPopup?.IsOpen   == true) return _hardResetPopup.GetSnapshot();
        if (_restartIslandPopup?.IsOpen == true) return _restartIslandPopup.GetSnapshot();
        if (_corruptSavePopup?.IsOpen == true) return _corruptSavePopup.GetSnapshot();
        if (_gameOverPopup?.IsOpen    == true) return _gameOverPopup.GetSnapshot();
        if (_demoEndPopup?.IsOpen     == true) return _demoEndPopup.GetSnapshot();

        // Puis les modales portées par l'overlay (confirmations du popup de prestige : montée de
        // corruption, perte d'essences) : même forme, même vue, donc même chaîne.
        return _overlayRenderer?.GetOverlayModalSnapshot() ?? ModalPopupSnapshot.None;
    }

    /// <summary>Déclenche un bouton de modale depuis une vue portée par l'hôte.</summary>
    public void InvokeModalPopupButtonFromHost(string popupId, string buttonKey)
    {
        switch (popupId)
        {
            case ModalPopupSnapshot.IdHardReset:   _hardResetPopup?.InvokeButton(buttonKey);   break;
            case ModalPopupSnapshot.IdRestartIsland: _restartIslandPopup?.InvokeButton(buttonKey); break;
            case ModalPopupSnapshot.IdCorruptSave: _corruptSavePopup?.InvokeButton(buttonKey); break;
            case ModalPopupSnapshot.IdGameOver:    _gameOverPopup?.InvokeButton(buttonKey);    break;
            case ModalPopupSnapshot.IdDemoEnd:     _demoEndPopup?.InvokeButton(buttonKey);     break;
            case ModalPopupSnapshot.IdPrestigeEssenceLoss:
            case ModalPopupSnapshot.IdPrestigeCorruptionWarning:
            case ModalPopupSnapshot.IdAscensionConfirm:
                _overlayRenderer?.InvokeOverlayModalButtonFromHost(popupId, buttonKey);
                break;
        }
    }

    /// <summary>Instantané de la barre d'onglets pour une vue portée par l'hôte.</summary>
    public TabBarSnapshot GetTabBarSnapshot() =>
        _overlayRenderer?.GetTabBarSnapshot() ?? TabBarSnapshot.Unavailable;

    /// <summary>Sélectionne un onglet depuis l'hôte (et applique la couche correspondante).</summary>
    public void SetActiveTabFromHost(int tabId) => _overlayRenderer?.SetActiveTabFromHost(tabId);

    /// <summary>Instantané du panneau ville pour une vue portée par l'hôte.</summary>
    public CityPanelSnapshot GetCityPanelSnapshot() =>
        _overlayRenderer?.GetCityPanelSnapshot() ?? CityPanelSnapshot.Hidden;

    public void CloseCityPanelFromHost() => _overlayRenderer?.CloseCityPanelFromHost();
    public void SetCityShowUniqueFromHost(bool v) => _overlayRenderer?.SetCityShowUniqueFromHost(v);
    public void ToggleCityBuildingActivationFromHost(string k) => _overlayRenderer?.ToggleCityBuildingActivationFromHost(k);
    public void ExecuteCityBuildingActionFromHost(string k) => _overlayRenderer?.ExecuteCityBuildingActionFromHost(k);
    public void GoToOtherCityFromHost(string k) => _overlayRenderer?.GoToOtherCityFromHost(k);
    public void SetHoveredCityBuildingFromHost(string? k, float x, float y) => _overlayRenderer?.SetHoveredCityBuildingFromHost(k, x, y);

    /// <summary>Instantané de l'onglet Journal pour une vue portée par l'hôte.</summary>
    public EventLogSnapshot GetEventLogSnapshot() =>
        _overlayRenderer?.GetEventLogSnapshot() ?? EventLogSnapshot.Hidden;

    /// <summary>Instantané de l'onglet Stats pour une vue portée par l'hôte.</summary>
    public StatsSnapshot GetStatsSnapshot() =>
        _overlayRenderer?.GetStatsSnapshot() ?? StatsSnapshot.Hidden;

    public void SetStatsSubTabFromHost(string key) => _overlayRenderer?.SetStatsSubTabFromHost(key);

    /// <summary>Instantané de l'onglet Rituels pour une vue portée par l'hôte.</summary>
    public RitualsSnapshot GetRitualsSnapshot() =>
        _overlayRenderer?.GetRitualsSnapshot() ?? RitualsSnapshot.Hidden;

    public void ToggleRitualFromHost(string key) => _overlayRenderer?.ToggleRitualFromHost(key);
    public void ChangeRitualPowerFromHost(string key, bool increase) => _overlayRenderer?.ChangeRitualPowerFromHost(key, increase);
    public void CastSpellFromHost(string key) => _overlayRenderer?.CastSpellFromHost(key);

    /// <summary>Instantané de l'onglet Automatisation pour une vue portée par l'hôte.</summary>
    public AutomationSnapshot GetAutomationSnapshot() =>
        _overlayRenderer?.GetAutomationSnapshot() ?? AutomationSnapshot.Hidden;

    public void ToggleAutomationFromHost(string key) => _overlayRenderer?.ToggleAutomationFromHost(key);
    public void ToggleAutomationPinFromHost(string key) => _overlayRenderer?.ToggleAutomationPinFromHost(key);
    public void ToggleAutomationsGloballyFromHost() => _overlayRenderer?.ToggleAutomationsGloballyFromHost();

    /// <summary>Instantané du menu de l'engrenage pour une vue portée par l'hôte.</summary>
    public SettingsMenuSnapshot GetSettingsMenuSnapshot() =>
        _overlayRenderer?.GetSettingsMenuSnapshot() ?? SettingsMenuSnapshot.Closed;

    public void InvokeSettingsMenuItemFromHost(string key) => _overlayRenderer?.InvokeSettingsMenuItemFromHost(key);
    public void CloseSettingsMenuFromHost() => _overlayRenderer?.CloseSettingsMenuFromHost();

    /// <summary>Instantané du popup de commerce pour une vue portée par l'hôte.</summary>
    public TradePopupSnapshot GetTradePopupSnapshot() =>
        _overlayRenderer?.GetTradePopupSnapshot() ?? TradePopupSnapshot.Closed;

    public void TradeSellFromHost(string key) => _overlayRenderer?.TradeSellFromHost(key);
    public void TradeBuyFromHost(string key) => _overlayRenderer?.TradeBuyFromHost(key);
    public void TradeSetMultiplierFromHost(int m) => _overlayRenderer?.TradeSetMultiplierFromHost(m);
    public void TradeSetHistoryTabFromHost(bool h) => _overlayRenderer?.TradeSetHistoryTabFromHost(h);
    public void CloseTradePopupFromHost() => _overlayRenderer?.CloseTradePopupFromHost();

    /// <summary>Instantané du popup de prestige pour une vue portée par l'hôte.</summary>
    public PrestigePopupSnapshot GetPrestigePopupSnapshot() =>
        _overlayRenderer?.GetPrestigePopupSnapshot() ?? PrestigePopupSnapshot.Closed;

    public void InvokePrestigeActionFromHost(string key) => _overlayRenderer?.InvokePrestigeActionFromHost(key);
    public void PrestigeSkipWonderTimeFromHost() => _overlayRenderer?.PrestigeSkipWonderTimeFromHost();
    public void PrestigeChangeTierFromHost(bool increase) => _overlayRenderer?.PrestigeChangeTierFromHost(increase);
    public void ClosePrestigePopupFromHost() => _overlayRenderer?.ClosePrestigePopupFromHost();

    /// <summary>Instantané du popup de réglages pour une vue portée par l'hôte.</summary>
    public SettingsPopupSnapshot GetSettingsPopupSnapshot() =>
        _overlayRenderer?.GetSettingsPopupSnapshot() ?? SettingsPopupSnapshot.Closed;

    public void ToggleSettingFromHost(string k) => _overlayRenderer?.ToggleSettingFromHost(k);
    public void SetSettingChoiceFromHost(string k, string c) => _overlayRenderer?.SetSettingChoiceFromHost(k, c);
    public void SetSettingSliderFromHost(string k, double v) => _overlayRenderer?.SetSettingSliderFromHost(k, v);
    public void SetSettingTextFromHost(string k, string v) => _overlayRenderer?.SetSettingTextFromHost(k, v);
    public void CloseSettingsPopupFromHost() => _overlayRenderer?.CloseSettingsPopupFromHost();

    /// <summary>Instantané du panneau civilisation pour une vue portée par l'hôte.</summary>
    public CivPanelSnapshot GetCivPanelSnapshot() =>
        _overlayRenderer?.GetCivPanelSnapshot() ?? CivPanelSnapshot.Hidden;

    public void ExecuteCivActionFromHost(string k) => _overlayRenderer?.ExecuteCivActionFromHost(k);
    public void ToggleCivPinnedFromHost(string k) => _overlayRenderer?.ToggleCivPinnedFromHost(k);
    public void SetCivPanelCollapsedFromHost(bool c) => _overlayRenderer?.SetCivPanelCollapsedFromHost(c);

    /// <summary>Instantané du panneau monument pour une vue portée par l'hôte.</summary>
    public MonumentPanelSnapshot GetMonumentPanelSnapshot() =>
        _overlayRenderer?.GetMonumentPanelSnapshot() ?? MonumentPanelSnapshot.Hidden;

    public void CloseMonumentPanelFromHost() => _overlayRenderer?.CloseMonumentPanelFromHost();
    public void ToggleMonumentInvestmentFromHost(string rowKey) =>
        _overlayRenderer?.ToggleMonumentInvestmentFromHost(rowKey);
    public void EvolveMonumentFromHost() => _overlayRenderer?.EvolveMonumentFromHost();
    public void SkipWonderFromHost() => _overlayRenderer?.SkipWonderFromHost();
    public void DestroyMonumentFromHost() => _overlayRenderer?.DestroyMonumentFromHost();

    /// <summary>Ouvre/ferme le menu paramètres depuis une icône portée par l'hôte.</summary>
    public void ToggleSettingsMenuFromHost() => _overlayRenderer?.ToggleSettingsMenuFromHost();

    /// <summary>Instantané de la barre de ressources pour une vue portée par l'hôte.</summary>
    public ResourceBarSnapshot GetResourceBarSnapshot() =>
        _overlayRenderer?.GetResourceBarSnapshot() ?? ResourceBarSnapshot.Unavailable;

    /// <summary>
    /// Signale que le pointeur est entré ou sorti du canevas. Les infobulles Skia ne
    /// s'affichent que dans le premier cas, ce qui les rend mutuellement exclusives avec
    /// celles d'Avalonia — qui n'existent, elles, que sur les contrôles de l'overlay.
    /// </summary>
    public void SetPointerOverMap(bool isOver) => _tooltipRenderer?.SetSuppressed(!isOver);

    /// <summary>
    /// Infobulle d'une pastille de ressource, désignée par son nom d'enum — celui que
    /// l'instantané expose en <c>IconName</c>, seul identifiant dont dispose la vue.
    /// </summary>
    public string? GetResourceTooltip(string resourceName) =>
        Enum.TryParse<Resource>(resourceName, out var resource)
            ? _overlayRenderer?.GetResourceTooltip(resource)
            : null;

    /// <summary>Instantané de l'état du temps pour un contrôle porté par l'hôte.</summary>
    public TimeControlSnapshot GetTimeControlSnapshot()
    {
        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return TimeControlSnapshot.Unavailable;

        return new TimeControlSnapshot(
            IsAvailable: true,
            IsPaused: clock.SpeedMultiplier == 0,
            ActiveSpeed: clock.ActiveSpeed,
            OfflineBankTicks: clock.OfflineBankTicks);
    }

    /// <summary>Instantané du saut de temps en cours, pour la popup de progression de l'hôte.</summary>
    public TimeJumpSnapshot GetTimeJumpSnapshot()
    {
        var jump = _gameControllerService.TimeJump;
        if (!jump.IsActive) return TimeJumpSnapshot.Inactive;

        double progress = jump.Progress;
        return new TimeJumpSnapshot(
            IsActive: true,
            Title: _localizationService.Get("time_jump_title"),
            Reason: _localizationService.Get(jump.ReasonKey),
            Progress: progress,
            PercentLabel: $"{(int)(progress * 100)} %");
    }

    /// <summary>Bascule pause/lecture, pour un contrôle de temps porté par l'hôte.</summary>
    public void ToggledPauseFromHost()
    {
        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;
        if (clock.SpeedMultiplier == 0) clock.Resume();
        else clock.Pause();
    }

    /// <summary>Change la vitesse de jeu, pour un contrôle de temps porté par l'hôte.</summary>
    public void SetGameSpeedFromHost(int multiplier) =>
        _gameControllerService.CurrentGameState?.Clock?.SetSpeed(multiplier);

    /// <summary>Zoom avant, pour un contrôle de zoom porté par l'hôte plutôt que par l'overlay Skia.</summary>
    public void ZoomIn() => _cameraService.SetZoom(_cameraService.ZoomLevel * ZoomStep);

    /// <summary>Zoom arrière, pour un contrôle de zoom porté par l'hôte plutôt que par l'overlay Skia.</summary>
    public void ZoomOut() => _cameraService.SetZoom(_cameraService.ZoomLevel / ZoomStep);

    /// <summary>Vrai quand la vue courante est une carte hex (île/inframonde/abysse).</summary>
    public bool IsMapViewActive => _overlayRenderer?.IsIslandTabActive ?? false;

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
        _tooltipRenderer?.Initialize(canvasSize);
        _lastCanvasSize      = canvasSize;
        _isCanvasInitialized = true;
    }

    public void Tick()
    {
        if (_isDisposed) return;

        var elapsed   = _tickStopwatch.Elapsed.TotalSeconds;
        _tickStopwatch.Restart();
        var deltaTime = (float)Math.Max(elapsed, 0f);

        // Saut de temps en cours : la simulation avance par tranches bornées en temps et rien
        // d'autre ne tourne. Le temps réel est volontairement ignoré pendant ce temps — l'horloge
        // plafonne de toute façon son rattrapage à 100 ms au retour — et la sauvegarde auto attend
        // la fin du saut plutôt que de sérialiser un état à mi-parcours.
        var timeJump = _gameControllerService.TimeJump;
        if (timeJump.IsActive)
        {
            if (_gameControllerService.CurrentGameState?.Clock is { } jumpClock) timeJump.Advance(jumpClock);
            else                                                                 timeJump.Cancel();
            return;
        }

        _gameControllerService.Update(deltaTime);
        DrainEventToasts();

        // Les toasts sont dessinés par l'hôte : leur Render ne tourne plus, c'est donc la boucle
        // de jeu qui doit les faire vieillir — sinon plus rien ne les ferait expirer.
        _notificationToastRenderer?.Advance(deltaTime);

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
            if (!_corruptSavePending && _gameControllerService.MainGameController.CurrentMainState is { } mainState)
            {
                var json = _gameControllerService.MainGameController.ExportMainState();
                _fileSystemService.SaveAuto(json);
                if (mainState.Settings.CloudSaveEnabled)
                    _storeController?.SaveCloudFile(CloudSaveFileName, json);
            }

            var statsJson = System.Text.Json.JsonSerializer.Serialize(_gameControllerService.MainGameController.LifetimeStats);
            _fileSystemService.SaveStats(statsJson);
        }
    }

    private static PlayerLifetimeStats ParseLifetimeStats(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new PlayerLifetimeStats();
        try { return System.Text.Json.JsonSerializer.Deserialize<PlayerLifetimeStats>(json) ?? new PlayerLifetimeStats(); }
        catch { return new PlayerLifetimeStats(); }
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

        // Les modales sont rendues par l'hôte ; ne reste ici que le panneau de debug.
        _debugPanelRenderer?.Render(canvas, _lastCanvasSize, _uiLayoutService.UiScale);
    }

    /// <summary>
    /// Seconde passe de rendu : les infobulles, posées par les renderers pendant
    /// <see cref="Render"/> et dessinées ici seulement.
    ///
    /// L'hôte l'appelle depuis un contrôle placé au-dessus de l'overlay Avalonia. Dessinée dans
    /// la passe principale, une infobulle atterrit sur le canevas de la carte — le premier enfant
    /// de l'arbre visuel — et disparaît derrière le panneau de ville ou la barre du haut dès
    /// qu'elle les déborde.
    /// </summary>
    public void RenderTooltips(SKCanvas canvas, SKSize canvasSize)
    {
        if (_isDisposed) return;

        var gameState = _gameControllerService.CurrentGameState;
        if (gameState == null) return;

        // Contexte minimal : cette passe ne fait que dessiner un état déjà calculé. Ni le temps
        // écoulé ni la caméra n'y interviennent — seule l'échelle UI compte pour les polices.
        var context = new GameRenderContext
        {
            GameState  = gameState,
            DeltaTime  = 0f,
            CanvasSize = canvasSize,
            UiScale    = _uiLayoutService.UiScale,
        };

        _overlayRenderer?.RenderTooltipLayer(canvas, context);
        _tooltipRenderer?.Render(canvas, context);
    }

    public void HandlePointerPressed(float x, float y, int pointerId, PointerButton button)
    {
        // Une modale ouverte est bloquante : elle est dessinée par l'hôte, qui intercepte déjà
        // les clics, mais la garde reste ici — c'est le renderer qui détient l'état d'ouverture.
        if (_hardResetPopup?.IsOpen    == true) return;
        if (_restartIslandPopup?.IsOpen == true) return;
        if (_corruptSavePopup?.IsOpen  == true) return;
        if (_gameOverPopup?.IsOpen     == true) return;
        if (_demoEndPopup?.IsOpen      == true) return;
        if (_introRenderer?.IsActive   == true) return;

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
        if (_restartIslandPopup?.IsOpen == true) return;
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
        if (_restartIslandPopup?.IsOpen == true) return;
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

        // Comme pour la molette, l'overlay reçoit le geste dans tous les cas : c'est lui qui
        // zoome les écrans qui ne sont pas la carte (recherche, carte de prestige).
        _inputService.HandlePinch(scaleRatio, x, y, panDeltaX, panDeltaY);
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

        bool prevHexCoords  = DebugSettings.ShowHexCoords;
        bool prevAutoplayer = DebugSettings.ShowAutoplayerCommands;
        bool prevFullMap    = DebugSettings.ShowFullMap;
        DebugSettings.ShowHexCoords            = false;
        DebugSettings.ShowAutoplayerCommands   = false;
        DebugSettings.ShowFullMap              = false;
        try
        {
            using (surface)
            {
                _renderService.RenderFrame(surface.Canvas, gameState, _cameraService);
                // Les infobulles ne font plus partie de la passe principale : sans cet appel la
                // capture perdrait celle qui est affichée au moment du raccourci.
                RenderTooltips(surface.Canvas, canvasSize);
                SaveExportPng(surface, "screenshot_interface.png");
            }
        }
        finally
        {
            DebugSettings.ShowHexCoords            = prevHexCoords;
            DebugSettings.ShowAutoplayerCommands   = prevAutoplayer;
            DebugSettings.ShowFullMap              = prevFullMap;
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
        => TitleCardRenderer.Draw(canvas, canvasSize, s);

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
        string resolvedFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{image.Width}x{image.Height}{Path.GetExtension(fileName)}";
        using var stream = File.Create(Path.Combine(outputDir, resolvedFileName));
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
            GameEventType.GreatLighthouseLevelUp => (
                _localizationService.Get("event_great_lighthouse_levelup_title"),
                _localizationService.GetFormated("event_great_lighthouse_levelup_body", entry.Message ?? "?"),
                NotificationIcon.Achievement),
            GameEventType.ObservatoryLevelUp => (
                _localizationService.Get("event_observatory_levelup_title"),
                _localizationService.GetFormated("event_observatory_levelup_body", entry.Message ?? "?"),
                NotificationIcon.Achievement),
            GameEventType.NecropolisLevelUp => (
                _localizationService.Get("event_necropolis_levelup_title"),
                _localizationService.GetFormated("event_necropolis_levelup_body", entry.Message ?? "?"),
                NotificationIcon.Achievement),
            GameEventType.CivilizationDiscovered => (
                _localizationService.Get("event_civilization_discovered_title"),
                _localizationService.Get("event_civilization_discovered_body"),
                NotificationIcon.Info),
            GameEventType.CivilizationDestroyed => (
                _localizationService.Get("event_civilization_destroyed_title"),
                _localizationService.Get("event_civilization_destroyed_body"),
                NotificationIcon.Achievement),
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
            GameEventType.MajorDemonDiscovered => (
                _localizationService.Get("event_major_demon_discovered_title"),
                _localizationService.Get("event_major_demon_discovered_body"),
                NotificationIcon.StoreFail),
            GameEventType.VolcanoDiscovered => (
                _localizationService.Get("event_volcano_discovered_title"),
                _localizationService.Get("event_volcano_discovered_body"),
                NotificationIcon.StoreFail),
            GameEventType.RaidMissingBarracks => (
                _localizationService.Get("event_raid_missing_barracks_title"),
                _localizationService.Get("event_raid_missing_barracks_body"),
                NotificationIcon.StoreFail),
            GameEventType.WarHeraldAutoReinforcementConflict => (
                _localizationService.Get("event_war_herald_auto_reinforcement_conflict_title"),
                _localizationService.Get("event_war_herald_auto_reinforcement_conflict_body"),
                NotificationIcon.StoreFail),
            GameEventType.CorruptionSpireBuilt => (
                _localizationService.Get("event_corruption_spire_built_title"),
                _localizationService.Get("event_corruption_spire_built_body"),
                NotificationIcon.Achievement),
            GameEventType.CorruptionSpireRadiusUpgraded => (
                _localizationService.Get("event_corruption_spire_radius_upgraded_title"),
                _localizationService.GetFormated("event_corruption_spire_radius_upgraded_body", entry.Message ?? "?"),
                NotificationIcon.Achievement),
            GameEventType.AbyssGateEligible => (
                _localizationService.Get("event_abyss_gate_eligible_title"),
                _localizationService.Get("event_abyss_gate_eligible_body"),
                NotificationIcon.Achievement),
            GameEventType.AbyssGatePlaced => (
                _localizationService.Get("event_abyss_gate_placed_title"),
                _localizationService.Get("event_abyss_gate_placed_body"),
                NotificationIcon.Info),
            GameEventType.AbyssGateBuilt => (
                _localizationService.Get("event_abyss_gate_built_title"),
                _localizationService.Get("event_abyss_gate_built_body"),
                NotificationIcon.Achievement),
            GameEventType.TentacleDiscovered => (
                _localizationService.Get("event_tentacle_discovered_title"),
                _localizationService.Get("event_tentacle_discovered_body"),
                NotificationIcon.StoreFail),
            GameEventType.DemonGodDiscovered => (
                _localizationService.Get("event_demon_god_discovered_title"),
                _localizationService.Get("event_demon_god_discovered_body"),
                NotificationIcon.StoreFail),
            GameEventType.PandemoniumGatePlaced => (
                _localizationService.Get("event_pandemonium_gate_placed_title"),
                _localizationService.Get("event_pandemonium_gate_placed_body"),
                NotificationIcon.Achievement),
            GameEventType.PandemoniumGateBuilt => (
                _localizationService.Get("event_pandemonium_gate_built_title"),
                _localizationService.Get("event_pandemonium_gate_built_body"),
                NotificationIcon.Achievement),
            GameEventType.DivineBonesPurified => (
                _localizationService.Get("event_divine_bones_purified_title"),
                _localizationService.Get("event_divine_bones_purified_body"),
                NotificationIcon.Achievement),
            GameEventType.DivineBonesPurifiedNoEssence => (
                _localizationService.Get("event_divine_bones_purified_no_essence_title"),
                _localizationService.Get("event_divine_bones_purified_no_essence_body"),
                NotificationIcon.Info),
            GameEventType.AbyssLostDivineEssence => (
                _localizationService.Get("event_abyss_lost_divine_essence_title"),
                _localizationService.GetFormated("event_abyss_lost_divine_essence_body", entry.Message ?? "?"),
                NotificationIcon.StoreFail),
            GameEventType.AbyssGateLost => (
                _localizationService.Get("event_abyss_gate_lost_title"),
                _localizationService.Get("event_abyss_gate_lost_body"),
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

    /// <summary>
    /// Exécute une mutation du modèle sous le verrou de l'hôte (cf.
    /// <see cref="Services.SkiaGameRuntime.SetStateSynchronizer"/>). Le verrou étant réentrant,
    /// l'appel reste inoffensif quand on s'y trouve déjà.
    /// </summary>
    private void RunSynchronized(Action action)
    {
        if (_runSynchronized != null) _runSynchronized(action);
        else action();
    }

    /// <summary>
    /// Charge une sauvegarde en cours de partie (menu « Charger »). Le MainGameState entier est
    /// remplacé : villes, civilisations et WorldState de l'écran courant deviennent des objets
    /// orphelins. On refait donc exactement le ménage des autres changements de monde
    /// (voir <see cref="CompletePrestigeTransition"/> et <see cref="HandleGameOverRestart"/>) —
    /// la sélection de ville, la structure investissable sélectionnée, les caches de
    /// constructibles et l'abonnement au stock bas pointent tous sur la partie précédente.
    /// </summary>
    private void HandleLoadGame(string saveJson)
    {
        var prevCiv = _gameControllerService.PlayerCivilization;
        _gameControllerService.ImportMainState(saveJson);

        // La couche affichée au moment de la sauvegarde (Inframonde/Abysse) n'a plus de sens une
        // fois la partie rechargée : on revient systématiquement sur la Surface, bouton d'onglet
        // surligné inclus, plutôt que de laisser l'overlay désynchronisé de la couche restaurée.
        _overlayRenderer?.SetActiveTabFromHost(TabBarRenderer.TabIsland);

        if (_playerResourcesOverlayRenderer != null && _gameControllerService.PlayerCivilization != null)
            _playerResourcesOverlayRenderer.ConnectLowStock(prevCiv, _gameControllerService.PlayerCivilization);
        _gameControllerService.CityBuildingService.ClearSelectedCity();
        _monumentService?.ClearSelectedInvestable();
        _constructionInteractionService?.ClearHover();

        if (_gameControllerService.CurrentGameState is SettlersOfIdlestan.Model.Game.MainGameState loadedState)
            _tutorialService?.InitializeForLoadedGame(loadedState);

        CenterCameraOnStartingCity();
        ResetPointerState();

        // La partie chargée peut déjà être perdue (plus aucune ville), comme au démarrage.
        var playerCiv = _gameControllerService.PlayerCivilization;
        _gameOverPending = playerCiv != null && playerCiv.Cities.Count == 0;
    }

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
        var clock = _gameControllerService.CurrentGameState?.Clock;
        _speedBeforeTargetSelection = clock?.SpeedMultiplier ?? 1;
        clock?.Pause();
        _overlayRenderer?.EnsureMapTabActiveForTargetSelection();
        _overlayRenderer?.Hide();
    }

    private void OnTargetSelectionConfirmed(object? sender, EventArgs e)
    {
        RestoreSpeedAfterTargetSelection();
        _overlayRenderer?.Show(suppressNextPress: true);
    }

    private void OnTargetSelectionCancelled(object? sender, EventArgs e)
    {
        RestoreSpeedAfterTargetSelection();
        _overlayRenderer?.Show(suppressNextPress: true);
    }

    private void RestoreSpeedAfterTargetSelection()
    {
        var clock = _gameControllerService.CurrentGameState?.Clock;
        if (clock == null) return;
        if (_speedBeforeTargetSelection > 0) clock.SetSpeed(_speedBeforeTargetSelection);
        else clock.Pause();
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
        _restartIslandPopup?.Dispose();
        _demoEndPopup?.Dispose();
        _constructionInteractionService?.Cleanup();
        _militaryInteractionService?.Cleanup();
        _renderService.Dispose();
        // Hors RenderService depuis qu'il est rendu dans sa propre passe : personne d'autre ne
        // le libere.
        _tooltipRenderer?.Dispose();
        _isDisposed = true;
    }
}
