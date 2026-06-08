using SkiaSharp;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

/// <summary>
/// Affiche un pop-up "À propos" avec trois phrases localisées.
/// </summary>
public class AboutRenderer : IGameRenderer
{
    private const float BaseHeight     = 180f;
    private const float BaseFontSize   = 16f;
    private const float BaseCorner     = 16f;
    private const float BaseStartY     = 40f;
    private const float BaseLineHeight = BaseFontSize * 1.7f;

    private bool _isVisible = false;
    private readonly SKPaint _backgroundPaint;
    private readonly SKPaint _borderPaint;
    private readonly SKPaint _textPaint;
    private SKFont _textFont;
    private float  _lastScale = 0f;
    private SKSize _canvasSize;
    private readonly InputHandlingService _inputService;
    private readonly LocalizationService _localization;

    public bool IsVisible => _isVisible;

    public AboutRenderer(InputHandlingService inputService, LocalizationService localization)
    {
        _inputService = inputService;
        _localization = localization;
        _inputService.PointerPressed += OnPointerPressed;

        _backgroundPaint = new SKPaint { Color = new SKColor(0, 0, 0, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _borderPaint = new SKPaint { Color = SKColors.Gold, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
        _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _textFont = new SKFont { Size = BaseFontSize, Typeface = SkiaFonts.Bold };
    }

    public void Show()
    {
        _isVisible = true;
    }

    public void Hide()
    {
        _isVisible = false;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!_isVisible) return;

        const float margin = 20f;
        float s = Math.Min(context.UiScale, (_canvasSize.Height - margin) / BaseHeight);

        if (s != _lastScale)
        {
            _lastScale = s;
            _textFont.Dispose();
            _textFont = new SKFont { Size = BaseFontSize * s, Typeface = SkiaFonts.Bold };
        }

        float width  = Math.Min(_canvasSize.Width * 0.7f, _canvasSize.Width - margin);
        float height = BaseHeight * s;
        float x = (_canvasSize.Width  - width)  / 2;
        float y = (_canvasSize.Height - height) / 2;
        float cornerRadius = BaseCorner * s;
        var rect = new SKRect(x, y, x + width, y + height);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _borderPaint);

        string[] lines =
        {
            _localization.Get("about_by"),
            _localization.Get("about_inspired")
        };
        float lineHeight = _textFont.Size * 1.7f;
        float startY = y + BaseStartY * s;
        for (int i = 0; i < lines.Length; i++)
        {
            float textWidth = _textFont.MeasureText(lines[i]);
            float textX = x + (width - textWidth) / 2;
            float textY = startY + i * lineHeight;
            SkiaTextUtils.DrawText(canvas, lines[i], textX, textY, _textFont, _textPaint);
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_isVisible)
            Hide();
    }

    public void Dispose()
    {
        _inputService.PointerPressed -= OnPointerPressed;
        _backgroundPaint.Dispose();
        _borderPaint.Dispose();
        _textPaint.Dispose();
        _textFont.Dispose();
        GC.SuppressFinalize(this);
    }
}
