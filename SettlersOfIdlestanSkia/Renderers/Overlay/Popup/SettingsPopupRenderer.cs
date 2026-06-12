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
    protected override float PopupHeight => 274;
    protected override float BtnFontSize => 12f;

    private const float BtnRightMargin = 24;
    private const float FirstRowY      = 72;

    private readonly MainGameController  _gameController;
    private readonly LocalizationService _localization;
    private readonly SettingsContentPanel _contentPanel = new();

    private SKRect _closeButtonRect = SKRect.Empty;
    private SKRect _popupRect       = SKRect.Empty;

    public SettingsPopupRenderer(MainGameController gameController, LocalizationService localization)
    {
        _gameController = gameController;
        _localization   = localization;
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

        _contentPanel.Render(canvas, x, y + FirstRowY * s, popupW - BtnRightMargin * s, s, settings, _localization);
    }

    public bool HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen) return false;
        if (JustOpened) { JustOpened = false; return true; }

        if (_closeButtonRect.Contains(pos.X, pos.Y)) { Close(); return true; }

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null && _contentPanel.HandleClick(pos, settings, _localization))
            return true;

        if (!_popupRect.Contains(pos.X, pos.Y)) { Close(); return false; }
        return true;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (!IsOpen || Disposed) return;
        _contentPanel.HandleHover(pos);
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _contentPanel.Dispose();
        base.Dispose();
    }
}
