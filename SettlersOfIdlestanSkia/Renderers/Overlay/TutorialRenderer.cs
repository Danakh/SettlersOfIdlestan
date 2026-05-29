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
    private readonly SKFont _titleFont    = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont     = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _taskFont     = new() { Size = 12, Typeface = SkiaFonts.Regular };
    private readonly SKFont _optionalFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    private TutorialStep? _step;

    private const float PanelLeft    = 10f;
    private const float PanelWidth   = 320f;
    private const float PanelPadding = 12f;
    private const float TaskMarkerW  = 18f;

    private static readonly SKColor ColorBg                    = new(0,   0,   0,   150);
    private static readonly SKColor ColorTitle                  = new(255, 215, 0,   230);
    private static readonly SKColor ColorDesc                   = new(200, 200, 200, 200);
    private static readonly SKColor ColorTaskPending            = new(255, 255, 255, 210);
    private static readonly SKColor ColorTaskDone               = new(120, 220, 120, 180);
    private static readonly SKColor ColorTaskSecondaryPending   = new(180, 180, 180, 140);
    private static readonly SKColor ColorTaskSecondaryDone      = new(100, 180, 100, 130);
    private static readonly SKColor ColorProgress               = new(180, 180, 180, 160);
    private static readonly SKColor ColorSeparator              = new(255, 255, 255, 50);

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
        var islandState = mainState?.CurrentIslandState;
        var runRecord = islandState?.RunRecord;

        float contentWidth = PanelWidth - PanelPadding * 2;

        string title = _localization.Get(_step.TitleKey);
        string desc  = _localization.Get(_step.DescKey);

        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, contentWidth, _descFont);

        bool hasSecondary = _step.SecondaryTasks.Count > 0;

        float titleH          = _titleFont.Size + 6f;
        float descH           = descLayout.Lines.Count * _descFont.Spacing;
        float separatorH      = 10f;
        float primaryTasksH   = _step.PrimaryTasks.Count * (_taskFont.Spacing + 2f);
        float secondaryGapH   = hasSecondary ? 8f : 0f;
        float secondaryLabelH = hasSecondary ? _optionalFont.Spacing + 2f : 0f;
        float secondaryTasksH = _step.SecondaryTasks.Count * (_taskFont.Spacing + 2f);
        float panelH          = PanelPadding + titleH + descH + separatorH + primaryTasksH + secondaryGapH + secondaryLabelH + secondaryTasksH + PanelPadding;
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
        using var pendingPaint          = new SKPaint { Color = ColorTaskPending,          IsAntialias = true };
        using var donePaint             = new SKPaint { Color = ColorTaskDone,             IsAntialias = true };
        using var secondaryPendingPaint = new SKPaint { Color = ColorTaskSecondaryPending, IsAntialias = true };
        using var secondaryDonePaint    = new SKPaint { Color = ColorTaskSecondaryDone,    IsAntialias = true };
        using var progressPaint         = new SKPaint { Color = ColorProgress,             IsAntialias = true };

        foreach (var task in _step.PrimaryTasks)
        {
            y += _taskFont.Size;
            bool done = task.IsCompleted(gameRecord, runRecord, islandState);
            var taskPaint = done ? donePaint : pendingPaint;
            canvas.DrawText(done ? "✓" : "☐", x, y, _taskFont, taskPaint);
            string name = _localization.Get(task.NameKey);
            canvas.DrawText(name, x + TaskMarkerW, y, _taskFont, taskPaint);
            DrawProgress(canvas, task, gameRecord, runRecord, islandState, x + TaskMarkerW + _taskFont.MeasureText(name) + 4f, y, progressPaint);
            y += 2f;
        }

        if (hasSecondary)
        {
            y += secondaryGapH;
            using var optionalPaint = new SKPaint { Color = ColorTaskSecondaryPending, IsAntialias = true };
            canvas.DrawText(_localization.Get("tutorial_optional"), x, y + _optionalFont.Size, _optionalFont, optionalPaint);
            y += secondaryLabelH;

            foreach (var task in _step.SecondaryTasks)
            {
                y += _taskFont.Size;
                bool done = task.IsCompleted(gameRecord, runRecord, islandState);
                var taskPaint = done ? secondaryDonePaint : secondaryPendingPaint;
                canvas.DrawText(done ? "✓" : "☐", x, y, _taskFont, taskPaint);
                string name = _localization.Get(task.NameKey);
                canvas.DrawText(name, x + TaskMarkerW, y, _taskFont, taskPaint);
                DrawProgress(canvas, task, gameRecord, runRecord, islandState, x + TaskMarkerW + _taskFont.MeasureText(name) + 4f, y, progressPaint);
                y += 2f;
            }
        }
    }

    private void DrawProgress(SKCanvas canvas, TutorialTask task, GameRecord gameRecord, RunRecord? runRecord, SettlersOfIdlestan.Model.IslandMap.IslandState? islandState, float x, float y, SKPaint paint)
    {
        if (task.GetProgress == null) return;
        var (current, max) = task.GetProgress(gameRecord, runRecord, islandState);
        if (max <= 1) return;
        canvas.DrawText($"({Math.Min(current, max)}/{max})", x, y, _optionalFont, paint);
    }

    public void Dispose() { }
}
