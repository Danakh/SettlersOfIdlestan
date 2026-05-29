using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Tasks;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public class TutorialRenderer : IGameRenderer
{
    private readonly ILocalizationService _localization;

    private SKSize _canvasSize;
    private readonly SKFont _titleFont = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont  = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _taskFont  = new() { Size = 12, Typeface = SkiaFonts.Regular };

    private TutorialStep? _step;

    private const float PanelLeft    = 10f;
    private const float PanelWidth   = 230f;
    private const float PanelPadding = 12f;
    private const float TaskMarkerW  = 18f;

    private static readonly SKColor ColorBg         = new(0,   0,   0,   150);
    private static readonly SKColor ColorTitle       = new(255, 215, 0,   230);
    private static readonly SKColor ColorDesc        = new(200, 200, 200, 200);
    private static readonly SKColor ColorTaskPending = new(255, 255, 255, 210);
    private static readonly SKColor ColorTaskDone    = new(120, 220, 120, 180);
    private static readonly SKColor ColorSeparator   = new(255, 255, 255, 50);

    public TutorialRenderer(ILocalizationService localization)
    {
        _localization = localization;
    }

    public void SetStep(TutorialStep? step) => _step = step;

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_step == null) return;

        var mainState = context.GameState as MainGameState;
        var gameRecord = mainState?.GameRecord ?? new GameRecord();
        var runRecord = mainState?.CurrentIslandState?.RunRecord;

        float contentWidth = PanelWidth - PanelPadding * 2;

        string title = _localization.Get(_step.TitleKey);
        string desc  = _localization.Get(_step.DescKey);

        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, contentWidth, _descFont);

        float titleH     = _titleFont.Size + 6f;
        float descH      = descLayout.Lines.Count * _descFont.Spacing;
        float separatorH = 10f;
        float tasksH     = _step.PrimaryTasks.Count() * (_taskFont.Spacing + 2f);
        float panelH     = PanelPadding + titleH + descH + separatorH + tasksH + PanelPadding;
        float panelTop   = _canvasSize.Height / 5f;

        var panelRect = new SKRect(PanelLeft, panelTop, PanelLeft + PanelWidth, panelTop + panelH);

        using var bgPaint = new SKPaint { Color = ColorBg, IsAntialias = true };
        canvas.DrawRoundRect(panelRect, 8f, 8f, bgPaint);

        float x = PanelLeft + PanelPadding;
        float y = panelTop + PanelPadding + _titleFont.Size;

        // Title
        using var titlePaint = new SKPaint { Color = ColorTitle, IsAntialias = true };
        canvas.DrawText(title, x, y, _titleFont, titlePaint);
        y += 6f;

        // Description
        using var descPaint = new SKPaint { Color = ColorDesc, IsAntialias = true };
        SkiaTextUtils.DrawTextLayout(canvas, descLayout, x, y + _descFont.Size, _descFont, descPaint);
        y += descH;

        // Separator
        y += 6f;
        using var sepPaint = new SKPaint { Color = ColorSeparator };
        canvas.DrawLine(x, y, x + contentWidth, y, sepPaint);
        y += 4f;

        // Tasks
        using var pendingPaint = new SKPaint { Color = ColorTaskPending, IsAntialias = true };
        using var donePaint    = new SKPaint { Color = ColorTaskDone,    IsAntialias = true };

        foreach (var task in _step.PrimaryTasks)
        {
            if (task != null)
            {
                y += _taskFont.Size;
                string marker = task.IsCompleted(gameRecord, runRecord) ? "✓" : "☐";
                string taskText = _localization.Get(task.NameKey);
                string taskDescription = _localization.Get(task.DescKey);
                var paint = task.IsCompleted(gameRecord, runRecord) ? donePaint : pendingPaint;

                canvas.DrawText(marker, x, y, _taskFont, paint);
                canvas.DrawText(taskText, x + TaskMarkerW, y, _taskFont, paint);
                // TODO - tooltip with taskDescription
                y += 2f;
            }
        }
    }

    public void Dispose() { }
}
