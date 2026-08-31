using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Controller.Store;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class SettingsPopupRenderer : PopupRendererBase
{
    private readonly MainGameController  _gameController;
    private readonly LocalizationService _localization;
    private readonly SettingsContentPanel _contentPanel;
    private readonly bool _allowDebugMode;
    private readonly StoreController? _storeController;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<float>? UiScaleChanged;
    public event Action<int, int>? DebugWindowResizeRequested;

    public SettingsPopupRenderer(MainGameController gameController, LocalizationService localization, UILayoutService uiLayout, bool allowDebugMode = false, StoreController? storeController = null)
    {
        _gameController    = gameController;
        _localization      = localization;
        _allowDebugMode    = allowDebugMode;
        _storeController   = storeController;
        _contentPanel      = new SettingsContentPanel(uiLayout);
        _contentPanel.FullscreenToggleRequested    += v => FullscreenToggleRequested?.Invoke(v);
        _contentPanel.UiScaleChanged                += v => UiScaleChanged?.Invoke(v);
        _contentPanel.DebugWindowResizeRequested    += (w, h) => DebugWindowResizeRequested?.Invoke(w, h);
    }

    /// <summary>Instantane du popup pour une vue portee par l'hote.</summary>
    public SettingsPopupSnapshot GetSnapshot()
    {
        var settings = _gameController.CurrentMainState?.Settings;
        if (!IsOpen || Disposed || settings == null) return SettingsPopupSnapshot.Closed;

        return new SettingsPopupSnapshot(
            IsOpen: true,
            Title: _localization.Get("settings_title"),
            Panel: _contentPanel.GetSnapshot(settings, _localization, _allowDebugMode, CanvasSize, _storeController));
    }

    public void ToggleSettingFromHost(string key)
    {
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null) _contentPanel.ToggleFromHost(key, settings, _storeController);
    }

    public void SetSettingChoiceFromHost(string key, string choiceKey)
    {
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null) _contentPanel.SetChoiceFromHost(key, choiceKey, settings, _localization);
    }

    public void SetSettingSliderFromHost(string key, double value)
    {
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null) _contentPanel.SetSliderFromHost(key, value, settings);
    }

    public void SetSettingTextFromHost(string key, string value) => _contentPanel.SetTextFromHost(key, value);

    public override void Close()
    {
        _contentPanel.ClearFocus();
        base.Close();
    }
}
