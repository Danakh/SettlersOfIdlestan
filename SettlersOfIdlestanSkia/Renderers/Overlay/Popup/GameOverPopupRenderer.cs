using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public sealed class GameOverPopupRenderer : PopupRendererBase
{
    protected override float PopupWidth    => 460;
    protected override float PopupHeight   => 240;
    protected override float TitleFontSize => 20f;
    protected override float BtnFontSize   => 14f;

    private const float BtnWidth  = 260;
    private const float BtnHeight = 44;

    private readonly LocalizationService _localization;
    private readonly Action              _onRestart;

    private readonly SKPaint _titlePaint      = new() { Color = new SKColor(220, 80,  80),  IsAntialias = true };
    private readonly SKPaint _restartBtnPaint = new() { Color = new SKColor(60,  110, 180), Style = SKPaintStyle.Fill, IsAntialias = true };

    private SKRect _restartRect = SKRect.Empty;

    public GameOverPopupRenderer(LocalizationService localization, Action onRestart)
    {
        _localization = localization;
        _onRestart    = onRestart;
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
        var   popup  = GetCenteredRect(s);

        DrawBackground(canvas, popup, s);

        string title = _localization.Get("game_over_title");
        float  titleW = TitleFont!.MeasureText(title);
        SkiaTextUtils.DrawText(canvas, title, popup.Left + (popupW - titleW) / 2f, popup.Top + 50 * s, TitleFont, _titlePaint);

        float lineY = popup.Top + 90 * s;
        foreach (var key in new[] { "game_over_line1", "game_over_line2" })
        {
            string line = _localization.Get(key);
            float  lw   = BodyFont!.MeasureText(line);
            SkiaTextUtils.DrawText(canvas, line, popup.Left + (popupW - lw) / 2f, lineY, BodyFont, SubtlePaint);
            lineY += BodyFont.Size * 1.8f;
        }

        float btnX = popup.Left + (popupW - btnW) / 2f;
        float btnY = popup.Top  + popupH - btnH - 28 * s;
        _restartRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);
        DrawButton(canvas, _restartRect, _restartBtnPaint, _localization.Get("game_over_btn_restart"), s);
    }

    public void HandlePointerPressed(SKPoint pos, PointerButton button)
    {
        if (!IsOpen || Disposed) return;
        if (_restartRect.Contains(pos.X, pos.Y))
        {
            IsOpen = false;
            _onRestart();
        }
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _titlePaint.Dispose();
        _restartBtnPaint.Dispose();
        base.Dispose();
    }
}
