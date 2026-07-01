using SkiaSharp;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Overlay générique affiché quand le joueur doit désigner une cible sur la carte (ville ou hexagone) :
/// Raid, placement de Merveille/Mine la Plus Profonde, sorts ciblés, etc.
/// Assombrit la carte, met en valeur les cibles sélectionnables, et gère la confirmation/annulation.
/// Remplace les anciens RaidTargetRenderer / WonderPlacementRenderer.
/// </summary>
public sealed class TargetSelectionRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly TargetSelectionService _selectionService;
    private readonly InputHandlingService _inputService;
    private readonly CameraService _cameraService;
    private readonly LocalizationService _localization;

    private const float VertexSnapRadius = 30f;
    private const float VertexHighlightRadius = 14f;
    private const float ButtonWidth = 120f;
    private const float ButtonHeight = 38f;
    private const float ButtonMargin = 14f;

    private SKPaint? _dimPaint;
    private SKPaint? _hostileFill;
    private SKPaint? _hostileHoverFill;
    private SKPaint? _friendlyFill;
    private SKPaint? _friendlyHoverFill;
    private SKPaint? _friendlyHexBorder;
    private SKPaint? _hostileHexBorder;
    private SKPaint? _cancelBgPaint;
    private SKPaint? _cancelTextPaint;
    private SKFont? _cancelFont;
    private SKPaint? _titlePaint;
    private SKFont? _titleFont;
    private SKPaint? _hexLabelPaint;
    private SKFont? _hexLabelFont;

    private SKRect _cancelButtonRect = SKRect.Empty;
    private int _currentLayer = IslandMap.SurfaceLayer;
    private bool _disposed;

    public TargetSelectionRenderer(
        TargetSelectionService selectionService,
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
        _hostileFill = new SKPaint { Color = new SKColor(220, 60, 60, 160), Style = SKPaintStyle.Fill, IsAntialias = true };
        _hostileHoverFill = new SKPaint { Color = new SKColor(255, 100, 100, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _friendlyFill = new SKPaint { Color = new SKColor(50, 200, 80, 100), Style = SKPaintStyle.Fill, IsAntialias = true };
        _friendlyHoverFill = new SKPaint { Color = new SKColor(80, 230, 100, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
        _friendlyHexBorder = new SKPaint { Color = new SKColor(50, 200, 80, 220), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        _hostileHexBorder = new SKPaint { Color = new SKColor(220, 60, 60, 220), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
        _cancelBgPaint = new SKPaint { Color = new SKColor(180, 50, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
        _cancelTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _cancelFont = new SKFont { Size = 14, Typeface = SkiaFonts.Bold };
        _titlePaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _titleFont = new SKFont { Size = 16, Typeface = SkiaFonts.Bold };
        _hexLabelPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _hexLabelFont = new SKFont { Size = 10, Typeface = SkiaFonts.Bold };
    }

    /// <summary>Affiche un libellé d'hex sur deux lignes centrées (coupure au premier espace) pour tenir dans l'hexagone.</summary>
    private void DrawHexLabel(SKCanvas canvas, string label, float cx, float cy)
    {
        int splitIndex = label.IndexOf(' ');
        string line1 = splitIndex >= 0 ? label[..splitIndex] : label;
        string line2 = splitIndex >= 0 ? label[(splitIndex + 1)..] : string.Empty;

        float lineHeight = _hexLabelFont!.Size + 2f;
        if (string.IsNullOrEmpty(line2))
        {
            SkiaTextUtils.DrawText(canvas, line1, cx, cy + lineHeight / 2f, SKTextAlign.Center, _hexLabelFont, _hexLabelPaint!);
            return;
        }

        SkiaTextUtils.DrawText(canvas, line1, cx, cy, SKTextAlign.Center, _hexLabelFont, _hexLabelPaint!);
        SkiaTextUtils.DrawText(canvas, line2, cx, cy + lineHeight, SKTextAlign.Center, _hexLabelFont, _hexLabelPaint!);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed || !_selectionService.IsActive) return;

        _currentLayer = context.CurrentLayer;
        bool hostile = _selectionService.Theme == TargetSelectionTheme.Hostile;

        canvas.DrawRect(new SKRect(0, 0, context.CanvasSize.Width, context.CanvasSize.Height), _dimPaint!);

        canvas.Save();
        var matrix = SKMatrix.CreateScaleTranslation(
            context.ZoomLevel, context.ZoomLevel,
            -context.CameraPosition.X * context.ZoomLevel,
            -context.CameraPosition.Y * context.ZoomLevel);
        canvas.SetMatrix(canvas.TotalMatrix.PostConcat(matrix));

        var fill = hostile ? _hostileFill! : _friendlyFill!;
        var hoverFill = hostile ? _hostileHoverFill! : _friendlyHoverFill!;
        foreach (var target in _selectionService.VertexTargets)
        {
            if (target.Z != _currentLayer) continue;
            var pt = VertexToIsland(target);
            bool isHovered = _selectionService.HoveredVertex?.Equals(target) == true;
            canvas.DrawCircle(pt, VertexHighlightRadius, isHovered ? hoverFill : fill);
        }

        var border = hostile ? _hostileHexBorder! : _friendlyHexBorder!;
        foreach (var hex in _selectionService.HexTargets)
        {
            if (hex.Z != _currentLayer) continue;
            var (cx, cy) = AxialToIsland(hex.Q, hex.R);
            var pts = GetHexagonPoints(cx, cy, HexSize);
            using var path = PointsToPath(pts);

            bool isHovered = _selectionService.HoveredHex?.Equals(hex) == true;
            canvas.DrawPath(path, isHovered ? hoverFill : fill);
            canvas.DrawPath(path, border);

            if (_selectionService.HexLabels?.TryGetValue(hex, out var label) == true)
                DrawHexLabel(canvas, label, cx, cy);
        }

        canvas.Restore();

        SkiaTextUtils.DrawText(canvas,
            _localization.Get(_selectionService.TitleKey),
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

        Vertex? hoveredVertex = null;
        float bestDist = VertexSnapRadius;
        foreach (var target in _selectionService.VertexTargets)
        {
            if (target.Z != _currentLayer) continue;
            float dist = SKPoint.Distance(islandPt, VertexToIsland(target));
            if (dist < bestDist) { bestDist = dist; hoveredVertex = target; }
        }
        _selectionService.HoveredVertex = hoveredVertex;

        var (q, r) = IslandToAxial(islandPt.X, islandPt.Y);
        var hoveredHex = new HexCoord(q, r, _currentLayer);
        _selectionService.HoveredHex = _selectionService.HexTargets.Any(h => h.Equals(hoveredHex)) ? hoveredHex : null;
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

        Vertex? bestVertex = null;
        float bestDist = VertexSnapRadius;
        foreach (var target in _selectionService.VertexTargets)
        {
            if (target.Z != _currentLayer) continue;
            float dist = SKPoint.Distance(islandPt, VertexToIsland(target));
            if (dist < bestDist) { bestDist = dist; bestVertex = target; }
        }
        if (bestVertex != null)
        {
            _selectionService.ConfirmVertex(bestVertex);
            return;
        }

        var (q, r) = IslandToAxial(islandPt.X, islandPt.Y);
        var hex = new HexCoord(q, r, _currentLayer);
        if (_selectionService.HexTargets.Any(h => h.Equals(hex)))
            _selectionService.ConfirmHex(hex);
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
        _hostileFill?.Dispose();
        _hostileHoverFill?.Dispose();
        _friendlyFill?.Dispose();
        _friendlyHoverFill?.Dispose();
        _friendlyHexBorder?.Dispose();
        _hostileHexBorder?.Dispose();
        _cancelBgPaint?.Dispose();
        _cancelTextPaint?.Dispose();
        _cancelFont?.Dispose();
        _titlePaint?.Dispose();
        _titleFont?.Dispose();
        _hexLabelPaint?.Dispose();
        _hexLabelFont?.Dispose();
        _disposed = true;
    }
}
