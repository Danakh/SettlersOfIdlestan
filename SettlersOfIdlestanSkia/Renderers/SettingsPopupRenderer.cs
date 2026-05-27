using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

public class SettingsPopupRenderer : IGameRenderer
{
    private const float PopupWidth = 580;
    private const float PopupHeight = 210;
    private const float CornerRadius = 14;
    private const float BtnWidth = 120;
    private const float BtnHeight = 34;
    private const float BtnGap = 10;
    private const float BtnRightMargin = 24;

    private bool _isVisible;
    private SKSize _canvasSize;

    private readonly MainGameController _gameController;
    private readonly ILocalizationService _localization;
    private readonly InputHandlingService _inputService;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(20, 20, 28, 235), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Color = SKColors.Gold, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _activeBtnPaint = new() { Color = new SKColor(60, 100, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inactiveBtnPaint = new() { Color = new SKColor(55, 55, 65), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnBorderPaint = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _labelPaint = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 16, Typeface = SkiaFonts.Bold };
    private readonly SKFont _labelFont = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private readonly SKFont _btnFont = new() { Size = 12, Typeface = SkiaFonts.Bold };

    // Hit-test rects, computed each frame
    private SKRect _btnFrench = SKRect.Empty;
    private SKRect _btnEnglish = SKRect.Empty;
    private SKRect _btnPauseOn = SKRect.Empty;
    private SKRect _btnPauseOff = SKRect.Empty;
    private SKRect _popupRect = SKRect.Empty;

    public bool IsVisible => _isVisible;

    public SettingsPopupRenderer(MainGameController gameController, ILocalizationService localization, InputHandlingService inputService)
    {
        _gameController = gameController;
        _localization = localization;
        _inputService = inputService;
        _inputService.PointerPressed += OnPointerPressed;
    }

    public void Show() => _isVisible = true;
    public void Hide() => _isVisible = false;

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!_isVisible) return;

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings == null) return;

        float x = (_canvasSize.Width - PopupWidth) / 2;
        float y = (_canvasSize.Height - PopupHeight) / 2;
        _popupRect = new SKRect(x, y, x + PopupWidth, y + PopupHeight);

        canvas.DrawRoundRect(_popupRect, CornerRadius, CornerRadius, _bgPaint);
        canvas.DrawRoundRect(_popupRect, CornerRadius, CornerRadius, _borderPaint);

        // Title
        string title = _localization.Get("settings_title");
        float titleW = _titleFont.MeasureText(title);
        canvas.DrawText(title, x + (PopupWidth - titleW) / 2, y + 34, _titleFont, _textPaint);

        // Ancrage droit commun pour les boutons
        float btnRight  = x + PopupWidth - BtnRightMargin;
        float btn2Left  = btnRight - BtnWidth;
        float btn1Left  = btn2Left - BtnGap - BtnWidth;

        // Row 1: Language
        float row1Y = y + 72;
        _btnFrench  = MakeRect(btn1Left, row1Y, BtnWidth, BtnHeight);
        _btnEnglish = MakeRect(btn2Left, row1Y, BtnWidth, BtnHeight);
        DrawRow(canvas, x, row1Y, "settings_language", new[]
        {
            (_btnFrench,  "menu_language_french",  settings.Language == Language.French),
            (_btnEnglish, "menu_language_english", settings.Language == Language.English),
        });

        // Row 2: Pause after prestige
        float row2Y = y + 136;
        _btnPauseOn  = MakeRect(btn1Left, row2Y, BtnWidth, BtnHeight);
        _btnPauseOff = MakeRect(btn2Left, row2Y, BtnWidth, BtnHeight);
        DrawRow(canvas, x, row2Y, "settings_pause_after_prestige", new[]
        {
            (_btnPauseOn,  "ui_yes", settings.PauseAfterPrestige),
            (_btnPauseOff, "ui_no",  !settings.PauseAfterPrestige),
        });
    }

    private void DrawRow(SKCanvas canvas, float popX, float rowY, string labelKey, (SKRect rect, string textKey, bool active)[] buttons)
    {
        // Label on the left
        string label = _localization.Get(labelKey) + " :";
        canvas.DrawText(label, popX + 20, rowY + BtnHeight / 2 + _labelFont.Size / 2, _labelFont, _labelPaint);

        foreach (var (rect, textKey, active) in buttons)
        {
            var bgPaint = active ? _activeBtnPaint : _inactiveBtnPaint;
            canvas.DrawRoundRect(rect, 6, 6, bgPaint);
            canvas.DrawRoundRect(rect, 6, 6, _btnBorderPaint);
            string text = _localization.Get(textKey);
            float tw = _btnFont.MeasureText(text);
            canvas.DrawText(text, rect.Left + (rect.Width - tw) / 2, rect.Top + rect.Height / 2 + _btnFont.Size / 2, _btnFont, _textPaint);
        }
    }

    private static SKRect MakeRect(float x, float y, float w, float h) => new(x, y, x + w, y + h);

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (!_isVisible) return;

        var pos = e.Position;

        if (_btnFrench.Contains(pos.X, pos.Y))
        {
            ApplyLanguage(Language.French);
            return;
        }
        if (_btnEnglish.Contains(pos.X, pos.Y))
        {
            ApplyLanguage(Language.English);
            return;
        }

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null)
        {
            if (_btnPauseOn.Contains(pos.X, pos.Y))
            {
                settings.PauseAfterPrestige = true;
                return;
            }
            if (_btnPauseOff.Contains(pos.X, pos.Y))
            {
                settings.PauseAfterPrestige = false;
                return;
            }
        }

        // Clic en dehors du popup → fermeture
        if (!_popupRect.Contains(pos.X, pos.Y))
            Hide();
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
        _inputService.PointerPressed -= OnPointerPressed;
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _activeBtnPaint.Dispose();
        _inactiveBtnPaint.Dispose();
        _btnBorderPaint.Dispose();
        _textPaint.Dispose();
        _labelPaint.Dispose();
        _titleFont.Dispose();
        _labelFont.Dispose();
        _btnFont.Dispose();
    }
}
