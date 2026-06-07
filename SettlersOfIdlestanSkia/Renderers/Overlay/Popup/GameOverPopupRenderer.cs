using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class GameOverPopupRenderer : IDisposable
{
    private const float PopupWidth  = 460;
    private const float PopupHeight = 240;
    private const float BtnWidth    = 260;
    private const float BtnHeight   = 44;

    private readonly LocalizationService _localization;
    private readonly Action               _onRestart;

    private readonly PopupChrome _chrome       = new();
    private readonly SKPaint _titlePaint       = new() { Color = new SKColor(220, 80,  80),  IsAntialias = true };
    private readonly SKPaint _textPaint        = new() { Color = SKColors.White,              IsAntialias = true };
    private readonly SKPaint _subtlePaint      = new() { Color = new SKColor(180, 180, 190), IsAntialias = true };
    private readonly SKPaint _restartBtnPaint  = new() { Color = new SKColor(60,  110, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnBorder        = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private SKFont  _titleFont = new() { Size = 20, Typeface = SkiaFonts.Bold };
    private SKFont  _bodyFont  = new() { Size = 13, Typeface = SkiaFonts.Regular };
    private SKFont  _btnFont   = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private float   _lastScale = 0f;

    private SKRect _restartRect = SKRect.Empty;
    private bool   _disposed;

    public bool IsOpen { get; private set; }

    public GameOverPopupRenderer(LocalizationService localization, Action onRestart)
    {
        _localization = localization;
        _onRestart    = onRestart;
    }

    public void Open() => IsOpen = true;

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || _disposed) return;

        float s = scale;
        if (s != _lastScale)
        {
            _lastScale = s;
            _titleFont.Dispose(); _titleFont = new SKFont { Size = 20 * s, Typeface = SkiaFonts.Bold };
            _bodyFont.Dispose();  _bodyFont  = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Regular };
            _btnFont.Dispose();   _btnFont   = new SKFont { Size = 14 * s, Typeface = SkiaFonts.Bold };
        }

        float popupW = PopupWidth  * s;
        float popupH = PopupHeight * s;
        float btnW   = BtnWidth    * s;
        float btnH   = BtnHeight   * s;

        float x = (canvasSize.Width  - popupW) / 2;
        float y = (canvasSize.Height - popupH) / 2;
        var popup = new SKRect(x, y, x + popupW, y + popupH);

        _chrome.DrawBackground(canvas, popup, canvasSize, s);

        string title = _localization.Get("game_over_title");
        float titleW = _titleFont.MeasureText(title);
        canvas.DrawText(title, x + (popupW - titleW) / 2f, y + 50 * s, _titleFont, _titlePaint);

        float lineY = y + 90 * s;
        foreach (var key in new[] { "game_over_line1", "game_over_line2" })
        {
            string line = _localization.Get(key);
            float lw = _bodyFont.MeasureText(line);
            canvas.DrawText(line, x + (popupW - lw) / 2f, lineY, _bodyFont, _subtlePaint);
            lineY += _bodyFont.Size * 1.8f;
        }

        float btnX = x + (popupW - btnW) / 2f;
        float btnY = y + popupH - btnH - 28 * s;
        _restartRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

        canvas.DrawRoundRect(_restartRect, 6 * s, 6 * s, _restartBtnPaint);
        canvas.DrawRoundRect(_restartRect, 6 * s, 6 * s, _btnBorder);
        string label = _localization.Get("game_over_btn_restart");
        float lw2 = _btnFont.MeasureText(label);
        canvas.DrawText(label,
            _restartRect.Left + (btnW - lw2) / 2f,
            _restartRect.Top  + (btnH + _btnFont.Size) / 2f,
            _btnFont, _textPaint);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || _disposed) return;

        if (_restartRect.Contains(pos.X, pos.Y))
        {
            IsOpen = false;
            _onRestart();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _chrome.Dispose();
        _titlePaint.Dispose();
        _textPaint.Dispose();
        _subtlePaint.Dispose();
        _restartBtnPaint.Dispose();
        _btnBorder.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
        _btnFont.Dispose();
        _disposed = true;
    }
}
