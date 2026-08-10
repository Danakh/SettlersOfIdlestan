using SkiaSharp;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Screens;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Point d'entrée public de la couche Skia. Coordonne le TitleScreen et le GameScreen.
/// </summary>
public sealed class SkiaGameRuntime : IDisposable
{
    private ResourceManager?      _resourceManager;
    private LocalizationService?  _localizationService;
    private UILayoutService?      _uiLayoutService;
    private IFileSystemService?   _fileSystemService;
    private StoreController?      _storeController;
    private bool                  _allowDebugMode;
    private bool                  _demoMode;

    private TitleScreen?  _titleScreen;
    private GameScreen?   _gameScreen;
    private bool          _onTitleScreen;
    private GameSettings  _titleSettings = new();
    private string?       _statsJson;

    private SKSize _lastCanvasSize;
    private bool   _isDisposed;
    private bool   _isInitialized;

    public event Action? QuitRequested;
    public event Action<string>? DiscordLinkClicked;
    public event Action<bool>? FullscreenStateChanged;
    public event Action<int, int>? DebugWindowResizeRequested;

    public bool IsFullscreenEnabled => _titleSettings.Fullscreen;

    public async Task SyncFullscreenSetting(bool fullscreen)
    {
        _titleSettings.Fullscreen = fullscreen;

        if (!_onTitleScreen && _gameScreen != null)
        {
            var gameSettings = _gameScreen.GetCurrentSettings();
            if (gameSettings != null)
            {
                gameSettings.Fullscreen = fullscreen;
                await _fileSystemService!.SaveSettings(System.Text.Json.JsonSerializer.Serialize(gameSettings));
                return;
            }
        }

        await _fileSystemService!.SaveSettings(System.Text.Json.JsonSerializer.Serialize(_titleSettings));
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public void Initialize(IFileSystemService fileSystemService, bool allowDebugMode = false, bool demoMode = false, StoreController? storeController = null)
    {
        var autoJson     = fileSystemService.LoadAuto().GetAwaiter().GetResult();
        var settingsJson = fileSystemService.LoadSettings().GetAwaiter().GetResult();
        var statsJson    = fileSystemService.LoadStats().GetAwaiter().GetResult();
        InitializeCore(fileSystemService, autoJson, settingsJson, statsJson, allowDebugMode, demoMode, storeController);
    }

    public async Task InitializeAsync(IFileSystemService fileSystemService, bool allowDebugMode = false, bool demoMode = false, StoreController? storeController = null)
    {
        var autoJson     = await fileSystemService.LoadAuto();
        var settingsJson = await fileSystemService.LoadSettings();
        var statsJson    = await fileSystemService.LoadStats();
        InitializeCore(fileSystemService, autoJson, settingsJson, statsJson, allowDebugMode, demoMode, storeController);
    }

    private void InitializeCore(IFileSystemService fileSystemService, string? autoJson, string? settingsJson, string? statsJson, bool allowDebugMode, bool demoMode = false, StoreController? storeController = null)
    {
        if (_isDisposed)    throw new ObjectDisposedException(nameof(SkiaGameRuntime));
        if (_isInitialized) return;

        _fileSystemService   = fileSystemService;
        _statsJson           = statsJson;
        _storeController     = storeController;
        _allowDebugMode      = allowDebugMode;
        _demoMode            = demoMode;
        _resourceManager     = new ResourceManager();
        _localizationService = new LocalizationService();
        _uiLayoutService     = new UILayoutService();

        _titleSettings = ParseSettings(settingsJson) ?? ExtractSettings(autoJson);

        // Pas de settings sauvegardés → demander la langue préférée au store
        if (settingsJson == null && _storeController != null)
        {
            var storeLang = _storeController.GetPreferredLanguage();
            if (storeLang.HasValue)
                _titleSettings.Language = storeLang.Value;
        }

        if (_demoMode) _titleSettings.DemoMode = true;
        _localizationService.SetLanguage(_titleSettings.Language);
        SkiaTextUtils.NumberFormat = _titleSettings.NumberFormat;

        bool hasSave = !string.IsNullOrEmpty(autoJson);
        ShowTitleScreen(hasSave);

        _isInitialized = true;
    }

    // ── Navigation entre écrans ───────────────────────────────────────────────

    private void ShowTitleScreen(bool hasSave)
    {
        _titleScreen?.Dispose();
        _titleScreen = new TitleScreen(_fileSystemService!, _localizationService!, _uiLayoutService!, _resourceManager!, hasSave, _titleSettings, _allowDebugMode, _storeController);
        _titleScreen.NewGameRequested          += OnNewGameRequested;
        _titleScreen.ContinueRequested         += OnContinueRequested;
        _titleScreen.DiscordLinkClicked        += url => DiscordLinkClicked?.Invoke(url);
        _titleScreen.FullscreenToggleRequested += v => FullscreenStateChanged?.Invoke(v);
        _titleScreen.DebugWindowResizeRequested += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);

        _onTitleScreen = true;
    }

