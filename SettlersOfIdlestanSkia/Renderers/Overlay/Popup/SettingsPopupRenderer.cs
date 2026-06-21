using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class SettingsPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth  => 580;
    protected override float PopupHeight => _allowDebugMode ? 594 : 466;
    protected override float BtnFontSize => 12f;

    private const float BtnRightMargin = 24;
    private const float FirstRowY      = 72;

    private readonly MainGameController  _gameController;
    private readonly LocalizationService _localization;
    private readonly IFileSystemService  _fileSystemService;
    private readonly SettingsContentPanel _contentPanel = new();
    private readonly bool _allowDebugMode;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<float>? UiScaleChanged;
    public event Action<int, int>? DebugWindowResizeRequested;

    private SKRect _closeButtonRect = SKRect.Empty;
    private SKRect _popupRect       = SKRect.Empty;

    public SettingsPopupRenderer(MainGameController gameController, LocalizationService localization, IFileSystemService fileSystemService, bool allowDebugMode = false)
    {
        _gameController    = gameController;
        _localization      = localization;
        _fileSystemService = fileSystemService;
        _allowDebugMode    = allowDebugMode;
        _contentPanel.FullscreenToggleRequested    += v => FullscreenToggleRequested?.Invoke(v);
        _contentPanel.UiScaleChanged                += v => UiScaleChanged?.Invoke(v);
        _contentPanel.DebugWindowResizeRequested    += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);
    }

    public void Render(SKCanvas canvas, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings == null) return;

        float s      = ComputeScale(scale);
        UpdateFonts(s);

        float popupW = PopupWidth  * s;
        float popupH = PopupHeight * s;
        float x      = (CanvasSize.Width  - popupW) / 2;
        float y      = (CanvasSize.Height - popupH) / 2;
        _popupRect   = new SKRect(x, y, x + popupW, y + popupH);

        DrawBackground(canvas, _popupRect, s);

        _closeButtonRect = GetCloseRect(_popupRect, s);
        DrawCloseButton(canvas, _closeButtonRect, s);

        string title  = _localization.Get("settings_title");
        float  titleW = TitleFont!.MeasureText(title);
        SkiaTextUtils.DrawText(canvas, title, x + (popupW - titleW) / 2f, y + 34f * s, TitleFont, TextPaint);

        _contentPanel.Render(canvas, x, y + FirstRowY * s, popupW - BtnRightMargin * s, s, settings, _localization, _allowDebugMode, CanvasSize);
    }

    public bool HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen) return false;
        if (JustOpened) { JustOpened = false; return true; }

        if (_closeButtonRect.Contains(pos.X, pos.Y)) { Close(); return true; }

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null && _contentPanel.HandleClick(pos, settings, _localization, _allowDebugMode))
        {
            _ = _fileSystemService.SaveSettings(System.Text.Json.JsonSerializer.Serialize(settings));
            return true;
        }

        if (!_popupRect.Contains(pos.X, pos.Y)) { Close(); return false; }
        return true;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (!IsOpen || Disposed) return;
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null) _contentPanel.HandlePointerMoved(pos, settings);
    }

    public void HandlePointerReleased(SKPoint pos)
    {
        if (!IsOpen || Disposed) return;
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings == null) return;
        if (!_contentPanel.HandlePointerReleased(settings)) return;

        _ = _fileSystemService.SaveSettings(System.Text.Json.JsonSerializer.Serialize(settings));
    }

    /// <summary>Flèches gauche/droite pour ajuster le slider d'échelle UI quand il est survolé.</summary>
    public bool HandleKeyPressed(string key)
    {
        if (!IsOpen || Disposed) return false;
        if (_contentPanel.HandleTextKey(key)) return true;

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings == null || !_contentPanel.HandleArrowKey(key, settings)) return false;

        _ = _fileSystemService.SaveSettings(System.Text.Json.JsonSerializer.Serialize(settings));
        return true;
    }

    public override void Close()
    {
        _contentPanel.ClearFocus();
        base.Close();
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _contentPanel.Dispose();
        base.Dispose();
    }
}
