using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public class TutorialRenderer : IGameRenderer
{
    private readonly ILocalizationService _localization;

    private SKSize _canvasSize;
    private SKFont _titleFont = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private SKFont _bodyFont  = new() { Size = 12, Typeface = SkiaFonts.Regular };

    private const float PanelWidth   = 230f;
    private const float PanelPadding = 12f;
    private const float PanelLeft    = 10f;

    public TutorialRenderer(ILocalizationService localization)
    {
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        string title   = _localization.Get("TODO");
        string task    = _localization.Get("TODO");

        float lineHeight = _bodyFont.Size + 6f;
        float panelHeight = PanelPadding * 2 + _titleFont.Size + 8f + lineHeight;
        float panelTop = _canvasSize.Height / 5f;

        var panelRect = new SKRect(PanelLeft, panelTop, PanelLeft + PanelWidth, panelTop + panelHeight);

        using var bgPaint = new SKPaint { Color = new SKColor(0, 0, 0, 150), IsAntialias = true };
        canvas.DrawRoundRect(panelRect, 8f, 8f, bgPaint);

        float textX = PanelLeft + PanelPadding;
        float y     = panelTop + PanelPadding + _titleFont.Size;

        using var titlePaint = new SKPaint { Color = new SKColor(255, 215, 0, 230), IsAntialias = true };
        canvas.DrawText(title, textX, y, _titleFont, titlePaint);

        y += 8f + _bodyFont.Size;
        using var taskPaint = new SKPaint { Color = new SKColor(255, 255, 255, 210), IsAntialias = true };
        canvas.DrawText(task, textX, y, _bodyFont, taskPaint);
    }

    public void Dispose() { }
}
