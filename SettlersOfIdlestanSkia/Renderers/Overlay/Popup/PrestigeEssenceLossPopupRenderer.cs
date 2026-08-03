using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class PrestigeEssenceLossPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth  => 440;
    protected override float PopupHeight => 220;

    private const float BtnWidth  = 180;
    private const float BtnHeight = 42;
    private const float BtnGap    = 16;

    private readonly LocalizationService _localization;
    private readonly Action<bool>        _onConfirm;

    private readonly SKPaint _titlePaint   = new() { Color = new SKColor(255, 180, 60), IsAntialias = true };
    private readonly SKPaint _cancelPaint  = new() { Color = new SKColor(55,  55, 65),  Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _confirmPaint = new() { Color = new SKColor(140, 90, 20),  Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _cancelRect  = SKRect.Empty;
    private SKRect _confirmRect = SKRect.Empty;
    private int    _essenceLoss;
    private bool   _corrupted;

    public PrestigeEssenceLossPopupRenderer(LocalizationService localization, Action<bool> onConfirm)
    {
        _localization = localization;
        _onConfirm    = onConfirm;
    }

    public void Open(int essenceLoss, bool corrupted)
    {
        _essenceLoss = essenceLoss;
        _corrupted   = corrupted;
        Open();
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;
        float s    = ComputeScale(scale);
        UpdateFonts(s);

        float popupW     = PopupWidth  * s;
        float btnW       = BtnWidth    * s;
        float btnH       = BtnHeight   * s;
        float btnGap     = BtnGap      * s;
        var   popup      = GetCenteredRect(s);
        float totalBtns  = btnW * 2 + btnGap;
        float btnStartX  = popup.Left + (popupW - totalBtns) / 2f;

        DrawBackground(canvas, popup, s);

        string title = _localization.Get("prestige_essence_loss_title");
        SkiaTextUtils.DrawText(canvas, title, popup.Left + popupW / 2f, popup.Top + 44 * s, SKTextAlign.Center, TitleFont, _titlePaint);

        string desc = _localization.GetFormated("prestige_essence_loss_desc", _essenceLoss);
        float  descW = BodyFont!.MeasureText(desc);
        SkiaTextUtils.DrawText(canvas, desc, popup.Left + (popupW - descW) / 2f, popup.Top + 90 * s, BodyFont, SubtlePaint);

        float btnY = popup.Top + 150 * s;
        _cancelRect  = new SKRect(btnStartX,                 btnY, btnStartX + btnW,          btnY + btnH);
        _confirmRect = new SKRect(btnStartX + btnW + btnGap, btnY, btnStartX + totalBtns,      btnY + btnH);

        DrawButton(canvas, _cancelRect,  _cancelPaint,  _localization.Get("prestige_essence_loss_btn_cancel"),  s);
        DrawButton(canvas, _confirmRect, _confirmPaint, _localization.Get("prestige_essence_loss_btn_confirm"), s);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return;

        if (_cancelRect.Contains(pos.X, pos.Y))  { IsOpen = false; return; }
        if (_confirmRect.Contains(pos.X, pos.Y)) { IsOpen = false; _onConfirm(_corrupted); }
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
