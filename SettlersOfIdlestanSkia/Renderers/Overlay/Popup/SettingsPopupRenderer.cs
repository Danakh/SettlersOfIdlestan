using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class SettingsPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth    => 580;
    protected override float PopupHeight   => 274;
    protected override float BtnFontSize   => 12f;

    private const float BtnWidth        = 120;
    private const float BtnHeight       = 34;
    private const float BtnGap          = 10;
    private const float BtnRightMargin  = 24;
    private const float RowSpacingY     = 64;
    private const float FirstRowY       = 72;
    private const float ToggleWidth     = 46f;
    private const float ToggleHeight    = 24f;

    private readonly MainGameController  _gameController;
    private readonly LocalizationService _localization;

    // Settings-specific paints
    private readonly SKPaint _activeBtnPaint    = new() { Color = new SKColor(60, 100, 180),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _inactiveBtnPaint  = new() { Color = new SKColor(55, 55, 65),    Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _labelPaint        = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKPaint _onPaint           = new() { Color = new SKColor(46, 125, 50),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _onHoverPaint      = new() { Color = new SKColor(60, 150, 64),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _offPaint          = new() { Color = new SKColor(160, 50, 50),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _offHoverPaint     = new() { Color = new SKColor(185, 65, 65),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _toggleBorderPaint = new() { Color = new SKColor(180, 180, 200), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _toggleKnobPaint   = new() { Color = SKColors.White,             Style = SKPaintStyle.Fill,   IsAntialias = true };

    // Settings-specific font (label, Bold 13)
    private SKFont _labelFont = new() { Size = 13, Typeface = SkiaFonts.Bold };

    private SKRect _closeButtonRect    = SKRect.Empty;
    private SKRect _popupRect          = SKRect.Empty;
    private SKRect _btnFrench          = SKRect.Empty;
    private SKRect _btnEnglish         = SKRect.Empty;
    private SKRect _pauseToggleRect    = SKRect.Empty;
    private SKRect _particlesToggleRect = SKRect.Empty;

    private bool _hoveredPause, _hoveredParticles;

    public SettingsPopupRenderer(MainGameController gameController, LocalizationService localization)
    {
        _gameController = gameController;
        _localization   = localization;
    }

    protected override void OnFontsUpdated(float s)
    {
        _labelFont.Dispose();
        _labelFont = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Bold };
    }

    public void Render(SKCanvas canvas, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings == null) return;

        float s = ComputeScale(scale);
        UpdateFonts(s);

        float popupW        = PopupWidth      * s;
        float popupH        = PopupHeight     * s;
        float btnW          = BtnWidth        * s;
        float btnH          = BtnHeight       * s;
        float btnGap        = BtnGap          * s;
        float btnRightMargin = BtnRightMargin * s;
        float rowSpacingY   = RowSpacingY     * s;
        float firstRowY     = FirstRowY       * s;
        float toggleW       = ToggleWidth     * s;
        float toggleH       = ToggleHeight    * s;

        float x = (CanvasSize.Width  - popupW) / 2;
        float y = (CanvasSize.Height - popupH) / 2;
        _popupRect = new SKRect(x, y, x + popupW, y + popupH);

        DrawBackground(canvas, _popupRect, s);

        _closeButtonRect = GetCloseRect(_popupRect, s);
        DrawCloseButton(canvas, _closeButtonRect, s);

        string title  = _localization.Get("settings_title");
        float  titleW = TitleFont!.MeasureText(title);
        SkiaTextUtils.DrawText(canvas, title, x + (popupW - titleW) / 2, y + 34 * s, TitleFont, TextPaint);

        float btnRight = x + popupW - btnRightMargin;
        float btn2Left = btnRight - btnW;
        float btn1Left = btn2Left - btnGap - btnW;

        float row1Y = y + firstRowY;
        _btnFrench  = MakeRect(btn1Left, row1Y, btnW, btnH);
        _btnEnglish = MakeRect(btn2Left, row1Y, btnW, btnH);
        DrawRow(canvas, x, row1Y, "settings_language", btnH, s, new[]
        {
            (_btnFrench,  "menu_language_french",  settings.Language == Language.French),
            (_btnEnglish, "menu_language_english", settings.Language == Language.English),
        });

        float row2Y = y + firstRowY + rowSpacingY;
        _pauseToggleRect = DrawToggleRow(canvas, x, row2Y, btnRight, "settings_pause_after_prestige",
            settings.PauseAfterPrestige, _hoveredPause, btnH, toggleW, toggleH, s);

        float row3Y = y + firstRowY + rowSpacingY * 2;
        _particlesToggleRect = DrawToggleRow(canvas, x, row3Y, btnRight, "settings_harvest_particles",
            settings.ShowHarvestParticles, _hoveredParticles, btnH, toggleW, toggleH, s);
    }

    private void DrawRow(SKCanvas canvas, float popX, float rowY, string labelKey, float btnH, float s,
        (SKRect rect, string textKey, bool active)[] buttons)
    {
        SkiaTextUtils.DrawText(canvas, _localization.Get(labelKey) + " :",
            popX + 20 * s, rowY + btnH / 2 + _labelFont.Size / 2, _labelFont, _labelPaint);

        foreach (var (rect, textKey, active) in buttons)
        {
            var fill = active ? _activeBtnPaint : _inactiveBtnPaint;
            canvas.DrawRoundRect(rect, 6 * s, 6 * s, fill);
            canvas.DrawRoundRect(rect, 6 * s, 6 * s, BtnBorderPaint);
            string text = _localization.Get(textKey);
            float  tw   = BtnFont!.MeasureText(text);
            SkiaTextUtils.DrawText(canvas, text,
                rect.Left + (rect.Width - tw) / 2,
                rect.Top  + rect.Height / 2 + BtnFont.Size / 2,
                BtnFont, TextPaint);
        }
    }

    private SKRect DrawToggleRow(SKCanvas canvas, float popX, float rowY, float rightEdge,
        string labelKey, bool isOn, bool isHovered, float btnH, float toggleW, float toggleH, float s)
    {
        SkiaTextUtils.DrawText(canvas, _localization.Get(labelKey) + " :",
            popX + 20 * s, rowY + btnH / 2 + _labelFont.Size / 2, _labelFont, _labelPaint);

        float toggleX   = rightEdge - toggleW;
        float toggleY   = rowY + (btnH - toggleH) / 2f;
        float radius    = toggleH / 2f;
        var   trackRect = new SKRect(toggleX, toggleY, toggleX + toggleW, toggleY + toggleH);

        var fill = isOn ? (isHovered ? _onHoverPaint : _onPaint) : (isHovered ? _offHoverPaint : _offPaint);
        canvas.DrawRoundRect(trackRect, radius, radius, fill);
        canvas.DrawRoundRect(trackRect, radius, radius, _toggleBorderPaint);

        float knobR  = radius - 3f * s;
        float knobCy = toggleY + radius;
        float knobCx = isOn ? toggleX + toggleW - radius - 1f * s : toggleX + radius + 1f * s;
        canvas.DrawCircle(knobCx, knobCy, knobR, _toggleKnobPaint);

        return trackRect;
    }

    private static SKRect MakeRect(float x, float y, float w, float h) => new(x, y, x + w, y + h);

    public bool HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen) return false;
        if (JustOpened) { JustOpened = false; return true; }

        if (_closeButtonRect.Contains(pos.X, pos.Y)) { Close(); return true; }

        if (_btnFrench.Contains(pos.X, pos.Y))  { ApplyLanguage(Language.French);  return true; }
        if (_btnEnglish.Contains(pos.X, pos.Y)) { ApplyLanguage(Language.English); return true; }

        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null)
        {
            if (!_pauseToggleRect.IsEmpty     && _pauseToggleRect.Contains(pos.X, pos.Y))     { settings.PauseAfterPrestige    = !settings.PauseAfterPrestige;    return true; }
            if (!_particlesToggleRect.IsEmpty && _particlesToggleRect.Contains(pos.X, pos.Y)) { settings.ShowHarvestParticles = !settings.ShowHarvestParticles; return true; }
        }

        if (!_popupRect.Contains(pos.X, pos.Y)) { Close(); return false; }
        return true;
    }

    public void HandlePointerMoved(SKPoint pos)
    {
        if (!IsOpen || Disposed) return;
        _hoveredPause     = !_pauseToggleRect.IsEmpty     && _pauseToggleRect.Contains(pos.X, pos.Y);
        _hoveredParticles = !_particlesToggleRect.IsEmpty && _particlesToggleRect.Contains(pos.X, pos.Y);
    }

    private void ApplyLanguage(Language lang)
    {
        _localization.SetLanguage(lang);
        var settings = _gameController.CurrentMainState?.Settings;
        if (settings != null)
            settings.Language = lang;
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _activeBtnPaint.Dispose();
        _inactiveBtnPaint.Dispose();
        _labelPaint.Dispose();
        _onPaint.Dispose();
        _onHoverPaint.Dispose();
        _offPaint.Dispose();
        _offHoverPaint.Dispose();
        _toggleBorderPaint.Dispose();
        _toggleKnobPaint.Dispose();
        _labelFont.Dispose();
        base.Dispose();
    }
}
