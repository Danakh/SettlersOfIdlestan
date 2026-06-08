using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class HardResetPopupRenderer : IDisposable
{
    private const float PopupWidth  = 480;
    private const float PopupHeight = 260;
    private const float BtnWidth    = 200;
    private const float BtnHeight   = 42;
    private const float BtnGap      = 16;

    private readonly LocalizationService _localization;
    private readonly IFileSystemService  _fileSystemService;
    private readonly Action              _onConfirm;

    private readonly PopupChrome _chrome = new();
    private readonly SKPaint _titlePaint   = new() { Color = new SKColor(255, 80, 80),  IsAntialias = true };
    private readonly SKPaint _textPaint    = new() { Color = SKColors.White,             IsAntialias = true };
    private readonly SKPaint _subtlePaint  = new() { Color = new SKColor(180, 180, 190), IsAntialias = true };
    private readonly SKPaint _cancelPaint  = new() { Color = new SKColor(55, 55, 65),    Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmPaint = new() { Color = new SKColor(140, 40, 40),   Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _btnBorder    = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private SKFont _titleFont = new() { Size = 16, Typeface = SkiaFonts.Bold };
    private SKFont _bodyFont  = new() { Size = 13, Typeface = SkiaFonts.Regular };
    private SKFont _btnFont   = new() { Size = 13, Typeface = SkiaFonts.Bold };
    private float  _lastScale = 0f;

    private SKRect _cancelRect  = SKRect.Empty;
    private SKRect _confirmRect = SKRect.Empty;

    private bool _justOpened;
    private bool _disposed;

    public bool IsOpen { get; private set; }

    public HardResetPopupRenderer(
        LocalizationService localization,
        IFileSystemService  fileSystemService,
        Action              onConfirm)
    {
        _localization      = localization;
        _fileSystemService = fileSystemService;
        _onConfirm         = onConfirm;
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

        string title = _localization.Get("hard_reset_title");
        float titleW = _titleFont.MeasureText(title);
        canvas.DrawText(title, x + (popupW - titleW) / 2f, y + 44 * s, _titleFont, _titlePaint);

        string desc = _localization.Get("hard_reset_desc");
        float descW = _bodyFont.MeasureText(desc);
        canvas.DrawText(desc, x + (popupW - descW) / 2f, y + 90 * s, _bodyFont, _subtlePaint);

        float totalBtns = btnW * 2 + btnGap;
        float btnStartX = x + (popupW - totalBtns) / 2f;
        float btnY = y + 160 * s;

        _cancelRect  = new SKRect(btnStartX,                  btnY, btnStartX + btnW,          btnY + btnH);
        _confirmRect = new SKRect(btnStartX + btnW + btnGap,  btnY, btnStartX + totalBtns,     btnY + btnH);

        DrawBtn(canvas, _cancelRect,  _cancelPaint,  _localization.Get("hard_reset_btn_cancel"),  s);
        DrawBtn(canvas, _confirmRect, _confirmPaint, _localization.Get("hard_reset_btn_confirm"), s);
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

        if (_cancelRect.Contains(pos.X, pos.Y))
        {
            IsOpen = false;
            return;
        }

        if (_confirmRect.Contains(pos.X, pos.Y))
        {
            IsOpen = false;
            _ = _fileSystemService.DeleteAuto();
            _onConfirm();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _chrome.Dispose();
        _titlePaint.Dispose();
        _textPaint.Dispose();
        _subtlePaint.Dispose();
        _cancelPaint.Dispose();
        _confirmPaint.Dispose();
        _btnBorder.Dispose();
        _titleFont.Dispose();
        _bodyFont.Dispose();
        _btnFont.Dispose();
        _disposed = true;
    }
}
