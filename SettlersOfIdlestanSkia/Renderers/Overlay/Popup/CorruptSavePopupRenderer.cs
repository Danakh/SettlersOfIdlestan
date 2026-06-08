using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class CorruptSavePopupRenderer : IDisposable
{
    private const float PopupWidth  = 520;
    private const float PopupHeight = 320;
    private const float BtnWidth    = 240;
    private const float BtnHeight   = 42;
    private const float BtnGap      = 10;

    private readonly LocalizationService _localization;
    private readonly IFileSystemService   _fileSystemService;
    private readonly string               _corruptJson;
    private readonly Action               _onStartFresh;
    private readonly Action               _onQuit;

    private readonly PopupChrome _chrome = new();
    private readonly SKPaint _titlePaint    = new() { Color = new SKColor(255, 100, 100), IsAntialias = true };
    private readonly SKPaint _textPaint     = new() { Color = SKColors.White,             IsAntialias = true };
    private readonly SKPaint _subtlePaint   = new() { Color = new SKColor(180, 180, 190), IsAntialias = true };
    private readonly SKPaint _exportPaint   = new() { Color = new SKColor(40,  90,  160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _newGamePaint  = new() { Color = new SKColor(140, 40,  40),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _quitPaint     = new() { Color = new SKColor(55,  55,  65),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnBorder     = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private SKFont  _titleFont = new() { Size = 16, Typeface = SkiaFonts.Bold };
    private SKFont  _bodyFont  = new() { Size = 13, Typeface = SkiaFonts.Regular };
    private SKFont  _btnFont   = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private float   _lastScale = 0f;

    private SKRect _exportRect  = SKRect.Empty;
    private SKRect _newGameRect = SKRect.Empty;
    private SKRect _quitRect    = SKRect.Empty;

    private bool _justOpened;
    private bool _disposed;

    public bool IsOpen { get; private set; }

    public CorruptSavePopupRenderer(
        LocalizationService localization,
        IFileSystemService   fileSystemService,
        string               corruptJson,
        Action               onStartFresh,
        Action               onQuit)
    {
        _localization      = localization;
        _fileSystemService = fileSystemService;
        _corruptJson       = corruptJson;
        _onStartFresh      = onStartFresh;
        _onQuit            = onQuit;
    }

    public void Open()
    {
        IsOpen      = true;
        _justOpened = true;
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || _disposed) return;

        const float margin = 20f;
        float s = Math.Min(scale, Math.Min(
            (canvasSize.Width  - margin) / PopupWidth,
            (canvasSize.Height - margin) / PopupHeight));
        if (s != _lastScale)
        {
            _lastScale = s;
            _titleFont.Dispose(); _titleFont = new SKFont { Size = 16 * s, Typeface = SkiaFonts.Bold };
            _bodyFont.Dispose();  _bodyFont  = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Regular };
            _btnFont.Dispose();   _btnFont   = new SKFont { Size = 13 * s, Typeface = SkiaFonts.Bold };
        }

        float popupW = PopupWidth  * s;
        float popupH = PopupHeight * s;
        float btnW   = BtnWidth    * s;
        float btnH   = BtnHeight   * s;
        float btnGap = BtnGap      * s;

        float x = (canvasSize.Width  - popupW) / 2;
        float y = (canvasSize.Height - popupH) / 2;
        var popup = new SKRect(x, y, x + popupW, y + popupH);

        _chrome.DrawBackground(canvas, popup, canvasSize, s);

        string title = _localization.Get("corrupt_save_title");
        float titleW = _titleFont.MeasureText(title);
        canvas.DrawText(title, x + (popupW - titleW) / 2f, y + 44 * s, _titleFont, _titlePaint);

        float lineY = y + 84 * s;
        foreach (var key in new[] { "corrupt_save_line1", "corrupt_save_line2" })
        {
            string line = _localization.Get(key);
            float lw = _bodyFont.MeasureText(line);
            canvas.DrawText(line, x + (popupW - lw) / 2f, lineY, _bodyFont, _subtlePaint);
            lineY += _bodyFont.Size * 1.7f;
        }

        float btnX = x + (popupW - btnW) / 2f;
        float btn1Y = y + 148 * s;
        float btn2Y = btn1Y + btnH + btnGap;
        float btn3Y = btn2Y + btnH + btnGap;

        _exportRect  = new SKRect(btnX, btn1Y, btnX + btnW, btn1Y + btnH);
        _newGameRect = new SKRect(btnX, btn2Y, btnX + btnW, btn2Y + btnH);
        _quitRect    = new SKRect(btnX, btn3Y, btnX + btnW, btn3Y + btnH);

        DrawBtn(canvas, _exportRect,  _exportPaint,  _localization.Get("corrupt_save_btn_export"),   s);
        DrawBtn(canvas, _newGameRect, _newGamePaint, _localization.Get("corrupt_save_btn_new_game"), s);
        DrawBtn(canvas, _quitRect,    _quitPaint,    _localization.Get("corrupt_save_btn_quit"),     s);
    }

    private void DrawBtn(SKCanvas canvas, SKRect rect, SKPaint fill, string label, float s)
    {
        canvas.DrawRoundRect(rect, 6 * s, 6 * s, fill);
        canvas.DrawRoundRect(rect, 6 * s, 6 * s, _btnBorder);
        float tw = _btnFont.MeasureText(label);
        canvas.DrawText(label,
            rect.Left + (rect.Width - tw) / 2f,
            rect.Top  + (rect.Height + _btnFont.Size) / 2f,
            _btnFont, _textPaint);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || _disposed) return;
        if (_justOpened) { _justOpened = false; return; }

        if (_exportRect.Contains(pos.X, pos.Y))
        {
            _ = _fileSystemService.SaveText("sauvegarde_corrompue.json", _corruptJson);
            return;
        }

        if (_newGameRect.Contains(pos.X, pos.Y))
        {
            IsOpen = false;
            _onStartFresh();
            return;
        }

        if (_quitRect.Contains(pos.X, pos.Y))
        {
            _onQuit();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _chrome.Dispose();
        _titlePaint.Dispose();
        _textPaint.Dispose();
        _subtlePaint.Dispose();
        _exportPaint.Dispose();
        _newGamePaint.Dispose();
        _quitPaint.Dispose();
        _btnBorder.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
        _btnFont.Dispose();
        _disposed = true;
    }
}
