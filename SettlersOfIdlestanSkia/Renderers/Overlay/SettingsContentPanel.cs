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

    private const float SliderWidth  = 160f;
    private const float SliderHeight = 6f;
    private const float SliderKnobR  = 9f;
    public  const float UiScaleMin   = 0.5f;
    public  const float UiScaleMax   = 2f;

    private readonly SKPaint _activeBtnPaint    = new() { Color = new SKColor(60, 100, 180),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _inactiveBtnPaint  = new() { Color = new SKColor(55, 55, 65),    Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _labelPaint        = new() { Color = new SKColor(200, 200, 210), IsAntialias = true };
    private readonly SKPaint _btnBorderPaint    = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint         = new() { Color = SKColors.White,             IsAntialias = true };
    private readonly SKPaint _sliderTrackPaint  = new() { Color = new SKColor(55, 55, 65),    Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _sliderFillPaint   = new() { Color = new SKColor(60, 100, 180),  Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _sliderKnobPaint   = new() { Color = SKColors.White,             Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _sliderKnobBorder  = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1.2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _sliderKnobHoverBorder = new() { Color = new SKColor(60, 100, 180), StrokeWidth = 1.6f, Style = SKPaintStyle.Stroke, IsAntialias = true };

    private SKFont _labelFont = new() { Size = 13f, Typeface = SkiaFonts.Bold };
    private SKFont _btnFont   = new() { Size = 12f, Typeface = SkiaFonts.Bold };
    private float  _lastScale;

    private SKRect _btnFrench              = SKRect.Empty;
    private SKRect _btnEnglish             = SKRect.Empty;
    private SKRect _pauseToggleRect        = SKRect.Empty;
    private SKRect _particlesToggleRect    = SKRect.Empty;
    private SKRect _fullscreenToggleRect   = SKRect.Empty;
    private SKRect _uiScaleSliderRect      = SKRect.Empty;

    private bool _hoveredPause;
    private bool _hoveredParticles;
    private bool _hoveredFullscreen;
    private bool _hoveredUiScaleSlider;
    private bool _focusedUiScaleSlider;
    private bool _draggingUiScaleSlider;
    private float? _pendingUiScaleValue;
    private bool _disposed;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<float>? UiScaleChanged;

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

        // Row 5 — UI scale (affiche la valeur en cours de glissement si un drag est actif, sans encore l'appliquer)
        float row5Y = y + spacingY * 4f;
        _uiScaleSliderRect = DrawSliderRow(canvas, x, row5Y, rightEdge,
            localization.Get("settings_ui_scale"), _pendingUiScaleValue ?? settings.UiScale, UiScaleMin, UiScaleMax, _hoveredUiScaleSlider, btnH, s);

        return spacingY * 4f + btnH;
    }

    private SKRect DrawSliderRow(SKCanvas canvas, float rowX, float rowY, float rightEdge,
        string label, float value, float min, float max, bool isHovered, float btnH, float s)
    {
        SkiaTextUtils.DrawText(canvas, label + " :",
            rowX + 20f * s, rowY + btnH / 2f + _labelFont.Size / 2f, _labelFont, _labelPaint);

        float sliderW = SliderWidth  * s;
        float trackH  = SliderHeight * s;
        float knobR   = SliderKnobR  * s;
        float trackX  = rightEdge - sliderW;
        float trackY  = rowY + (btnH - trackH) / 2f;

        var trackRect = new SKRect(trackX, trackY, trackX + sliderW, trackY + trackH);
        canvas.DrawRoundRect(trackRect, trackH / 2f, trackH / 2f, _sliderTrackPaint);

        float ratio = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
        float knobCx = trackX + sliderW * ratio;
        float knobCy = trackY + trackH / 2f;
        if (ratio > 0f)
            canvas.DrawRoundRect(new SKRect(trackX, trackY, knobCx, trackY + trackH), trackH / 2f, trackH / 2f, _sliderFillPaint);

        canvas.DrawCircle(knobCx, knobCy, knobR, _sliderKnobPaint);
        canvas.DrawCircle(knobCx, knobCy, knobR, isHovered ? _sliderKnobHoverBorder : _sliderKnobBorder);

        string valueText = $"x{value:0.0}";
        float  textW     = _btnFont.MeasureText(valueText);
        SkiaTextUtils.DrawText(canvas, valueText,
            trackX - 12f * s - textW, rowY + btnH / 2f + _btnFont.Size / 2f, _btnFont, _textPaint);

        // Zone cliquable étendue verticalement pour faciliter la saisie du curseur.
        return new SKRect(trackX - knobR, rowY, trackX + sliderW + knobR, rowY + btnH);
    }

    /// <summary>Met à jour la position visuelle du curseur pendant le drag, sans encore appliquer la valeur
    /// (l'application réelle — qui redimensionne toute l'UI, y compris ce slider — n'a lieu qu'au relâchement).</summary>
    private void UpdatePendingUiScaleFromX(float x)
    {
        if (_uiScaleSliderRect.IsEmpty) return;
        float knobR   = SliderKnobR * _lastScale;
        float trackX  = _uiScaleSliderRect.Left + knobR;
        float sliderW = _uiScaleSliderRect.Width - knobR * 2f;
        float ratio   = sliderW > 0f ? Math.Clamp((x - trackX) / sliderW, 0f, 1f) : 0f;
        _pendingUiScaleValue = MathF.Round((UiScaleMin + ratio * (UiScaleMax - UiScaleMin)) * 10f) / 10f;
    }

    /// <summary>Ajuste le slider au clavier (flèches gauche/droite, pas de 0.1), quand il est survolé ou
    /// qu'il a le focus (acquis au clic, conservé jusqu'à l'activation d'un autre contrôle ou la fermeture de l'écran).
    /// Contrairement au drag souris, l'effet est appliqué immédiatement (pas de notion de "relâchement").</summary>
    public bool HandleArrowKey(string key, GameSettings settings)
    {
        if (!_hoveredUiScaleSlider && !_focusedUiScaleSlider) return false;
        float delta = key switch { "ArrowLeft" => -0.1f, "ArrowRight" => 0.1f, _ => 0f };
        if (delta == 0f) return false;

        float value = MathF.Round(Math.Clamp(settings.UiScale + delta, UiScaleMin, UiScaleMax) * 10f) / 10f;
        if (value == settings.UiScale) return false;

        settings.UiScale = value;
        UiScaleChanged?.Invoke(value);
        return true;
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

        var trackRect = new SKRect(rightEdge - toggleW, rowY + (btnH - toggleH) / 2f, rightEdge, rowY + (btnH + toggleH) / 2f);
        SkiaToggleUtils.Draw(canvas, trackRect, isOn, isHovered);
        return trackRect;
    }

    /// <summary>Returns true if a setting was changed.</summary>
    public bool HandleClick(SKPoint pos, GameSettings settings, LocalizationService localization)
    {
        if (_btnFrench.Contains(pos.X, pos.Y))
        {
            _focusedUiScaleSlider = false;
            localization.SetLanguage(Language.French);
            settings.Language = Language.French;
            return true;
        }
        if (_btnEnglish.Contains(pos.X, pos.Y))
        {
            _focusedUiScaleSlider = false;
            localization.SetLanguage(Language.English);
            settings.Language = Language.English;
            return true;
        }
        if (!_pauseToggleRect.IsEmpty && _pauseToggleRect.Contains(pos.X, pos.Y))
        {
            _focusedUiScaleSlider = false;
            settings.PauseAfterPrestige = !settings.PauseAfterPrestige;
            return true;
        }
        if (!_particlesToggleRect.IsEmpty && _particlesToggleRect.Contains(pos.X, pos.Y))
        {
            _focusedUiScaleSlider = false;
            settings.ShowHarvestParticles = !settings.ShowHarvestParticles;
            return true;
        }
        if (!_fullscreenToggleRect.IsEmpty && _fullscreenToggleRect.Contains(pos.X, pos.Y))
        {
            _focusedUiScaleSlider = false;
            settings.Fullscreen = !settings.Fullscreen;
            FullscreenToggleRequested?.Invoke(settings.Fullscreen);
            return true;
        }
        if (!_uiScaleSliderRect.IsEmpty && _uiScaleSliderRect.Contains(pos.X, pos.Y))
        {
            // Démarre le drag mais ne change pas encore le réglage — appliqué au relâchement.
            // Le slider garde le focus clavier (flèches) même si la souris le quitte ensuite.
            _focusedUiScaleSlider  = true;
            _draggingUiScaleSlider = true;
            UpdatePendingUiScaleFromX(pos.X);
            return false;
        }
        return false;
    }

    /// <summary>Retire le focus clavier du slider — à appeler à la fermeture de l'écran/popup qui héberge ce panneau.</summary>
    public void ClearFocus() => _focusedUiScaleSlider = false;

    /// <summary>Met à jour le survol et la position visuelle du slider pendant un drag en cours
    /// (la valeur n'est pas encore appliquée — voir <see cref="HandlePointerReleased"/>).</summary>
    public void HandlePointerMoved(SKPoint pos, GameSettings settings)
    {
        _hoveredPause         = !_pauseToggleRect.IsEmpty      && _pauseToggleRect.Contains(pos.X, pos.Y);
        _hoveredParticles     = !_particlesToggleRect.IsEmpty  && _particlesToggleRect.Contains(pos.X, pos.Y);
        _hoveredFullscreen    = !_fullscreenToggleRect.IsEmpty && _fullscreenToggleRect.Contains(pos.X, pos.Y);
        _hoveredUiScaleSlider = !_uiScaleSliderRect.IsEmpty    && _uiScaleSliderRect.Contains(pos.X, pos.Y);

        if (_draggingUiScaleSlider)
            UpdatePendingUiScaleFromX(pos.X);
    }

    /// <summary>Applique la valeur du slider d'échelle si un drag était en cours, et indique s'il faut persister.</summary>
    public bool HandlePointerReleased(GameSettings settings)
    {
        bool wasDragging = _draggingUiScaleSlider;
        _draggingUiScaleSlider = false;

        if (!wasDragging || _pendingUiScaleValue is not float value)
            return false;

        _pendingUiScaleValue = null;
        if (value == settings.UiScale) return false;

        settings.UiScale = value;
        UiScaleChanged?.Invoke(value);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _activeBtnPaint.Dispose();
        _inactiveBtnPaint.Dispose();
        _labelPaint.Dispose();
        _btnBorderPaint.Dispose();
        _textPaint.Dispose();
        _sliderTrackPaint.Dispose();
        _sliderFillPaint.Dispose();
        _sliderKnobPaint.Dispose();
        _sliderKnobBorder.Dispose();
        _sliderKnobHoverBorder.Dispose();
        _labelFont.Dispose();
        _btnFont.Dispose();
    }
}
