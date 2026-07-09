using System.Text.RegularExpressions;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;
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

    private const int MaxDebugResolutionLength = 11; // "99999x99999"
    private const int MinDebugResolution = 128;
    private static readonly Regex DebugResolutionRegex = new(@"^(\d{1,5})[xX](\d{1,5})$", RegexOptions.Compiled);

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
    private readonly SKPaint _scrollTrackPaint  = new() { Color = new SKColor(50, 50, 65, 200),    Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _scrollThumbPaint  = new() { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKFont _labelFont = new() { Size = 13f, Typeface = SkiaFonts.Bold };
    private SKFont _btnFont   = new() { Size = 12f, Typeface = SkiaFonts.Bold };
    private float  _lastScale;

    private SKRect _btnFrench              = SKRect.Empty;
    private SKRect _btnEnglish             = SKRect.Empty;
    private SKRect _pauseToggleRect        = SKRect.Empty;
    private SKRect _particlesToggleRect    = SKRect.Empty;
    private SKRect _militaryStatsToggleRect = SKRect.Empty;
    private SKRect _fullscreenToggleRect   = SKRect.Empty;
    private SKRect _menuPositionToggleRect = SKRect.Empty;
    private SKRect _uiScaleSliderRect      = SKRect.Empty;
    private SKRect _debugResolutionFieldRect = SKRect.Empty;
    private SKRect _exportTransparentBgToggleRect = SKRect.Empty;

    private bool _hoveredPause;
    private bool _hoveredParticles;
    private bool _hoveredMilitaryStats;
    private bool _hoveredFullscreen;
    private bool _hoveredMenuPosition;
    private bool _hoveredUiScaleSlider;
    private bool _hoveredExportTransparentBg;
    private bool _focusedUiScaleSlider;
    private bool _draggingUiScaleSlider;
    private float? _pendingUiScaleValue;
    private bool _disposed;

    private float  _scrollOffsetPx;
    private float  _totalContentHeight;
    private float  _viewportHeight;
    private float  _viewportTop;
    private bool   _needsScroll;
    private bool   _isDraggingScrollbar;
    private float  _scrollDragStartY;
    private float  _scrollDragStartOffset;
    private SKRect _scrollTrackRect = SKRect.Empty;
    private SKRect _scrollThumbRect = SKRect.Empty;

    private readonly UILayoutService _uiLayout;

    private string _debugResolutionText     = "";
    private bool   _debugResolutionFocused;

    public event Action<bool>? FullscreenToggleRequested;
    public event Action<float>? UiScaleChanged;
    public event Action<int, int>? DebugWindowResizeRequested;

    public SettingsContentPanel(UILayoutService uiLayout)
    {
        _uiLayout = uiLayout;
    }

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
        GameSettings settings, LocalizationService localization, bool allowDebugMode = false, SKSize currentResolution = default,
        float maxHeight = float.PositiveInfinity)
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

        int   rowCount      = allowDebugMode ? 8 : 6;
        float contentHeight = spacingY * rowCount + btnH;

        _needsScroll        = contentHeight > maxHeight;
        _viewportTop        = y;
        _viewportHeight     = _needsScroll ? maxHeight : contentHeight;
        _totalContentHeight = contentHeight;
        float maxScroll     = Math.Max(0f, contentHeight - _viewportHeight);
        _scrollOffsetPx     = _needsScroll ? Math.Clamp(_scrollOffsetPx, 0f, maxScroll) : 0f;

        if (_needsScroll)
        {
            canvas.Save();
            canvas.ClipRect(new SKRect(x, y, rightEdge, y + _viewportHeight));
            canvas.Translate(0, -_scrollOffsetPx);
        }

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

        // Row 3 — Position du menu (tabs en haut ou en bas)
        float rowMenuPositionY = y + spacingY * 2f;
        _menuPositionToggleRect = DrawToggleRow(canvas, x, rowMenuPositionY, rightEdge,
            localization.Get("settings_force_menu_position"), _uiLayout.MenuAtBottomSetting, _hoveredMenuPosition, btnH, toggleW, toggleH, s);

        // Row 4 — Pause after prestige
        float row2Y = y + spacingY * 3f;
        _pauseToggleRect = DrawToggleRow(canvas, x, row2Y, rightEdge,
            localization.Get("settings_pause_after_prestige"), settings.PauseAfterPrestige, _hoveredPause, btnH, toggleW, toggleH, s);

        // Row 5 — Harvest particles
        float row3Y = y + spacingY * 4f;
        _particlesToggleRect = DrawToggleRow(canvas, x, row3Y, rightEdge,
            localization.Get("settings_harvest_particles"), settings.ShowHarvestParticles, _hoveredParticles, btnH, toggleW, toggleH, s);

        // Row 6 — Afficher les stats militaires des villes
        float row6Y = y + spacingY * 5f;
        _militaryStatsToggleRect = DrawToggleRow(canvas, x, row6Y, rightEdge,
            localization.Get("settings_show_military_stats"), settings.ShowCityMilitaryStats, _hoveredMilitaryStats, btnH, toggleW, toggleH, s);

        // Row 7 — UI scale (affiche la valeur en cours de glissement si un drag est actif, sans encore l'appliquer)
        float row5Y = y + spacingY * 6f;
        _uiScaleSliderRect = DrawSliderRow(canvas, x, row5Y, rightEdge,
            localization.Get("settings_ui_scale"), _pendingUiScaleValue ?? settings.UiScale, UiScaleMin, UiScaleMax, _hoveredUiScaleSlider, btnH, s);

        if (allowDebugMode)
        {
            // Row 8 (debug uniquement) — Résolution de la fenêtre, appliquée à l'appui sur Entrée.
            // Tant que le champ n'a pas le focus, il reflète en continu la résolution actuelle de la fenêtre.
            if (!_debugResolutionFocused && currentResolution.Width > 0f && currentResolution.Height > 0f)
                _debugResolutionText = $"{(int)MathF.Round(currentResolution.Width)}x{(int)MathF.Round(currentResolution.Height)}";

            float row7Y = y + spacingY * 7f;
            _debugResolutionFieldRect = DrawTextInputRow(canvas, x, row7Y, rightEdge,
                localization.Get("settings_debug_window_resolution"), _debugResolutionText, _debugResolutionFocused, btnH, s);

            // Row 9 (debug uniquement) — Export PNG avec fond transparent plutôt que le fond opaque habituel.
            float row8Y = y + spacingY * 8f;
            _exportTransparentBgToggleRect = DrawToggleRow(canvas, x, row8Y, rightEdge,
                localization.Get("settings_debug_export_transparent_bg"), DebugSettings.ExportTransparentBackground,
                _hoveredExportTransparentBg, btnH, toggleW, toggleH, s);
        }

        if (_needsScroll)
        {
            canvas.Restore();
            DrawScrollbar(canvas, rightEdge, y, _viewportHeight, s);
        }

        return contentHeight;
    }

    // Piste + curseur du scrollbar, dessinés hors du clip (dans la marge droite réservée par l'appelant).
    private void DrawScrollbar(SKCanvas canvas, float rightEdge, float top, float viewportHeight, float s)
    {
        float scrollW = 5f * s;
        float trackX  = rightEdge + 6f * s;
        var   trackRect = new SKRect(trackX, top, trackX + scrollW, top + viewportHeight);
        canvas.DrawRoundRect(trackRect, scrollW / 2f, scrollW / 2f, _scrollTrackPaint);

        float maxScroll  = Math.Max(1f, _totalContentHeight - viewportHeight);
        float thumbRatio = viewportHeight / _totalContentHeight;
        float thumbH     = Math.Max(16f * s, thumbRatio * viewportHeight);
        float thumbTop   = top + (_scrollOffsetPx / maxScroll) * (viewportHeight - thumbH);

        _scrollTrackRect = trackRect;
        _scrollThumbRect = new SKRect(trackX, thumbTop, trackX + scrollW, thumbTop + thumbH);
        canvas.DrawRoundRect(_scrollThumbRect, scrollW / 2f, scrollW / 2f, _scrollThumbPaint);
    }

    private SKRect DrawTextInputRow(SKCanvas canvas, float rowX, float rowY, float rightEdge,
        string label, string value, bool isFocused, float btnH, float s)
    {
        SkiaTextUtils.DrawText(canvas, label + " :",
            rowX + 20f * s, rowY + btnH / 2f + _labelFont.Size / 2f, _labelFont, _labelPaint);

        float fieldW = SliderWidth * s;
        float fieldH = BtnHeight   * s;
        float fieldX = rightEdge - fieldW;
        var   fieldRect = new SKRect(fieldX, rowY, fieldX + fieldW, rowY + fieldH);

        canvas.DrawRoundRect(fieldRect, 6f * s, 6f * s, _inactiveBtnPaint);
        canvas.DrawRoundRect(fieldRect, 6f * s, 6f * s, isFocused ? _sliderKnobHoverBorder : _btnBorderPaint);

        bool   hasValue     = value.Length > 0;
        string displayText  = hasValue ? value : "1920x1080";
        var    displayPaint = hasValue ? _textPaint : _labelPaint;
        SkiaTextUtils.DrawText(canvas, displayText,
            fieldRect.Left + 10f * s, fieldRect.Top + fieldRect.Height / 2f + _btnFont.Size / 2f, _btnFont, displayPaint);

        return fieldRect;
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
    public bool HandleClick(SKPoint pos, GameSettings settings, LocalizationService localization, bool allowDebugMode = false)
    {
        if (_needsScroll)
        {
            if (!_scrollThumbRect.IsEmpty && _scrollThumbRect.Contains(pos.X, pos.Y))
            {
                _isDraggingScrollbar   = true;
                _scrollDragStartY      = pos.Y;
                _scrollDragStartOffset = _scrollOffsetPx;
                return false;
            }
            if (!_scrollTrackRect.IsEmpty && _scrollTrackRect.Contains(pos.X, pos.Y))
            {
                float maxScroll = Math.Max(0f, _totalContentHeight - _viewportHeight);
                float ratio     = (pos.Y - _scrollTrackRect.Top) / _scrollTrackRect.Height;
                _scrollOffsetPx = Math.Clamp(ratio * maxScroll, 0f, maxScroll);
                return false;
            }
            // Clic hors du viewport visible (contenu masqué par le clip) — ignoré.
            if (pos.Y < _viewportTop || pos.Y > _viewportTop + _viewportHeight)
                return false;
        }

        // Convertit la position écran en coordonnées de contenu (les rects ci-dessous sont
        // enregistrés tels que dessinés avant défilement — voir canvas.Translate dans Render).
        float py = pos.Y + _scrollOffsetPx;

        if (_btnFrench.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            localization.SetLanguage(Language.French);
            settings.Language = Language.French;
            return true;
        }
        if (_btnEnglish.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            localization.SetLanguage(Language.English);
            settings.Language = Language.English;
            return true;
        }
        if (!_pauseToggleRect.IsEmpty && _pauseToggleRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            settings.PauseAfterPrestige = !settings.PauseAfterPrestige;
            return true;
        }
        if (!_particlesToggleRect.IsEmpty && _particlesToggleRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            settings.ShowHarvestParticles = !settings.ShowHarvestParticles;
            return true;
        }
        if (!_militaryStatsToggleRect.IsEmpty && _militaryStatsToggleRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            settings.ShowCityMilitaryStats = !settings.ShowCityMilitaryStats;
            return true;
        }
        if (!_fullscreenToggleRect.IsEmpty && _fullscreenToggleRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            settings.Fullscreen = !settings.Fullscreen;
            FullscreenToggleRequested?.Invoke(settings.Fullscreen);
            return true;
        }
        if (!_menuPositionToggleRect.IsEmpty && _menuPositionToggleRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            settings.ForceMenuPosition = _uiLayout.MenuAtBottomSetting ? MenuPosition.Top : MenuPosition.Bottom;
            // Reflète le changement immédiatement : sur l'écran-titre rien d'autre ne resynchronise
            // UILayoutService à chaque frame (contrairement à OverlayRenderer pendant une partie).
            _uiLayout.SetMenuPosition(settings.ForceMenuPosition);
            return true;
        }
        if (!_uiScaleSliderRect.IsEmpty && _uiScaleSliderRect.Contains(pos.X, py))
        {
            // Démarre le drag mais ne change pas encore le réglage — appliqué au relâchement.
            // Le slider garde le focus clavier (flèches) même si la souris le quitte ensuite.
            _focusedUiScaleSlider   = true;
            _debugResolutionFocused = false;
            _draggingUiScaleSlider  = true;
            UpdatePendingUiScaleFromX(pos.X);
            return false;
        }
        if (allowDebugMode && !_debugResolutionFieldRect.IsEmpty && _debugResolutionFieldRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = true;
            return false;
        }
        if (allowDebugMode && !_exportTransparentBgToggleRect.IsEmpty && _exportTransparentBgToggleRect.Contains(pos.X, py))
        {
            _focusedUiScaleSlider   = false;
            _debugResolutionFocused = false;
            DebugSettings.ExportTransparentBackground = !DebugSettings.ExportTransparentBackground;
            return false;
        }
        _debugResolutionFocused = false;
        return false;
    }

    /// <summary>Ajuste le défilement au clavier/molette de souris. Retourne false si le contenu tient déjà entier.</summary>
    public bool HandleScroll(float delta)
    {
        if (!_needsScroll) return false;
        const float step = 60f;
        float dir       = delta > 0 ? -1f : 1f;
        float maxScroll = Math.Max(0f, _totalContentHeight - _viewportHeight);
        _scrollOffsetPx = Math.Clamp(_scrollOffsetPx + dir * step, 0f, maxScroll);
        return true;
    }

    /// <summary>Traite une touche tapée dans le champ de résolution de debug. Retourne true si la touche a été
    /// consommée par le champ (et ne doit donc pas être interprétée comme un raccourci de jeu).</summary>
    public bool HandleTextKey(string key)
    {
        if (!_debugResolutionFocused) return false;

        switch (key)
        {
            case "Escape":
                _debugResolutionFocused = false;
                return true;
            case "Enter":
                TryApplyDebugResolution();
                return true;
            case "Backspace":
                if (_debugResolutionText.Length > 0)
                    _debugResolutionText = _debugResolutionText[..^1];
                return true;
            default:
                if (key.Length == 1 && _debugResolutionText.Length < MaxDebugResolutionLength &&
                    (char.IsDigit(key[0]) || key[0] is 'x' or 'X'))
                {
                    _debugResolutionText += key[0];
                }
                return true;
        }
    }

    /// <summary>Applique la résolution saisie — uniquement si elle correspond au format "LARGEURxHAUTEUR"
    /// et que chaque dimension est d'au moins <see cref="MinDebugResolution"/> pixels.</summary>
    private void TryApplyDebugResolution()
    {
        var match = DebugResolutionRegex.Match(_debugResolutionText);
        if (!match.Success) return;
        if (!int.TryParse(match.Groups[1].Value, out int width)  || width  < MinDebugResolution) return;
        if (!int.TryParse(match.Groups[2].Value, out int height) || height < MinDebugResolution) return;

        DebugWindowResizeRequested?.Invoke(width, height);
    }

    /// <summary>Retire le focus clavier du slider et du champ de résolution debug — à appeler à la fermeture
    /// de l'écran/popup qui héberge ce panneau.</summary>
    public void ClearFocus()
    {
        _focusedUiScaleSlider   = false;
        _debugResolutionFocused = false;
        _isDraggingScrollbar    = false;
    }

    /// <summary>Met à jour le survol et la position visuelle du slider pendant un drag en cours
    /// (la valeur n'est pas encore appliquée — voir <see cref="HandlePointerReleased"/>).</summary>
    public void HandlePointerMoved(SKPoint pos, GameSettings settings)
    {
        if (_isDraggingScrollbar)
        {
            float thumbRange  = _scrollTrackRect.Height - _scrollThumbRect.Height;
            float maxScroll   = Math.Max(0f, _totalContentHeight - _viewportHeight);
            float scrollPerPx = thumbRange > 0f ? maxScroll / thumbRange : 0f;
            _scrollOffsetPx   = Math.Clamp(_scrollDragStartOffset + (pos.Y - _scrollDragStartY) * scrollPerPx, 0f, maxScroll);
            return;
        }

        // Ignore le survol des lignes quand le pointeur est hors du viewport visible (contenu masqué par le clip).
        bool inViewport = !_needsScroll || (pos.Y >= _viewportTop && pos.Y <= _viewportTop + _viewportHeight);
        float py = inViewport ? pos.Y + _scrollOffsetPx : float.NegativeInfinity;

        _hoveredPause         = !_pauseToggleRect.IsEmpty      && _pauseToggleRect.Contains(pos.X, py);
        _hoveredParticles     = !_particlesToggleRect.IsEmpty  && _particlesToggleRect.Contains(pos.X, py);
        _hoveredMilitaryStats = !_militaryStatsToggleRect.IsEmpty && _militaryStatsToggleRect.Contains(pos.X, py);
        _hoveredFullscreen    = !_fullscreenToggleRect.IsEmpty && _fullscreenToggleRect.Contains(pos.X, py);
        _hoveredMenuPosition  = !_menuPositionToggleRect.IsEmpty && _menuPositionToggleRect.Contains(pos.X, py);
        _hoveredUiScaleSlider = !_uiScaleSliderRect.IsEmpty    && _uiScaleSliderRect.Contains(pos.X, py);
        _hoveredExportTransparentBg = !_exportTransparentBgToggleRect.IsEmpty && _exportTransparentBgToggleRect.Contains(pos.X, py);

        if (_draggingUiScaleSlider)
            UpdatePendingUiScaleFromX(pos.X);
    }

    /// <summary>Applique la valeur du slider d'échelle si un drag était en cours, et indique s'il faut persister.</summary>
    public bool HandlePointerReleased(GameSettings settings)
    {
        _isDraggingScrollbar = false;

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
        _scrollTrackPaint.Dispose();
        _scrollThumbPaint.Dispose();
        _labelFont.Dispose();
        _btnFont.Dispose();
    }
}
