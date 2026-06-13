using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class DemoEndPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth    => 480;
    protected override float PopupHeight   => 280;
    protected override float TitleFontSize => 20f;
    protected override float BodyFontSize  => 13f;
    protected override float BtnFontSize   => 14f;

    private const float BtnWidth  = 220;
    private const float BtnHeight = 44;

    private readonly LocalizationService _localization;
    private readonly Action              _onReplay;

    private readonly SKPaint _titlePaint     = new() { Color = new SKColor(255, 200, 50), IsAntialias = true };
    private readonly SKPaint _replayBtnPaint = new() { Color = new SKColor(46, 125, 50),  Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _replayRect = SKRect.Empty;
    private SKRect _closeRect  = SKRect.Empty;

    public DemoEndPopupRenderer(LocalizationService localization, Action onReplay)
    {
        _localization = localization;
        _onReplay     = onReplay;
    }

    public void Render(SKCanvas canvas, SKSize canvasSize, float scale = 1f)
    {
        if (!IsOpen || Disposed) return;
        CanvasSize = canvasSize;
        float s = ComputeScale(scale);
        UpdateFonts(s);

        var popup  = GetCenteredRect(s);
        float popupW = PopupWidth  * s;
        float popupH = PopupHeight * s;
        DrawBackground(canvas, popup, s);

        string title = _localization.Get("demo_end_title");
        float  titleW = TitleFont!.MeasureText(title);
        SkiaTextUtils.DrawText(canvas, title, popup.Left + (popupW - titleW) / 2f, popup.Top + 50 * s, TitleFont, _titlePaint);

        float lineY = popup.Top + 90 * s;
        foreach (var key in new[] { "demo_end_line1", "demo_end_line2" })
        {
            string line = _localization.Get(key);
            float  lw   = BodyFont!.MeasureText(line);
            SkiaTextUtils.DrawText(canvas, line, popup.Left + (popupW - lw) / 2f, lineY, BodyFont, SubtlePaint);
            lineY += BodyFont.Size * 1.8f;
        }

        float btnW = BtnWidth  * s;
        float btnH = BtnHeight * s;
        float btnY = popup.Top + popupH - btnH - 24 * s;
        float btnX = popup.Left + (popupW - btnW) / 2f;
        _replayRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);
        DrawButton(canvas, _replayRect, _replayBtnPaint, _localization.Get("demo_end_replay"), s);

        _closeRect = GetCloseRect(popup, s);
        DrawCloseButton(canvas, _closeRect, s);
    }

    public bool HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return false;
        if (_closeRect.Contains(pos.X, pos.Y))  { Close(); return true; }
        if (_replayRect.Contains(pos.X, pos.Y)) { Close(); _onReplay(); return true; }
        return true;
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _titlePaint.Dispose();
        _replayBtnPaint.Dispose();
        base.Dispose();
    }
}
