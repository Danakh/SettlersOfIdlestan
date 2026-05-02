using SkiaSharp;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Affiche un pop-up "À propos" avec trois phrases localisées.
/// </summary>
public class AboutRenderer : IGameRenderer
{
    private bool _isVisible = false;
    private SKPaint _backgroundPaint;
    private SKPaint _borderPaint;
    private SKPaint _textPaint;
    private SKFont _textFont;
    private SKSize _canvasSize;
    private readonly InputHandlingService _inputService;
    private readonly ILocalizationService _localization;

    public bool IsVisible => _isVisible;

    public AboutRenderer(InputHandlingService inputService, ILocalizationService localization)
    {
        _inputService = inputService;
        _localization = localization;
        _inputService.PointerPressed += OnPointerPressed;

        _backgroundPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 220),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        _borderPaint = new SKPaint
        {
            Color = SKColors.Gold,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        _textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        _textFont = new SKFont { Size = 16, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
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
        if (_backgroundPaint == null || _borderPaint == null || _textPaint == null || _textFont == null)
            Initialize(_canvasSize);

        float width = _canvasSize.Width * 0.7f;
        float height = 180;
        float x = (_canvasSize.Width - width) / 2;
        float y = (_canvasSize.Height - height) / 2;
        float cornerRadius = 16;
        var rect = new SKRect(x, y, x + width, y + height);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _backgroundPaint);
        canvas.DrawRoundRect(rect, cornerRadius, cornerRadius, _borderPaint);

        // Affiche les trois phrases localisées
        string[] lines =
        {
            _localization.Get("about_by"),
            _localization.Get("about_inspired"),
            _localization.Get("about_icons")
        };
        float lineHeight = _textFont.Size * 1.7f;
        float startY = y + 40;
        for (int i = 0; i < lines.Length; i++)
        {
            float textWidth = _textFont.MeasureText(lines[i]);
            float textX = x + (width - textWidth) / 2;
            float textY = startY + i * lineHeight;
            canvas.DrawText(lines[i], textX, textY, _textFont, _textPaint);
        }
    }

    private void OnPointerPressed(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (_isVisible)
            Hide();
    }

    public void Dispose()
    {
        _inputService.PointerPressed -= OnPointerPressed;
        _backgroundPaint?.Dispose();
        _borderPaint?.Dispose();
        _textPaint?.Dispose();
        _textFont?.Dispose();
    }
}