    private void OnNewGameRequested()
    {
        _titleScreen?.Dispose();
        _titleScreen   = null;
        _onTitleScreen = false;

        _gameScreen = new GameScreen(
            _fileSystemService!,
            _localizationService!,
            _uiLayoutService!,
            _resourceManager!,
            saveJson: null,
            _allowDebugMode,
            _demoMode,
            _storeController,
            statsJson: _statsJson);
        _gameScreen.ReturnToTitleRequested     += OnReturnToTitle;
        _gameScreen.QuitRequested              += () => QuitRequested?.Invoke();
        _gameScreen.FullscreenToggleRequested  += v => FullscreenStateChanged?.Invoke(v);
        _gameScreen.DebugWindowResizeRequested += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);

        if (_isCanvasInitialized)
            _gameScreen.EnsureCanvasInitialized(_lastCanvasSize);
    }

    // async void : handler d'événement ; le blocage synchrone (GetResult) est
    // interdit sur le runtime WebAssembly mono-thread.
    private async void OnContinueRequested()
    {
        _titleScreen?.Dispose();
        _titleScreen   = null;
        _onTitleScreen = false;

        var saveJson = await _fileSystemService!.LoadAuto();

        _gameScreen = new GameScreen(
            _fileSystemService!,
            _localizationService!,
            _uiLayoutService!,
            _resourceManager!,
            saveJson,
            _allowDebugMode,
            _demoMode,
            _storeController,
            statsJson: _statsJson);
        _gameScreen.ReturnToTitleRequested     += OnReturnToTitle;
        _gameScreen.QuitRequested              += () => QuitRequested?.Invoke();
        _gameScreen.FullscreenToggleRequested  += v => FullscreenStateChanged?.Invoke(v);
        _gameScreen.DebugWindowResizeRequested += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);

        if (_isCanvasInitialized)
            _gameScreen.EnsureCanvasInitialized(_lastCanvasSize);
    }

    private async void OnReturnToTitle()
    {
        _gameScreen?.Dispose();
        _gameScreen = null;

        var autoJson     = await _fileSystemService!.LoadAuto();
        var settingsJson = await _fileSystemService.LoadSettings();
        _statsJson       = await _fileSystemService.LoadStats();
        _titleSettings   = ParseSettings(settingsJson) ?? ExtractSettings(autoJson);
        _localizationService!.SetLanguage(_titleSettings.Language);
        SkiaTextUtils.NumberFormat = _titleSettings.NumberFormat;
        ShowTitleScreen(autoJson != null);
    }

    private static GameSettings? ParseSettings(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<GameSettings>(json); }
        catch { return null; }
    }

    private static GameSettings ExtractSettings(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new GameSettings();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("Settings", out var prop))
            {
                var s = System.Text.Json.JsonSerializer.Deserialize<GameSettings>(prop);
                if (s != null) return s;
            }
        }
        catch { }
        return new GameSettings();
    }

    // ── API publique ─────────────────────────────────────────────────────────

    private bool _isCanvasInitialized;

    /// <summary>Vrai quand une partie est en cours et affiche une carte hex (pas l'écran titre ni un onglet plein écran).</summary>
    public bool IsMapViewActive => !_onTitleScreen && (_gameScreen?.IsMapViewActive ?? false);

    public void ZoomIn()  => _gameScreen?.ZoomIn();
    public void ZoomOut() => _gameScreen?.ZoomOut();

    /// <summary>Instantané de la barre d'onglets pour une vue portée par l'hôte.</summary>
    public TabBarSnapshot GetTabBarSnapshot() =>
        _onTitleScreen ? TabBarSnapshot.Unavailable
                       : _gameScreen?.GetTabBarSnapshot() ?? TabBarSnapshot.Unavailable;

    public void SetActiveTab(int tabId) => _gameScreen?.SetActiveTabFromHost(tabId);

    /// <summary>Instantané du panneau ville pour une vue portée par l'hôte.</summary>
    public CityPanelSnapshot GetCityPanelSnapshot() =>
        _onTitleScreen ? CityPanelSnapshot.Hidden
                       : _gameScreen?.GetCityPanelSnapshot() ?? CityPanelSnapshot.Hidden;

    public void CloseCityPanel() => _gameScreen?.CloseCityPanelFromHost();
    public void SetCityShowUnique(bool v) => _gameScreen?.SetCityShowUniqueFromHost(v);
    public void ToggleCityBuildingActivation(string k) => _gameScreen?.ToggleCityBuildingActivationFromHost(k);
    public void ExecuteCityBuildingAction(string k) => _gameScreen?.ExecuteCityBuildingActionFromHost(k);
    public void GoToOtherCity(string k) => _gameScreen?.GoToOtherCityFromHost(k);
    public void SetHoveredCityBuilding(string? k, float x, float y) => _gameScreen?.SetHoveredCityBuildingFromHost(k, x, y);

    /// <summary>Instantané de l'onglet Journal pour une vue portée par l'hôte.</summary>
    public EventLogSnapshot GetEventLogSnapshot() =>
        _onTitleScreen ? EventLogSnapshot.Hidden
                       : _gameScreen?.GetEventLogSnapshot() ?? EventLogSnapshot.Hidden;

    /// <summary>Instantané de l'onglet Stats pour une vue portée par l'hôte.</summary>
    public StatsSnapshot GetStatsSnapshot() =>
        _onTitleScreen ? StatsSnapshot.Hidden
                       : _gameScreen?.GetStatsSnapshot() ?? StatsSnapshot.Hidden;

    public void SetStatsSubTab(string key) => _gameScreen?.SetStatsSubTabFromHost(key);

    /// <summary>Instantané de l'onglet Rituels pour une vue portée par l'hôte.</summary>
    public RitualsSnapshot GetRitualsSnapshot() =>
        _onTitleScreen ? RitualsSnapshot.Hidden
                       : _gameScreen?.GetRitualsSnapshot() ?? RitualsSnapshot.Hidden;

    public void ToggleRitual(string key) => _gameScreen?.ToggleRitualFromHost(key);
    public void ChangeRitualPower(string key, bool increase) => _gameScreen?.ChangeRitualPowerFromHost(key, increase);
    public void CastSpell(string key) => _gameScreen?.CastSpellFromHost(key);

    /// <summary>Instantané de l'onglet Automatisation pour une vue portée par l'hôte.</summary>
    public AutomationSnapshot GetAutomationSnapshot() =>
        _onTitleScreen ? AutomationSnapshot.Hidden
                       : _gameScreen?.GetAutomationSnapshot() ?? AutomationSnapshot.Hidden;

    public void ToggleAutomation(string key) => _gameScreen?.ToggleAutomationFromHost(key);
    public void ToggleAutomationPin(string key) => _gameScreen?.ToggleAutomationPinFromHost(key);
    public void ToggleAutomationsGlobally() => _gameScreen?.ToggleAutomationsGloballyFromHost();

    /// <summary>Instantané du menu de l'engrenage pour une vue portée par l'hôte.</summary>
    public SettingsMenuSnapshot GetSettingsMenuSnapshot() =>
        _onTitleScreen ? SettingsMenuSnapshot.Closed
                       : _gameScreen?.GetSettingsMenuSnapshot() ?? SettingsMenuSnapshot.Closed;

    public void InvokeSettingsMenuItem(string key) => _gameScreen?.InvokeSettingsMenuItemFromHost(key);
    public void CloseSettingsMenu() => _gameScreen?.CloseSettingsMenuFromHost();

    /// <summary>Instantané du popup de commerce pour une vue portée par l'hôte.</summary>
    public TradePopupSnapshot GetTradePopupSnapshot() =>
        _onTitleScreen ? TradePopupSnapshot.Closed
                       : _gameScreen?.GetTradePopupSnapshot() ?? TradePopupSnapshot.Closed;

    public void TradeSell(string key) => _gameScreen?.TradeSellFromHost(key);
    public void TradeBuy(string key) => _gameScreen?.TradeBuyFromHost(key);
    public void TradeSetMultiplier(int m) => _gameScreen?.TradeSetMultiplierFromHost(m);
    public void TradeSetHistoryTab(bool h) => _gameScreen?.TradeSetHistoryTabFromHost(h);
    public void CloseTradePopup() => _gameScreen?.CloseTradePopupFromHost();

    /// <summary>Instantané du popup de prestige pour une vue portée par l'hôte.</summary>
    public PrestigePopupSnapshot GetPrestigePopupSnapshot() =>
        _onTitleScreen ? PrestigePopupSnapshot.Closed
                       : _gameScreen?.GetPrestigePopupSnapshot() ?? PrestigePopupSnapshot.Closed;

    public void InvokePrestigeAction(string key) => _gameScreen?.InvokePrestigeActionFromHost(key);
    public void PrestigeSkipWonderTime() => _gameScreen?.PrestigeSkipWonderTimeFromHost();
    public void PrestigeChangeTier(bool increase) => _gameScreen?.PrestigeChangeTierFromHost(increase);
    public void ClosePrestigePopup() => _gameScreen?.ClosePrestigePopupFromHost();

    /// <summary>Instantané du popup de réglages pour une vue portée par l'hôte.</summary>
    public SettingsPopupSnapshot GetSettingsPopupSnapshot() =>
        _onTitleScreen ? SettingsPopupSnapshot.Closed
                       : _gameScreen?.GetSettingsPopupSnapshot() ?? SettingsPopupSnapshot.Closed;

    public void ToggleSetting(string k) => _gameScreen?.ToggleSettingFromHost(k);
    public void SetSettingChoice(string k, string c) => _gameScreen?.SetSettingChoiceFromHost(k, c);
    public void SetSettingSlider(string k, double v) => _gameScreen?.SetSettingSliderFromHost(k, v);
    public void SetSettingText(string k, string v) => _gameScreen?.SetSettingTextFromHost(k, v);
    public void CloseSettingsPopup() => _gameScreen?.CloseSettingsPopupFromHost();

    // ── Ecran-titre ───────────────────────────────────────────────────────────
    //
    // Seul bloc d'instantanés gaté à l'inverse des autres : il ne répond que **sur** l'écran-titre.

    public TitleScreenSnapshot GetTitleScreenSnapshot() =>
        _onTitleScreen ? _titleScreen?.GetSnapshot() ?? TitleScreenSnapshot.Hidden
                       : TitleScreenSnapshot.Hidden;

    public void SetTitleTab(string key) { if (_onTitleScreen) _titleScreen?.SetTabFromHost(key); }
    public void InvokeTitleAction(string key) { if (_onTitleScreen) _titleScreen?.InvokeActionFromHost(key); }
    public void SetTitleSettingToggle(string k) { if (_onTitleScreen) _titleScreen?.ToggleSettingFromHost(k); }
    public void SetTitleSettingChoice(string k, string c) { if (_onTitleScreen) _titleScreen?.SetSettingChoiceFromHost(k, c); }
    public void SetTitleSettingSlider(string k, double v) { if (_onTitleScreen) _titleScreen?.SetSettingSliderFromHost(k, v); }
    public void SetTitleSettingText(string k, string v) { if (_onTitleScreen) _titleScreen?.SetSettingTextFromHost(k, v); }

    /// <summary>Instantané des toasts pour une vue portée par l'hôte.</summary>
    public ToastListSnapshot GetToastSnapshot() =>
        // L'écran-titre a ses propres toasts (connexion au store).
        _onTitleScreen ? _titleScreen?.GetToastSnapshot() ?? ToastListSnapshot.Empty
                       : _gameScreen?.GetToastSnapshot() ?? ToastListSnapshot.Empty;

    public void DismissToast(long id)
    {
        if (_onTitleScreen) _titleScreen?.DismissToastFromHost(id);
        else _gameScreen?.DismissToastFromHost(id);
    }

    /// <summary>Instantané de la modale bloquante ouverte, pour une vue portée par l'hôte.</summary>
    public ModalPopupSnapshot GetModalPopupSnapshot() =>
        // L'écran-titre a sa propre confirmation de remise à zéro, de même forme : même vue.
        _onTitleScreen ? _titleScreen?.GetModalSnapshot() ?? ModalPopupSnapshot.None
                       : _gameScreen?.GetModalPopupSnapshot() ?? ModalPopupSnapshot.None;

    public void InvokeModalPopupButton(string popupId, string buttonKey)
    {
        if (_onTitleScreen) _titleScreen?.InvokeModalButtonFromHost(buttonKey);
        else _gameScreen?.InvokeModalPopupButtonFromHost(popupId, buttonKey);
    }

    /// <summary>Instantané du panneau civilisation pour une vue portée par l'hôte.</summary>
    public CivPanelSnapshot GetCivPanelSnapshot() =>
        _onTitleScreen ? CivPanelSnapshot.Hidden
                       : _gameScreen?.GetCivPanelSnapshot() ?? CivPanelSnapshot.Hidden;

    public void ExecuteCivAction(string k) => _gameScreen?.ExecuteCivActionFromHost(k);
    public void ToggleCivPinned(string k) => _gameScreen?.ToggleCivPinnedFromHost(k);
    public void SetCivPanelCollapsed(bool c) => _gameScreen?.SetCivPanelCollapsedFromHost(c);

    /// <summary>Instantané du panneau monument pour une vue portée par l'hôte.</summary>
    public MonumentPanelSnapshot GetMonumentPanelSnapshot() =>
        _onTitleScreen ? MonumentPanelSnapshot.Hidden
                       : _gameScreen?.GetMonumentPanelSnapshot() ?? MonumentPanelSnapshot.Hidden;

    public void CloseMonumentPanel() => _gameScreen?.CloseMonumentPanelFromHost();
    public void ToggleMonumentInvestment(string rowKey) => _gameScreen?.ToggleMonumentInvestmentFromHost(rowKey);
    public void EvolveMonument() => _gameScreen?.EvolveMonumentFromHost();
    public void SkipWonder() => _gameScreen?.SkipWonderFromHost();

    /// <summary>Ouvre/ferme le menu paramètres depuis une icône portée par l'hôte.</summary>
    public void ToggleSettingsMenu() => _gameScreen?.ToggleSettingsMenuFromHost();

    /// <summary>Instantané de la barre de ressources pour une vue portée par l'hôte.</summary>
    public ResourceBarSnapshot GetResourceBarSnapshot() =>
        _onTitleScreen ? ResourceBarSnapshot.Unavailable
                       : _gameScreen?.GetResourceBarSnapshot() ?? ResourceBarSnapshot.Unavailable;

    /// <summary>Infobulle d'une pastille de la barre de ressources portée par l'hôte.</summary>
    public string? GetResourceTooltip(string resourceName) =>
        _onTitleScreen ? null : _gameScreen?.GetResourceTooltip(resourceName);

    /// <summary>Instantané de l'état du temps pour un contrôle de temps porté par l'hôte.</summary>
    public TimeControlSnapshot GetTimeControlSnapshot() =>
        _onTitleScreen ? TimeControlSnapshot.Unavailable
                       : _gameScreen?.GetTimeControlSnapshot() ?? TimeControlSnapshot.Unavailable;

    /// <summary>Traduction pour les contrôles d'overlay portés par l'hôte.</summary>
    public string Localize(string key) => _localizationService?.Get(key) ?? key;

    /// <summary>Traduction formatée pour les contrôles d'overlay portés par l'hôte.</summary>
    public string LocalizeFormat(string key, params object[] args) =>
        _localizationService?.GetFormated(key, args) ?? key;

    public void TogglePause() => _gameScreen?.ToggledPauseFromHost();

    public void SetGameSpeed(int multiplier) => _gameScreen?.SetGameSpeedFromHost(multiplier);

    /// <summary>Définit l'échelle UI automatique détectée par la plateforme hôte (densité d'écran, grande résolution…).</summary>
    public void SetUiScale(float scale)
    {
        if (_uiLayoutService != null) _uiLayoutService.AutoUiScale = scale;
        _gameScreen?.SetUiScale(scale);
    }

    public void EnsureCanvasInitialized(SKSize canvasSize)
    {
        if (_isDisposed)    throw new ObjectDisposedException(nameof(SkiaGameRuntime));
        if (!_isInitialized) throw new InvalidOperationException($"{nameof(SkiaGameRuntime)} n'est pas initialisé.");

        _lastCanvasSize      = canvasSize;
        _isCanvasInitialized = true;

        _gameScreen?.EnsureCanvasInitialized(canvasSize);
        _titleScreen?.SetCanvasSize(canvasSize);
    }

    public void Tick()
    {
        if (_isDisposed || !_isInitialized) return;
        _gameScreen?.Tick();

        // L ecran-titre n a pas de boucle de jeu : ses toasts vieillissent ici une fois qu il ne
        // se dessine plus lui-meme.
        if (_onTitleScreen) _titleScreen?.AdvanceToasts();
    }

    /// <summary>
    /// L'écran-titre n'a rien à dessiner : il est entièrement fait de contrôles Avalonia, et
    /// le canevas reste noir derrière eux.
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        if (_isDisposed || !_isInitialized) return;
        if (!_onTitleScreen) _gameScreen?.Render(canvas);
    }

    // L'input ne concerne plus que la partie : l'écran-titre reçoit le sien par l'arbre visuel
    // d'Avalonia, sans passer par ici.

    public void HandlePointerPressed(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        if (!_onTitleScreen) _gameScreen?.HandlePointerPressed(x, y, pointerId, button);
    }

    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        if (!_onTitleScreen) _gameScreen?.HandlePointerMoved(x, y, pointerId);
    }

    public void HandlePointerReleased(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        if (!_onTitleScreen) _gameScreen?.HandlePointerReleased(x, y, pointerId, button);
    }

    public void HandleZoom(float wheelDelta, float x, float y)
    {
        if (!_onTitleScreen) _gameScreen?.HandleZoom(wheelDelta, x, y);
    }

    public void HandlePinch(float scaleRatio, float x, float y, float panDeltaX = 0f, float panDeltaY = 0f)
    {
        if (!_onTitleScreen) _gameScreen?.HandlePinch(scaleRatio, x, y, panDeltaX, panDeltaY);
    }

    public void HandleKeyReleased(string key)
    {
        if (!_onTitleScreen) _gameScreen?.HandleKeyReleased(key);
    }

    public void HandleKeyPressed(string key)
    {
        if (!_onTitleScreen) _gameScreen?.HandleKeyPressed(key, _allowDebugMode);
    }

    public void NotifyPageVisible(double hiddenSeconds)
    {
        if (!_onTitleScreen) _gameScreen?.NotifyPageVisible(hiddenSeconds);
    }

    public void NotifyError(Exception ex)
    {
        if (!_onTitleScreen) _gameScreen?.NotifyError(ex);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_isDisposed) return;
        _titleScreen?.Dispose();
        _gameScreen?.Dispose();
        _resourceManager?.Dispose();
        _titleScreen     = null;
        _gameScreen      = null;
        _resourceManager = null;
        _isDisposed      = true;
    }
}
