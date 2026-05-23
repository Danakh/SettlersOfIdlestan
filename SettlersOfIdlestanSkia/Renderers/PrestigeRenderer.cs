using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers;

public sealed class PrestigeRenderer : IDisposable
{
    private const float PopupWidth = 460;
    private const float PopupHeight = 300;
    private const float Padding = 18;
    private const float ButtonHeight = 36;
    private const float CloseSize = 28;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly Action _prestigeRequested;
    private SKSize _canvasSize;
    private SKRect _prestigeButtonRect = SKRect.Empty;
    private SKRect _closeButtonRect = SKRect.Empty;
    private bool _disposed;

    private readonly SKPaint _overlayPaint = new() { Color = new SKColor(0, 0, 0, 120), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _backgroundPaint = new() { Color = new SKColor(24, 24, 30, 245), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _borderPaint = new() { Color = SKColors.Gold, StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _buttonPaint = new() { Color = new SKColor(46, 125, 50), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _mutedTextPaint = new() { Color = new SKColor(190, 190, 195), IsAntialias = true };
    private readonly SKFont _titleFont = new() { Size = 20, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };
    private readonly SKFont _font = new() { Size = 14, Typeface = SKTypeface.FromFamilyName("Arial") };
    private readonly SKFont _boldFont = new() { Size = 14, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

    public bool IsOpen { get; private set; }

    public PrestigeRenderer(GameControllerService gameControllerService, ILocalizationService localization, Action prestigeRequested)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _prestigeRequested = prestigeRequested;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Open() => IsOpen = true;

    public void Close() => IsOpen = false;

    public void Render(SKCanvas canvas)
    {
        if (!IsOpen || _disposed)
            return;

        canvas.DrawRect(new SKRect(0, 0, _canvasSize.Width, _canvasSize.Height), _overlayPaint);

        var popup = GetPopupRect();
        canvas.DrawRoundRect(popup, 10, 10, _backgroundPaint);
        canvas.DrawRoundRect(popup, 10, 10, _borderPaint);

        canvas.DrawText(_localization.Get("prestige_title"), popup.MidX, popup.Top + 30, SKTextAlign.Center, _titleFont, _textPaint);

        _closeButtonRect = new SKRect(popup.Right - Padding - CloseSize, popup.Top + 10, popup.Right - Padding, popup.Top + 10 + CloseSize);
        DrawCloseButton(canvas, _closeButtonRect);

        var controller = _gameControllerService.MainGameController.PrestigeController;
        var sources = controller.GetPrestigePointSources();
        float y = popup.Top + 68;
        foreach (var source in sources)
        {
            canvas.DrawText(_localization.Get(source.LabelKey), popup.Left + Padding, y, _font, _textPaint);
            canvas.DrawText(source.Points.ToString(), popup.Right - Padding, y, SKTextAlign.Right, _boldFont, _textPaint);
            y += 24;
        }

        var total = controller.CalculatePrestigePoints();
        canvas.DrawText(_localization.Get("prestige_total"), popup.Left + Padding, popup.Bottom - 72, _boldFont, _textPaint);
        canvas.DrawText(total.ToString(), popup.Right - Padding, popup.Bottom - 72, SKTextAlign.Right, _boldFont, _textPaint);

        _prestigeButtonRect = new SKRect(popup.MidX - 75, popup.Bottom - Padding - ButtonHeight, popup.MidX + 75, popup.Bottom - Padding);
        canvas.DrawRoundRect(_prestigeButtonRect, 7, 7, _buttonPaint);
        canvas.DrawText(_localization.Get("prestige_action"), _prestigeButtonRect.MidX, _prestigeButtonRect.MidY + 5, SKTextAlign.Center, _boldFont, _textPaint);
    }

    public bool HandlePointerPressed(SKPoint position, PointerButton button)
    {
        if (!IsOpen)
            return false;

        if (button != PointerButton.Left)
            return GetPopupRect().Contains(position.X, position.Y);

        if (_closeButtonRect.Contains(position.X, position.Y))
        {
            Close();
            return true;
        }

        if (_prestigeButtonRect.Contains(position.X, position.Y) && _gameControllerService.MainGameController.PrestigeController.PrestigeIsAvailable())
        {
            _prestigeRequested();
            return true;
        }

        return GetPopupRect().Contains(position.X, position.Y);
    }

    private void DrawCloseButton(SKCanvas canvas, SKRect rect)
    {
        using var closePaint = new SKPaint { Color = new SKColor(90, 50, 50, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(rect, 5, 5, closePaint);
        canvas.DrawText("X", rect.MidX, rect.MidY + 6, SKTextAlign.Center, _boldFont, _textPaint);
    }

    private SKRect GetPopupRect()
    {
        float width = Math.Min(PopupWidth, _canvasSize.Width - 30);
        float height = Math.Min(PopupHeight, _canvasSize.Height - 30);
        float x = (_canvasSize.Width - width) / 2;
        float y = (_canvasSize.Height - height) / 2;
        return new SKRect(x, y, x + width, y + height);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _overlayPaint.Dispose();
        _backgroundPaint.Dispose();
        _borderPaint.Dispose();
        _buttonPaint.Dispose();
        _textPaint.Dispose();
        _mutedTextPaint.Dispose();
        _titleFont.Dispose();
        _font.Dispose();
        _boldFont.Dispose();
        _disposed = true;
    }
}
