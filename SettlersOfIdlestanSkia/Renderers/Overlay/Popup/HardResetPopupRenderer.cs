using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class HardResetPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth  => 480;
    protected override float PopupHeight => 260;

    private const float BtnWidth  = 200;
    private const float BtnHeight = 42;
    private const float BtnGap    = 16;

    private readonly LocalizationService _localization;
    private readonly IFileSystemService  _fileSystemService;
    private readonly Action              _onConfirm;

    private readonly SKPaint _titlePaint   = new() { Color = new SKColor(255, 80, 80),  IsAntialias = true };
    private readonly SKPaint _cancelPaint  = new() { Color = new SKColor(55,  55, 65),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmPaint = new() { Color = new SKColor(140, 40, 40),  Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _cancelRect  = SKRect.Empty;
    private SKRect _confirmRect = SKRect.Empty;

    public HardResetPopupRenderer(
        LocalizationService localization,
        IFileSystemService  fileSystemService,
        Action              onConfirm)
    {
        _localization      = localization;
        _fileSystemService = fileSystemService;
        _onConfirm         = onConfirm;
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;
        float s    = ComputeScale(scale);
        UpdateFonts(s);

        float popupW     = PopupWidth  * s;
        float popupH     = PopupHeight * s;
        float btnW       = BtnWidth    * s;
        float btnH       = BtnHeight   * s;
        float btnGap     = BtnGap      * s;
        var   popup      = GetCenteredRect(s);
        float totalBtns  = btnW * 2 + btnGap;
        float btnStartX  = popup.Left + (popupW - totalBtns) / 2f;

        DrawBackground(canvas, popup, s);

        string title = _localization.Get("hard_reset_title");
        SkiaTextUtils.DrawText(canvas, title, popup.Left + popupW / 2f, popup.Top + 44 * s, SKTextAlign.Center, TitleFont, _titlePaint);

        string desc = _localization.Get("hard_reset_desc");
        float  descW = BodyFont!.MeasureText(desc);
        SkiaTextUtils.DrawText(canvas, desc, popup.Left + (popupW - descW) / 2f, popup.Top + 90 * s, BodyFont, SubtlePaint);

        float btnY = popup.Top + 160 * s;
        _cancelRect  = new SKRect(btnStartX,              btnY, btnStartX + btnW,          btnY + btnH);
        _confirmRect = new SKRect(btnStartX + btnW + btnGap, btnY, btnStartX + totalBtns, btnY + btnH);

        DrawButton(canvas, _cancelRect,  _cancelPaint,  _localization.Get("hard_reset_btn_cancel"),  s);
        DrawButton(canvas, _confirmRect, _confirmPaint, _localization.Get("hard_reset_btn_confirm"), s);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return;

        if (_cancelRect.Contains(pos.X, pos.Y))  { IsOpen = false; return; }
        if (_confirmRect.Contains(pos.X, pos.Y)) { IsOpen = false; _ = _fileSystemService.DeleteAuto(); _onConfirm(); }
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _titlePaint.Dispose();
        _cancelPaint.Dispose();
        _confirmPaint.Dispose();
        base.Dispose();
    }
}
