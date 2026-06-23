using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

/// Boutons − / + de zoom en bas à droite de l'écran (onglet île uniquement).
public sealed class ZoomControlRenderer : IDisposable
{
    private const float ButtonSize    = 28f;
    private const float ButtonSpacing = 4f;
    private const float Padding       = 10f;

    private readonly InputHandlingService _inputService;
    private readonly UILayoutService _uiLayout;

    public Action? OnZoomIn  { get; set; }
    public Action? OnZoomOut { get; set; }

    private SKSize _canvasSize;
    private float _scale = 1f;
    private SKRect _zoomInRect;
    private SKRect _zoomOutRect;

    private readonly SKPaint _bgPaint     = new() { Color = new SKColor(35, 35, 50, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Color = new SKColor(100, 100, 130), StrokeWidth = 1f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint   = new() { Color = SKColors.White, IsAntialias = true };
    private SKFont _font = new() { Size = 16f, Typeface = SkiaFonts.Bold };

    private bool _disposed;

    public ZoomControlRenderer(InputHandlingService inputService, UILayoutService uiLayout)
    {
        _inputService = inputService;
        _uiLayout     = uiLayout;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize, float scale)
    {
        _canvasSize = canvasSize;
        if (Math.Abs(scale - _scale) > 0.001f)
        {
            _scale = scale;
            _font.Dispose();
            _font = new SKFont { Size = 16f * scale, Typeface = SkiaFonts.Bold };
        }
        RecalcRects();
    }

    private void RecalcRects()
    {
        float s       = _scale;
        float btnSz   = ButtonSize * s;
        float spacing = ButtonSpacing * s;
        float pad     = Padding * s;

        float bottomPad = _uiLayout.IsMobile
            ? (UILayoutService.MobileTabBarHeight + Padding) * s
            : pad;

        float right  = _canvasSize.Width  - pad;
        float bottom = _canvasSize.Height - bottomPad;

        _zoomInRect  = new SKRect(right - btnSz,                  bottom - btnSz, right,                  bottom);
        _zoomOutRect = new SKRect(right - btnSz * 2f - spacing,   bottom - btnSz, right - btnSz - spacing, bottom);
    }

    public void Render(SKCanvas canvas)
    {
        DrawButton(canvas, _zoomOutRect, "-");
        DrawButton(canvas, _zoomInRect,  "+");
    }

    private void DrawButton(SKCanvas canvas, SKRect rect, string label)
    {
        float cr = 4f * _scale;
        canvas.DrawRoundRect(rect, cr, cr, _bgPaint);
        canvas.DrawRoundRect(rect, cr, cr, _borderPaint);
        float textY = rect.MidY + _font.Size / 2f - 2f * _scale;
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, textY, SKTextAlign.Center, _font, _textPaint);
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (_disposed || e.Button != PointerButton.Left) return;
        var pt = e.Position;
        if (_zoomInRect.Contains(pt.X, pt.Y))  { OnZoomIn?.Invoke();  return; }
        if (_zoomOutRect.Contains(pt.X, pt.Y)) { OnZoomOut?.Invoke(); }
    }

    public bool ContainsPoint(SKPoint point) =>
        _zoomInRect.Contains(point.X, point.Y) || _zoomOutRect.Contains(point.X, point.Y);

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _bgPaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _font.Dispose();
        _disposed = true;
    }
}
