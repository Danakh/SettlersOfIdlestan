using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class CorruptSavePopupRenderer : PopupRendererBase
{
    protected override float PopupWidth  => 520;
    protected override float PopupHeight => 320;

    private const float BtnWidth  = 240;
    private const float BtnHeight = 42;
    private const float BtnGap    = 10;

    private readonly LocalizationService _localization;
    private readonly IFileSystemService   _fileSystemService;
    private readonly string               _corruptJson;
    private readonly Action               _onStartFresh;
    private readonly Action               _onQuit;

    private readonly SKPaint _titlePaint   = new() { Color = new SKColor(255, 100, 100), IsAntialias = true };
    private readonly SKPaint _exportPaint  = new() { Color = new SKColor(40,  90,  160), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _newGamePaint = new() { Color = new SKColor(140, 40,  40),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _quitPaint    = new() { Color = new SKColor(55,  55,  65),  Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _exportRect  = SKRect.Empty;
    private SKRect _newGameRect = SKRect.Empty;
    private SKRect _quitRect    = SKRect.Empty;

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

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;
        float s    = ComputeScale(scale);
        UpdateFonts(s);

        float popupW = PopupWidth  * s;
        float popupH = PopupHeight * s;
        float btnW   = BtnWidth    * s;
        float btnH   = BtnHeight   * s;
        float btnGap = BtnGap      * s;
        var   popup  = GetCenteredRect(s);

        DrawBackground(canvas, popup, s);

        string title = _localization.Get("corrupt_save_title");
        float  titleW = TitleFont!.MeasureText(title);
        SkiaTextUtils.DrawText(canvas, title, popup.Left + (popupW - titleW) / 2f, popup.Top + 44 * s, TitleFont, _titlePaint);

        float lineY = popup.Top + 84 * s;
        foreach (var key in new[] { "corrupt_save_line1", "corrupt_save_line2" })
        {
            string line = _localization.Get(key);
            float  lw   = BodyFont!.MeasureText(line);
            SkiaTextUtils.DrawText(canvas, line, popup.Left + (popupW - lw) / 2f, lineY, BodyFont, SubtlePaint);
            lineY += BodyFont.Size * 1.7f;
        }

        float btnX  = popup.Left + (popupW - btnW) / 2f;
        float btn1Y = popup.Top + 148 * s;
        float btn2Y = btn1Y + btnH + btnGap;
        float btn3Y = btn2Y + btnH + btnGap;

        _exportRect  = new SKRect(btnX, btn1Y, btnX + btnW, btn1Y + btnH);
        _newGameRect = new SKRect(btnX, btn2Y, btnX + btnW, btn2Y + btnH);
        _quitRect    = new SKRect(btnX, btn3Y, btnX + btnW, btn3Y + btnH);

        DrawButton(canvas, _exportRect,  _exportPaint,  _localization.Get("corrupt_save_btn_export"),   s);
        DrawButton(canvas, _newGameRect, _newGamePaint, _localization.Get("corrupt_save_btn_new_game"), s);
        DrawButton(canvas, _quitRect,    _quitPaint,    _localization.Get("corrupt_save_btn_quit"),     s);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return;
        if (JustOpened) { JustOpened = false; return; }

        if (_exportRect.Contains(pos.X, pos.Y))
        {
            _ = _fileSystemService.SaveText("sauvegarde_corrompue.json", _corruptJson);
            return;
        }
        if (_newGameRect.Contains(pos.X, pos.Y)) { IsOpen = false; _onStartFresh(); return; }
        if (_quitRect.Contains(pos.X, pos.Y))    { _onQuit(); }
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _titlePaint.Dispose();
        _exportPaint.Dispose();
        _newGamePaint.Dispose();
        _quitPaint.Dispose();
        base.Dispose();
    }
}
