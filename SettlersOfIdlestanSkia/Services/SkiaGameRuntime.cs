using SkiaSharp;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Core;
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
        InitializeCore(fileSystemService, autoJson, settingsJson, allowDebugMode, demoMode, storeController);
    }

    public async Task InitializeAsync(IFileSystemService fileSystemService, bool allowDebugMode = false, bool demoMode = false, StoreController? storeController = null)
    {
        var autoJson     = await fileSystemService.LoadAuto();
        var settingsJson = await fileSystemService.LoadSettings();
        InitializeCore(fileSystemService, autoJson, settingsJson, allowDebugMode, demoMode, storeController);
    }

    private void InitializeCore(IFileSystemService fileSystemService, string? autoJson, string? settingsJson, bool allowDebugMode, bool demoMode = false, StoreController? storeController = null)
    {
        if (_isDisposed)    throw new ObjectDisposedException(nameof(SkiaGameRuntime));
        if (_isInitialized) return;

        _fileSystemService   = fileSystemService;
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

        bool hasSave = !string.IsNullOrEmpty(autoJson);
        ShowTitleScreen(hasSave);

        _isInitialized = true;
    }

    // ── Navigation entre écrans ───────────────────────────────────────────────

    private void ShowTitleScreen(bool hasSave)
    {
        _titleScreen?.Dispose();
        _titleScreen = new TitleScreen(_fileSystemService!, _localizationService!, _uiLayoutService!, _resourceManager!, hasSave, _titleSettings, _allowDebugMode);
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
            _storeController);
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
            _storeController);
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
        _titleSettings   = ParseSettings(settingsJson) ?? ExtractSettings(autoJson);
        _localizationService!.SetLanguage(_titleSettings.Language);
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

    // ── API publique (inchangée pour les shells Desktop/Web) ─────────────────

    private bool _isCanvasInitialized;

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
    }

    public void Tick()
    {
        if (_isDisposed || !_isInitialized) return;
        _gameScreen?.Tick();
    }

    public void Render(SKCanvas canvas)
    {
        if (_isDisposed || !_isInitialized) return;

        float uiScale = _uiLayoutService?.UiScale ?? 1f;

        if (_onTitleScreen)
            _titleScreen?.Render(canvas, _lastCanvasSize, uiScale);
        else
            _gameScreen?.Render(canvas);
    }

    public void HandlePointerPressed(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        if (_onTitleScreen) _titleScreen?.HandlePointerPressed(x, y, button);
        else                _gameScreen?.HandlePointerPressed(x, y, pointerId, button);
    }

    public void HandlePointerMoved(float x, float y, int pointerId = 0)
    {
        if (_onTitleScreen) _titleScreen?.HandlePointerMoved(x, y);
        else                _gameScreen?.HandlePointerMoved(x, y, pointerId);
    }

    public void HandlePointerReleased(float x, float y, int pointerId = 0, PointerButton button = PointerButton.Left)
    {
        if (_onTitleScreen) _titleScreen?.HandlePointerReleased(x, y, button);
        else                _gameScreen?.HandlePointerReleased(x, y, pointerId, button);
    }

    public void HandleZoom(float wheelDelta, float x, float y)
    {
        if (_onTitleScreen) _titleScreen?.HandleScroll(wheelDelta);
        else                _gameScreen?.HandleZoom(wheelDelta, x, y);
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
        if (_onTitleScreen) _titleScreen?.HandleKeyPressed(key);
        else                _gameScreen?.HandleKeyPressed(key, _allowDebugMode);
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
