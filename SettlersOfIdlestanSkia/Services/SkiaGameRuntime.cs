using SkiaSharp;
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
    private bool                  _allowDebugMode;

    private TitleScreen? _titleScreen;
    private GameScreen?  _gameScreen;
    private bool         _onTitleScreen;

    private SKSize _lastCanvasSize;
    private bool   _isDisposed;
    private bool   _isInitialized;

    public event Action? QuitRequested;

    // ── Initialisation ────────────────────────────────────────────────────────

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
        if (_isDisposed)    throw new ObjectDisposedException(nameof(SkiaGameRuntime));
        if (_isInitialized) return;

        _fileSystemService   = fileSystemService;
        _allowDebugMode      = allowDebugMode;
        _resourceManager     = new ResourceManager();
        _localizationService = new LocalizationService();
        _uiLayoutService     = new UILayoutService();

        bool hasSave = !string.IsNullOrEmpty(autoJson);
        ShowTitleScreen(hasSave);

        _isInitialized = true;
    }

    // ── Navigation entre écrans ───────────────────────────────────────────────

    private void ShowTitleScreen(bool hasSave)
    {
        _titleScreen?.Dispose();
        _titleScreen = new TitleScreen(_fileSystemService!, _localizationService!, _uiLayoutService!, hasSave);
        _titleScreen.NewGameRequested  += OnNewGameRequested;
        _titleScreen.ContinueRequested += OnContinueRequested;
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
            _allowDebugMode);
        _gameScreen.ReturnToTitleRequested += OnReturnToTitle;
        _gameScreen.QuitRequested          += () => QuitRequested?.Invoke();

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
            _allowDebugMode);
        _gameScreen.ReturnToTitleRequested += OnReturnToTitle;
        _gameScreen.QuitRequested          += () => QuitRequested?.Invoke();

        if (_isCanvasInitialized)
            _gameScreen.EnsureCanvasInitialized(_lastCanvasSize);
    }

    private async void OnReturnToTitle()
    {
        _gameScreen?.Dispose();
        _gameScreen = null;

        bool hasSave = await _fileSystemService!.LoadAuto() != null;
        ShowTitleScreen(hasSave);

    }

    // ── API publique (inchangée pour les shells Desktop/Web) ─────────────────

    private bool _isCanvasInitialized;

    public void SetUiScale(float scale)
    {
        if (_uiLayoutService != null) _uiLayoutService.UiScale = scale;
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

    public bool TryGetDebugStats(out RuntimeDebugStats stats)
    {
        if (!_onTitleScreen && _gameScreen != null)
            return _gameScreen.TryGetDebugStats(out stats);
        stats = default;
        return false;
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

public readonly record struct RuntimeDebugStats(
    float fps,
    float cameraX,
    float cameraY,
    int   cityCount,
    int   roadCount);
