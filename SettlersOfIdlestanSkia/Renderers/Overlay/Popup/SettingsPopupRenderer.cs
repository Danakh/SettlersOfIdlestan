using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class SettingsPopupRenderer : IDisposable
{
    private const float PopupWidth     = 580;
    private const float PopupHeight    = 210;
    private const float BtnWidth       = 120;
    private const float BtnHeight      = 34;
    private const float BtnGap         = 10;
    private const float BtnRightMargin = 24;
    private const float RowSpacingY    = 64;
    private const float FirstRowY      = 72;

    private readonly MainGameController _gameController;
    private readonly ILocalizationService _localization;

    private readonly PopupChrome _chrome = new();
    private readonly SKPaint _activeBtnPaint   = new() { Color = new SKColor(60, 100, 180),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _inactiveBtnPaint = new() { Color = new SKColor(55, 55, 65),    Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _btnBorderPaint   = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint        = new() { Color = SKColors.White,             IsAntialias = true };
    private readonly SKPaint _labelPaint       = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKFont  _titleFont        = new() { Size = 16, Typeface = SkiaFonts.Bold };
    private readonly SKFont  _labelFont        = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont  _btnFont          = new() { Size = 12, Typeface = SkiaFonts.Bold };

    private SKSize _canvasSize;
    private SKRect _closeButtonRect = SKRect.Empty;
    private SKRect _popupRect       = SKRect.Empty;
    private SKRect _btnFrench       = SKRect.Empty;
    private SKRect _btnEnglish      = SKRect.Empty;
    private SKRect _btnPauseOn      = SKRect.Empty;
    private SKRect _btnPauseOff     = SKRect.Empty;

    private bool _disposed;
    private bool _justOpened;

    public bool IsOpen { get; private set; }

    public SettingsPopupRenderer(MainGameController gameController, ILocalizationService localization)
    {
        _gameController = gameController;
        _localization   = localization;
    }

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void Open()
    {
        IsOpen = true;
        _justOpened = true;
    }

    public void Close() => IsOpen = false;

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || _disposed) return;

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings == null) return;

        float x = (_canvasSize.Width  - PopupWidth)  / 2;
        float y = (_canvasSize.Height - PopupHeight) / 2;
        _popupRect = new SKRect(x, y, x + PopupWidth, y + PopupHeight);

        _chrome.DrawBackground(canvas, _popupRect, _canvasSize);

        _closeButtonRect = PopupChrome.GetCloseRect(_popupRect);
        _chrome.DrawCloseButton(canvas, _closeButtonRect);

        string title = _localization.Get("settings_title");
        float titleW = _titleFont.MeasureText(title);
        canvas.DrawText(title, x + (PopupWidth - titleW) / 2, y + 34, _titleFont, _textPaint);

        float btnRight = x + PopupWidth - BtnRightMargin;
        float btn2Left = btnRight - BtnWidth;
        float btn1Left = btn2Left - BtnGap - BtnWidth;

        float row1Y = y + FirstRowY;
        _btnFrench  = MakeRect(btn1Left, row1Y, BtnWidth, BtnHeight);
        _btnEnglish = MakeRect(btn2Left, row1Y, BtnWidth, BtnHeight);
        DrawRow(canvas, x, row1Y, "settings_language", new[]
        {
            (_btnFrench,  "menu_language_french",  settings.Language == Language.French),
            (_btnEnglish, "menu_language_english", settings.Language == Language.English),
        });

        float row2Y = y + FirstRowY + RowSpacingY;
        _btnPauseOn  = MakeRect(btn1Left, row2Y, BtnWidth, BtnHeight);
        _btnPauseOff = MakeRect(btn2Left, row2Y, BtnWidth, BtnHeight);
        DrawRow(canvas, x, row2Y, "settings_pause_after_prestige", new[]
        {
            (_btnPauseOn,  "ui_yes",  settings.PauseAfterPrestige),
            (_btnPauseOff, "ui_no",  !settings.PauseAfterPrestige),
        });
    }

    private void DrawRow(SKCanvas canvas, float popX, float rowY, string labelKey,
        (SKRect rect, string textKey, bool active)[] buttons)
    {
        canvas.DrawText(_localization.Get(labelKey) + " :",
            popX + 20, rowY + BtnHeight / 2 + _labelFont.Size / 2,
            _labelFont, _labelPaint);

        foreach (var (rect, textKey, active) in buttons)
        {
            canvas.DrawRoundRect(rect, 6, 6, active ? _activeBtnPaint : _inactiveBtnPaint);
            canvas.DrawRoundRect(rect, 6, 6, _btnBorderPaint);
            string text = _localization.Get(textKey);
            float tw = _btnFont.MeasureText(text);
            canvas.DrawText(text, rect.Left + (rect.Width - tw) / 2,
                rect.Top + rect.Height / 2 + _btnFont.Size / 2, _btnFont, _textPaint);
        }
    }

    private static SKRect MakeRect(float x, float y, float w, float h) => new(x, y, x + w, y + h);

    /// <returns>true si le clic est consommé (popup visible), false sinon.</returns>
    public bool HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen) return false;

        if (_justOpened) { _justOpened = false; return true; }

        if (_closeButtonRect.Contains(pos.X, pos.Y))
        {
            Close();
            return true;
        }

        if (_btnFrench.Contains(pos.X, pos.Y))  { ApplyLanguage(Language.French);  return true; }
        if (_btnEnglish.Contains(pos.X, pos.Y)) { ApplyLanguage(Language.English); return true; }

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null)
        {
            if (_btnPauseOn.Contains(pos.X, pos.Y))  { settings.PauseAfterPrestige = true;  return true; }
            if (_btnPauseOff.Contains(pos.X, pos.Y)) { settings.PauseAfterPrestige = false; return true; }
        }

        if (!_popupRect.Contains(pos.X, pos.Y))
        {
            Close();
            return false;
        }

        return true;
    }

    private void ApplyLanguage(Language lang)
    {
        _localization.SetLanguage(lang);
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null)
            settings.Language = lang;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _chrome.Dispose();
        _activeBtnPaint.Dispose();
        _inactiveBtnPaint.Dispose();
        _btnBorderPaint.Dispose();
        _textPaint.Dispose();
        _labelPaint.Dispose();
        _titleFont.Dispose();
        _labelFont.Dispose();
        _btnFont.Dispose();
        _disposed = true;
    }
}
