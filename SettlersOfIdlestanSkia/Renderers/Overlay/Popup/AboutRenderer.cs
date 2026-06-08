using SkiaSharp;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Popup;

public class AboutRenderer : PopupRendererBase, IGameRenderer
{
    private const float BaseHeight     = 180f;
    private const float BaseCorner     = 16f;
    private const float BaseStartY     = 40f;

    private readonly InputHandlingService _inputService;
    private readonly LocalizationService  _localization;

    public AboutRenderer(InputHandlingService inputService, LocalizationService localization)
    {
        _inputService = inputService;
        _localization = localization;
        _inputService.PointerPressed += OnPointerPressed;
    }

    public override void Open()  => IsOpen = true;
    public override void Close() => IsOpen = false;

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (!IsOpen) return;

        const float margin = 20f;
        float s = Math.Min(context.UiScale, (CanvasSize.Height - margin) / BaseHeight);
        UpdateFonts(s);

        float width  = Math.Min(CanvasSize.Width * 0.7f, CanvasSize.Width - margin);
        float height = BaseHeight * s;
        float x      = (CanvasSize.Width  - width)  / 2;
        float y      = (CanvasSize.Height - height) / 2;
        var   rect   = new SKRect(x, y, x + width, y + height);

        DrawBackgroundOnly(canvas, rect, BaseCorner * s);

        string[] lines =
        {
            _localization.Get("about_by"),
            _localization.Get("about_inspired")
        };
        float lineHeight = TitleFont!.Size * 1.7f;
        float startY     = y + BaseStartY * s;
        for (int i = 0; i < lines.Length; i++)
        {
            float textWidth = TitleFont.MeasureText(lines[i]);
            float textX     = x + (width - textWidth) / 2;
            float textY     = startY + i * lineHeight;
            SkiaTextUtils.DrawText(canvas, lines[i], textX, textY, TitleFont, TextPaint);
        }
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (IsOpen) Close();
    }

    public override void Dispose()
    {
        if (Disposed) return;
        _inputService.PointerPressed -= OnPointerPressed;
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
