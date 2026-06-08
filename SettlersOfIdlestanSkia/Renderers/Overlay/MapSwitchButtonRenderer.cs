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

    private readonly SKPaint _bgPaint     = new() { Color = new SKColor(40, 25, 70), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Color = new SKColor(160, 100, 220), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint   = new() { Color = SKColors.White, IsAntialias = true };
    private SKFont _font = new() { Size = 11, Typeface = SkiaFonts.Bold };

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
        _font = new SKFont { Size = 11 * _uiLayout.UiScale, Typeface = SkiaFonts.Bold };
    }

    public void Render(SKCanvas canvas)
    {
        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState == null || !worldState.Layers.ContainsKey(LayerState.UnderworldZ)) return;

        float s = _uiLayout.UiScale;
        float btnW = 130f * s, btnH = 22f * s;
        float btnX = (_canvasSize.Width - btnW) / 2f;
        float btnY = _uiLayout.ResourceBarBottom + 3f * s;
        _buttonRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

        canvas.DrawRoundRect(_buttonRect, 5 * s, 5 * s, _bgPaint);
        canvas.DrawRoundRect(_buttonRect, 5 * s, 5 * s, _borderPaint);

        string label = worldState.CurrentViewedLayer == LayerState.UnderworldZ
            ? _localization.Get("btn_map_surface")
            : _localization.Get("btn_map_underworld");
        SkiaTextUtils.DrawText(canvas, label, _buttonRect.MidX, _buttonRect.MidY + 4f * s, SKTextAlign.Center, _font, _textPaint);
    }

    /// Returns true if the click was consumed.
    /// <paramref name="onSwitchedToUnderworld"/> is called when the layer switches to underworld.
    public bool HandlePointerPressed(SKPoint point, Action onSwitchedToUnderworld)
    {
        if (_buttonRect == default || !_buttonRect.Contains(point.X, point.Y)) return false;

        var worldState = _gameControllerService.CurrentWorldState;
        if (worldState?.Layers.ContainsKey(LayerState.UnderworldZ) != true) return false;

        worldState.CurrentViewedLayer = worldState.CurrentViewedLayer == LayerState.UnderworldZ
            ? IslandMap.SurfaceLayer
            : LayerState.UnderworldZ;

        if (worldState.CurrentViewedLayer == LayerState.UnderworldZ)
            onSwitchedToUnderworld();

        return true;
    }

    public void Dispose()
    {
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
    }
}
