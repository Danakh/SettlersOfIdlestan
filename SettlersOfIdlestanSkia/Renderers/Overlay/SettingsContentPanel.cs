using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// <summary>
/// Panneau de contenu des paramètres — utilisé par SettingsPopupRenderer et TitleScreen.
/// Ajouter une option ici la fait apparaître automatiquement dans les deux endroits.
/// </summary>
public sealed class SettingsContentPanel : IDisposable
{
    private const float BtnWidth     = 120f;
    private const float BtnHeight    = 34f;
    private const float BtnGap       = 10f;
    public  const float RowSpacingY  = 64f;
    private const float ToggleWidth  = 46f;
    private const float ToggleHeight = 24f;

    private readonly SKPaint _activeBtnPaint    = new() { Color = new SKColor(60, 100, 180),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _inactiveBtnPaint  = new() { Color = new SKColor(55, 55, 65),    Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _labelPaint        = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKPaint _onPaint           = new() { Color = new SKColor(46, 125, 50),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _onHoverPaint      = new() { Color = new SKColor(60, 150, 64),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _offPaint          = new() { Color = new SKColor(160, 50, 50),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _offHoverPaint     = new() { Color = new SKColor(185, 65, 65),   Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _toggleBorderPaint = new() { Color = new SKColor(180, 180, 200), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _toggleKnobPaint   = new() { Color = SKColors.White,             Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _btnBorderPaint    = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint         = new() { Color = SKColors.White,             IsAntialias = true };

    private SKFont _labelFont = new() { Size = 13f, Typeface = SkiaFonts.Bold };
    private SKFont _btnFont   = new() { Size = 12f, Typeface = SkiaFonts.Bold };
    private float  _lastScale;

    private SKRect _btnFrench              = SKRect.Empty;
    private SKRect _btnEnglish             = SKRect.Empty;
    private SKRect _pauseToggleRect        = SKRect.Empty;
    private SKRect _particlesToggleRect    = SKRect.Empty;
    private SKRect _fullscreenToggleRect   = SKRect.Empty;

    private bool _hoveredPause;
    private bool _hoveredParticles;
    private bool _hoveredFullscreen;
    private bool _disposed;

    public event Action<bool>? FullscreenToggleRequested;

    private void UpdateFonts(float s)
    {
        if (s == _lastScale) return;
        _lastScale = s;
        _labelFont.Dispose();
        _labelFont = new SKFont { Size = 13f * s, Typeface = SkiaFonts.Bold };
        _btnFont.Dispose();
        _btnFont = new SKFont { Size = 12f * s, Typeface = SkiaFonts.Bold };
    }

    /// <summary>
    /// Renders all settings rows starting at (x, y) within a width.
    /// Returns the total height used by the content.
    /// </summary>
    public float Render(SKCanvas canvas, float x, float y, float width, float s,
        GameSettings settings, LocalizationService localization)
    {
        UpdateFonts(s);

        float btnW      = BtnWidth    * s;
        float btnH      = BtnHeight   * s;
        float btnGap    = BtnGap      * s;
        float spacingY  = RowSpacingY * s;
        float toggleW   = ToggleWidth  * s;
        float toggleH   = ToggleHeight * s;
        float rightEdge = x + width;

        float btn2Left = rightEdge - btnW;
        float btn1Left = btn2Left - btnGap - btnW;

        // Row 1 — Language (English first)
        float row1Y = y;
        _btnEnglish = new SKRect(btn1Left, row1Y, btn1Left + btnW, row1Y + btnH);
        _btnFrench  = new SKRect(btn2Left, row1Y, btn2Left + btnW, row1Y + btnH);
        DrawRow(canvas, x, row1Y, localization.Get("settings_language"), btnH, s,
        [
            (_btnEnglish, localization.Get("menu_language_english"), settings.Language == Language.English),
            (_btnFrench,  localization.Get("menu_language_french"),  settings.Language == Language.French),
        ]);

        // Row 2 — Fullscreen
        float row4Y = y + spacingY;
        _fullscreenToggleRect = DrawToggleRow(canvas, x, row4Y, rightEdge,
            localization.Get("settings_fullscreen"), settings.Fullscreen, _hoveredFullscreen, btnH, toggleW, toggleH, s);

        // Row 3 — Pause after prestige
        float row2Y = y + spacingY * 2f;
        _pauseToggleRect = DrawToggleRow(canvas, x, row2Y, rightEdge,
            localization.Get("settings_pause_after_prestige"), settings.PauseAfterPrestige, _hoveredPause, btnH, toggleW, toggleH, s);

        // Row 4 — Harvest particles
        float row3Y = y + spacingY * 3f;
        _particlesToggleRect = DrawToggleRow(canvas, x, row3Y, rightEdge,
            localization.Get("settings_harvest_particles"), settings.ShowHarvestParticles, _hoveredParticles, btnH, toggleW, toggleH, s);

        return spacingY * 3f + btnH;
    }

    private void DrawRow(SKCanvas canvas, float rowX, float rowY, string label, float btnH, float s,
        (SKRect rect, string text, bool active)[] buttons)
    {
        SkiaTextUtils.DrawText(canvas, label + " :",
            rowX + 20f * s, rowY + btnH / 2f + _labelFont.Size / 2f, _labelFont, _labelPaint);

        foreach (var (rect, text, active) in buttons)
        {
            canvas.DrawRoundRect(rect, 6f * s, 6f * s, active ? _activeBtnPaint : _inactiveBtnPaint);
            canvas.DrawRoundRect(rect, 6f * s, 6f * s, _btnBorderPaint);
            float tw = _btnFont.MeasureText(text);
            SkiaTextUtils.DrawText(canvas, text,
                rect.Left + (rect.Width  - tw) / 2f,
                rect.Top  + rect.Height  / 2f + _btnFont.Size / 2f,
                _btnFont, _textPaint);
        }
    }

    private SKRect DrawToggleRow(SKCanvas canvas, float rowX, float rowY, float rightEdge,
        string label, bool isOn, bool isHovered, float btnH, float toggleW, float toggleH, float s)
    {
        SkiaTextUtils.DrawText(canvas, label + " :",
            rowX + 20f * s, rowY + btnH / 2f + _labelFont.Size / 2f, _labelFont, _labelPaint);

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

    /// <summary>Returns true if a setting was changed.</summary>
    public bool HandleClick(SKPoint pos, GameSettings settings, LocalizationService localization)
    {
        if (_btnFrench.Contains(pos.X, pos.Y))
        {
            localization.SetLanguage(Language.French);
            settings.Language = Language.French;
            return true;
        }
        if (_btnEnglish.Contains(pos.X, pos.Y))
        {
            localization.SetLanguage(Language.English);
            settings.Language = Language.English;
            return true;
        }
        if (!_pauseToggleRect.IsEmpty && _pauseToggleRect.Contains(pos.X, pos.Y))
        {
            settings.PauseAfterPrestige = !settings.PauseAfterPrestige;
            return true;
        }
        if (!_particlesToggleRect.IsEmpty && _particlesToggleRect.Contains(pos.X, pos.Y))
        {
            settings.ShowHarvestParticles = !settings.ShowHarvestParticles;
            return true;
        }
        if (!_fullscreenToggleRect.IsEmpty && _fullscreenToggleRect.Contains(pos.X, pos.Y))
        {
            settings.Fullscreen = !settings.Fullscreen;
            FullscreenToggleRequested?.Invoke(settings.Fullscreen);
            return true;
        }
        return false;
    }

    public void HandleHover(SKPoint pos)
    {
        _hoveredPause      = !_pauseToggleRect.IsEmpty      && _pauseToggleRect.Contains(pos.X, pos.Y);
        _hoveredParticles  = !_particlesToggleRect.IsEmpty  && _particlesToggleRect.Contains(pos.X, pos.Y);
        _hoveredFullscreen = !_fullscreenToggleRect.IsEmpty && _fullscreenToggleRect.Contains(pos.X, pos.Y);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _activeBtnPaint.Dispose();
        _inactiveBtnPaint.Dispose();
        _labelPaint.Dispose();
        _onPaint.Dispose();
        _onHoverPaint.Dispose();
        _offPaint.Dispose();
        _offHoverPaint.Dispose();
        _toggleBorderPaint.Dispose();
        _toggleKnobPaint.Dispose();
        _btnBorderPaint.Dispose();
        _textPaint.Dispose();
        _labelFont.Dispose();
        _btnFont.Dispose();
    }
}
