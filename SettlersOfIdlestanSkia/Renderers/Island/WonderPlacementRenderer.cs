using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Rendered on top of the normal game board when the player is choosing a hex for the Wonder.
/// Shows a dim overlay, highlights selectable hexes, and provides a Cancel button.
/// </summary>
public sealed class WonderPlacementRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly WonderSelectionService _selectionService;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private readonly ILocalizationService _localization;

    private const float ButtonWidth = 120f;
    private const float ButtonHeight = 38f;
    private const float ButtonMargin = 14f;

    private SKPaint? _dimPaint;
    private SKPaint? _selectableFill;
    private SKPaint? _hoverFill;
    private SKPaint? _hexBorder;
    private SKPaint? _cancelBgPaint;
    private SKPaint? _cancelTextPaint;
    private SKFont? _cancelFont;
    private SKPaint? _titlePaint;
    private SKFont? _titleFont;

    private SKRect _cancelButtonRect = SKRect.Empty;
    private bool _disposed;

    public WonderPlacementRenderer(
        WonderSelectionService selectionService,
        InputHandlingService inputService,
        CameraService cameraService,
        ILocalizationService localization)
    {
        _selectionService = selectionService;
        _inputService = inputService;
        _cameraService = cameraService;
        _localization = localization;

        _inputService.PointerPressed += OnPointerPressed;
        _inputService.PointerMoved += OnPointerMoved;
    }

    public void Initialize(SKSize canvasSize)
    {
        _dimPaint = new SKPaint { Color = new SKColor(0, 0, 0, 140), Style = SKPaintStyle.Fill };
        _selectableFill = new SKPaint { Color = new SKColor(50, 200, 80, 100), Style = SKPaintStyle.Fill, IsAntialias = true };
        _hoverFill = new SKPaint { Color = new SKColor(80, 230, 100, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
        _hexBorder = new SKPaint { Color = new SKColor(50, 200, 80, 220), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        _cancelBgPaint = new SKPaint { Color = new SKColor(180, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
        _cancelTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _cancelFont = new SKFont { Size = 14, Typeface = SkiaFonts.Bold };
        _titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _titleFont = new SKFont { Size = 16, Typeface = SkiaFonts.Bold };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_selectionService.IsActive) return;

        // Semi-transparent dim overlay
        canvas.DrawRect(new SKRect(0, 0, context.CanvasSize.Width, context.CanvasSize.Height), _dimPaint!);

        // Highlight selectable hexes in world space (with camera transform)
        canvas.Save();
        var matrix = SKMatrix.CreateScaleTranslation(
            context.ZoomLevel, context.ZoomLevel,
            -context.CameraPosition.X * context.ZoomLevel,
            -context.CameraPosition.Y * context.ZoomLevel);
        canvas.SetMatrix(canvas.TotalMatrix.PostConcat(matrix));

        foreach (var hex in _selectionService.PlaceableHexes)
        {
            var (cx, cy) = AxialToIsland(hex.Q, hex.R);
            var pts = GetHexagonPoints(cx, cy, HexSize);
            using var path = PointsToPath(pts);

            bool isHovered = _selectionService.HoveredHex?.Equals(hex) == true;
            canvas.DrawPath(path, isHovered ? _hoverFill! : _selectableFill!);
            canvas.DrawPath(path, _hexBorder!);
        }

        canvas.Restore();

        // Title instruction
        canvas.DrawText(
            _localization.Get("wonder_select_hex"),
            context.CanvasSize.Width / 2f, 60f,
            SKTextAlign.Center, _titleFont!, _titlePaint!);

        // Cancel button
        _cancelButtonRect = new SKRect(
            context.CanvasSize.Width - ButtonMargin - ButtonWidth,
            context.CanvasSize.Height - ButtonMargin - ButtonHeight,
            context.CanvasSize.Width - ButtonMargin,
            context.CanvasSize.Height - ButtonMargin);

        canvas.DrawRoundRect(_cancelButtonRect, 7, 7, _cancelBgPaint!);
        canvas.DrawText(_localization.Get("ui_cancel"), _cancelButtonRect.MidX, _cancelButtonRect.MidY + 6, SKTextAlign.Center, _cancelFont!, _cancelTextPaint!);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_selectionService.IsActive) return;

        var islandPt = ScreenToIsland(e.Position);
        var (q, r) = IslandToAxial(islandPt.X, islandPt.Y);
        var hex = new HexCoord(q, r, IslandMap.SurfaceLayer);
        _selectionService.HoveredHex = _selectionService.PlaceableHexes.Any(h => h.Equals(hex)) ? hex : null;
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (!_selectionService.IsActive) return;
        if (e.Button != PointerButton.Left) return;

        if (_cancelButtonRect.Contains(e.Position.X, e.Position.Y))
        {
            _selectionService.Cancel();
            return;
        }

        var islandPt = ScreenToIsland(e.Position);
        var (q, r) = IslandToAxial(islandPt.X, islandPt.Y);
        var hex = new HexCoord(q, r, IslandMap.SurfaceLayer);
        if (_selectionService.PlaceableHexes.Any(h => h.Equals(hex)))
            _selectionService.Confirm(hex);
    }

    private SKPoint ScreenToIsland(SKPoint screen)
    {
        float zoom = _cameraService.ZoomLevel;
        var cam = _cameraService.Position;
        return new SKPoint(screen.X / zoom + cam.X, screen.Y / zoom + cam.Y);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= OnPointerPressed;
        _inputService.PointerMoved -= OnPointerMoved;
        _dimPaint?.Dispose();
        _selectableFill?.Dispose();
        _hoverFill?.Dispose();
        _hexBorder?.Dispose();
        _cancelBgPaint?.Dispose();
        _cancelTextPaint?.Dispose();
        _cancelFont?.Dispose();
        _titlePaint?.Dispose();
        _titleFont?.Dispose();
        _disposed = true;
    }
}
