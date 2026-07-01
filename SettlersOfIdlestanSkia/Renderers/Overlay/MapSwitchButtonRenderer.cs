using System;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// Renders the "Surface / Underworld" toggle button and handles its click.
public sealed class MapSwitchButtonRenderer : IDisposable
{
    private readonly LocalizationService _localization;
    private readonly UILayoutService _uiLayout;
    private readonly GameControllerService _gameControllerService;

    private readonly SKPaint _bgPaint         = new() { Color = new SKColor(40, 25, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint     = new() { Color = new SKColor(160, 100, 220), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint       = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _bgDisabledPaint = new() { Color = new SKColor(30, 30, 35), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderDisabledPaint = new() { Color = new SKColor(80, 80, 90), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textDisabledPaint   = new() { Color = new SKColor(100, 100, 110), IsAntialias = true };
    private SKFont _font = new() { Size = 11, Typeface = SkiaFonts.Bold };
    private SKFont _corruptionFont = new() { Size = 9, Typeface = SkiaFonts.Bold };

    private SKRect _buttonRect;
    private SKSize _canvasSize;

    public MapSwitchButtonRenderer(
        LocalizationService localization,
        UILayoutService uiLayout,
        GameControllerService gameControllerService)
    {
        _localization = localization;
        _uiLayout = uiLayout;
        _gameControllerService = gameControllerService;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _font.Dispose();
        _corruptionFont.Dispose();
        _font = new SKFont { Size = 11 * _uiLayout.UiScale, Typeface = SkiaFonts.Bold };
        _corruptionFont = new SKFont { Size = 9 * _uiLayout.UiScale, Typeface = SkiaFonts.Bold };
    }

    public void Render(SKCanvas canvas)
    {
        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null || !worldState.Layers.ContainsKey(LayerState.UnderworldZ)) return;

        float s = _uiLayout.UiScale;
        float btnW = 220f * s, btnH = 34f * s;
        float btnX = (_canvasSize.Width - btnW) / 2f;
        // Menu en haut avec ressources reléguées : la ligne juste sous la barre du haut leur est réservée,
        // le bouton doit donc s'ancrer après toutes les lignes déjà utilisées.
        float btnY = (_uiLayout.ResourcesOnOwnRow ? _uiLayout.SecondRowBottom : _uiLayout.ResourceBarBottom) + 3f * s;
        _buttonRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

        bool accessible = IsUnderworldAccessible(worldState);
        var bgPaint     = accessible ? _bgPaint         : _bgDisabledPaint;
        var borderPaint = accessible ? _borderPaint      : _borderDisabledPaint;
        var textPaint   = accessible ? _textPaint        : _textDisabledPaint;

        canvas.DrawRoundRect(_buttonRect, 5 * s, 5 * s, bgPaint);
        canvas.DrawRoundRect(_buttonRect, 5 * s, 5 * s, borderPaint);

        string label = worldState.CurrentViewedLayer == LayerState.UnderworldZ
            ? _localization.Get("btn_map_surface")
            : _localization.Get("btn_map_underworld");
        SkiaTextUtils.DrawText(canvas, label, _buttonRect.MidX, _buttonRect.MidY - 1f * s, SKTextAlign.Center, _font, textPaint);

        int corruptionLevel = _gameControllerService.MainGameController.PrestigeController.GetCorruptionLevel();
        string corruptionLabel = _localization.GetFormated("map_switch_corruption_level", corruptionLevel);
        SkiaTextUtils.DrawText(canvas, corruptionLabel, _buttonRect.MidX, _buttonRect.MidY + 13f * s, SKTextAlign.Center, _corruptionFont, textPaint);
    }

    /// Returns true if the click was consumed.
    /// <paramref name="onSwitchedToUnderworld"/> is called when the layer switches to underworld.
    public bool HandlePointerPressed(SKPoint point, Action onSwitchedToUnderworld)
    {
        if (_buttonRect == default || !_buttonRect.Contains(point.X, point.Y)) return false;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState?.Layers.ContainsKey(LayerState.UnderworldZ) != true) return false;
        if (!IsUnderworldAccessible(worldState)) return true; // consomme le clic sans action

        worldState.CurrentViewedLayer = worldState.CurrentViewedLayer == LayerState.UnderworldZ
            ? IslandMap.SurfaceLayer
            : LayerState.UnderworldZ;

        if (worldState.CurrentViewedLayer == LayerState.UnderworldZ)
            onSwitchedToUnderworld();

        return true;
    }

    private static bool IsUnderworldAccessible(WorldState worldState)
    {
        var map = worldState.GetMapForZ(LayerState.UnderworldZ);
        return map != null && map.Tiles.Count > 0;
    }

    public void Dispose()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _bgDisabledPaint.Dispose();
        _borderDisabledPaint.Dispose();
        _textDisabledPaint.Dispose();
        _font.Dispose();
        _corruptionFont.Dispose();
    }
}
