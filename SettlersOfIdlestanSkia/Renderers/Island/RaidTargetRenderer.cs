using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Overlay affiché quand le joueur choisit une ville ennemie pour un Raid.
/// Assombrit la carte, met en valeur les villes sélectionnables, et gère la confirmation/annulation.
/// </summary>
public sealed class RaidTargetRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly RaidSelectionService _selectionService;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private readonly LocalizationService _localization;

    private const float CitySnapRadius = 30f;
    private const float CityHighlightRadius = 14f;
    private const float ButtonWidth = 120f;
    private const float ButtonHeight = 38f;
    private const float ButtonMargin = 14f;

    private SKPaint? _dimPaint;
    private SKPaint? _highlightPaint;
    private SKPaint? _hoverPaint;
    private SKPaint? _cancelBgPaint;
    private SKPaint? _cancelTextPaint;
    private SKFont? _cancelFont;
    private SKPaint? _titlePaint;
    private SKFont? _titleFont;

    private SKRect _cancelButtonRect = SKRect.Empty;
    private int _currentLayer = IslandMap.SurfaceLayer;
    private bool _disposed;

    public RaidTargetRenderer(
        RaidSelectionService selectionService,
        InputHandlingService inputService,
        CameraService cameraService,
        LocalizationService localization)
    {
        _selectionService = selectionService;
        _inputService = inputService;
        _cameraService = cameraService;
        _localization = localization;

        _inputService.PointerPressed += OnPointerPressed;
        _inputService.PointerMoved += OnPointerMoved;
        _inputService.KeyPressed += OnKeyPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _dimPaint = new SKPaint { Color = new SKColor(0, 0, 0, 140), Style = SKPaintStyle.Fill };
        _highlightPaint = new SKPaint { Color = new SKColor(220, 60, 60, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
        _hoverPaint = new SKPaint { Color = new SKColor(255, 100, 100, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _cancelBgPaint = new SKPaint { Color = new SKColor(180, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
        _cancelTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _cancelFont = new SKFont { Size = 14, Typeface = SkiaFonts.Bold };
        _titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _titleFont = new SKFont { Size = 16, Typeface = SkiaFonts.Bold };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_selectionService.IsActive) return;

        _currentLayer = context.CurrentLayer;

        canvas.DrawRect(new SKRect(0, 0, context.CanvasSize.Width, context.CanvasSize.Height), _dimPaint!);

        canvas.Save();
        var matrix = SKMatrix.CreateScaleTranslation(
            context.ZoomLevel, context.ZoomLevel,
            -context.CameraPosition.X * context.ZoomLevel,
            -context.CameraPosition.Y * context.ZoomLevel);
        canvas.SetMatrix(canvas.TotalMatrix.PostConcat(matrix));

        foreach (var target in _selectionService.SelectableTargets)
        {
            if (target.Z != _currentLayer) continue;
            var pt = VertexToIsland(target);
            bool isHovered = _selectionService.HoveredTarget?.Equals(target) == true;
            canvas.DrawCircle(pt, CityHighlightRadius, isHovered ? _hoverPaint! : _highlightPaint!);
        }

        canvas.Restore();

        SkiaTextUtils.DrawText(canvas,
            _localization.Get("raid_select_city"),
            context.CanvasSize.Width / 2f, 60f,
            SKTextAlign.Center, _titleFont!, _titlePaint!);

        _cancelButtonRect = new SKRect(
            context.CanvasSize.Width - ButtonMargin - ButtonWidth,
            context.CanvasSize.Height - ButtonMargin - ButtonHeight,
            context.CanvasSize.Width - ButtonMargin,
            context.CanvasSize.Height - ButtonMargin);

        canvas.DrawRoundRect(_cancelButtonRect, 7, 7, _cancelBgPaint!);
        SkiaTextUtils.DrawText(canvas, _localization.Get("ui_cancel"), _cancelButtonRect.MidX, _cancelButtonRect.MidY + 6, SKTextAlign.Center, _cancelFont!, _cancelTextPaint!);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_selectionService.IsActive) return;

        var islandPt = ScreenToIsland(e.Position);
        Vertex? hovered = null;
        float bestDist = CitySnapRadius;
        foreach (var target in _selectionService.SelectableTargets)
        {
            if (target.Z != _currentLayer) continue;
            float dist = SKPoint.Distance(islandPt, VertexToIsland(target));
            if (dist < bestDist) { bestDist = dist; hovered = target; }
        }
        _selectionService.HoveredTarget = hovered;
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
        Vertex? best = null;
        float bestDist = CitySnapRadius;
        foreach (var target in _selectionService.SelectableTargets)
        {
            if (target.Z != _currentLayer) continue;
            float dist = SKPoint.Distance(islandPt, VertexToIsland(target));
            if (dist < bestDist) { bestDist = dist; best = target; }
        }
        if (best != null)
            _selectionService.Confirm(best);
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        if (_selectionService.IsActive && e.Key == "Escape")
            _selectionService.Cancel();
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
        _inputService.KeyPressed -= OnKeyPressed;
        _dimPaint?.Dispose();
        _highlightPaint?.Dispose();
        _hoverPaint?.Dispose();
        _cancelBgPaint?.Dispose();
        _cancelTextPaint?.Dispose();
        _cancelFont?.Dispose();
        _titlePaint?.Dispose();
        _titleFont?.Dispose();
        _disposed = true;
    }
}
