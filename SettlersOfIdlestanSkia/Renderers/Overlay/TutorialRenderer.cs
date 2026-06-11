using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.Tasks;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using SkiaSharp;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public class TutorialRenderer : IGameRenderer
{
    private readonly LocalizationService _localization;
    private readonly InputHandlingService _inputService;
    public UILayoutService? LayoutService { get; set; }
    private bool IsMobile => LayoutService?.IsMobile ?? false;

    private SKSize _canvasSize;
    private readonly SKFont _titleFont    = new() { Size = 14, Typeface = SkiaFonts.Bold };
    private readonly SKFont _descFont     = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private readonly SKFont _taskFont     = new() { Size = 12, Typeface = SkiaFonts.Regular };
    private readonly SKFont _optionalFont = new() { Size = 10, Typeface = SkiaFonts.Regular };
    private float _lastUiScale = 0f;
    private SKFont _tooltipFont = new() { Size = 11, Typeface = SkiaFonts.Regular };

    private TutorialStep? _step;

    private SKPoint _lastPointerPosition;
    private TutorialTask? _hoveredTask;
    private readonly List<(SKRect Rect, TutorialTask Task)> _taskRects = [];

    private const float PanelLeft    = 10f;
    private const float PanelWidth   = 320f;
    private const float PanelPadding = 12f;
    private const float TaskMarkerW  = 18f;

    private static readonly SKColor ColorBg                    = new(0,   0,   0,   150);
    private static readonly SKColor ColorTitle                  = new(255, 215, 0,   230);
    private static readonly SKColor ColorDesc                   = new(200, 200, 200, 200);
    private static readonly SKColor ColorTaskPending            = new(255, 255, 255, 210);
    private static readonly SKColor ColorTaskDone               = new(120, 220, 120, 180);
    private static readonly SKColor ColorTaskSecondaryPending   = new(220, 220, 220, 200);
    private static readonly SKColor ColorTaskSecondaryDone      = new(120, 220, 120, 150);
    private static readonly SKColor ColorProgress               = new(180, 180, 180, 160);
    private static readonly SKColor ColorSeparator              = new(255, 255, 255, 50);

    public TutorialRenderer(LocalizationService localization, InputHandlingService inputService)
    {
        _localization = localization;
        _inputService = inputService;
        _inputService.PointerMoved += HandlePointerMoved;
    }

    public void SetStep(TutorialStep? step) => _step = step;

    public void Initialize(SKSize canvasSize) => _canvasSize = canvasSize;

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.UiScale != _lastUiScale)
        {
            _lastUiScale = context.UiScale;
            _titleFont.Size    = 14 * _lastUiScale;
            _descFont.Size     = 11 * _lastUiScale;
            _taskFont.Size     = 12 * _lastUiScale;
            _optionalFont.Size = 10 * _lastUiScale;
            _tooltipFont.Dispose();
            _tooltipFont = new SKFont { Size = 11 * _lastUiScale, Typeface = SkiaFonts.Regular };
        }
        if (_step == null) return;
        _taskRects.Clear();

        var mainState = context.GameState as MainGameState;
        var gameRecord = mainState?.GameRecord ?? new GameRecord();
        var WorldState = mainState?.CurrentWorldState;
        var runRecord = WorldState?.RunRecord;

        float s            = _lastUiScale;
        float panelLeft    = PanelLeft    * s;
        float panelWidth   = PanelWidth   * s;
        float panelPadding = PanelPadding * s;
        float taskMarkerW  = TaskMarkerW  * s;
        float contentWidth = panelWidth - panelPadding * 2;

        string title = _localization.Get(_step.TitleKey);
        string desc  = _localization.Get(_step.DescKey);

        var descLayout = SkiaTextUtils.MeasureWrappedText(desc, contentWidth, _descFont);

        bool hasSecondary = _step.SecondaryTasks.Count > 0;

        float titleH          = _titleFont.Size + 6f * s;
        float descH           = descLayout.Lines.Count * _descFont.Spacing;
        float separatorH      = 10f * s;
        float primaryTasksH   = _step.PrimaryTasks.Count * (_taskFont.Spacing + 2f * s);
        float secondaryGapH   = hasSecondary ? 8f * s : 0f;
        float secondaryLabelH = hasSecondary ? _optionalFont.Spacing + 2f * s : 0f;
        float secondaryTasksH = _step.SecondaryTasks.Count * (_taskFont.Spacing + 2f * s);
        float panelH          = panelPadding + titleH + descH + separatorH + primaryTasksH + secondaryGapH + secondaryLabelH + secondaryTasksH + panelPadding;
        float bottomMargin    = IsMobile ? (UILayoutService.MobileTabBarHeight + 10f) * s : 10f * s;
        float panelTop        = _canvasSize.Height - panelH - bottomMargin;

        var panelRect = new SKRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelH);

        using var bgPaint = new SKPaint { Color = ColorBg, IsAntialias = true };
        canvas.DrawRoundRect(panelRect, 8f * s, 8f * s, bgPaint);

        float x = panelLeft + panelPadding;
        float y = panelTop + panelPadding + _titleFont.Size;

        // Title
        using var titlePaint = new SKPaint { Color = ColorTitle, IsAntialias = true };
        SkiaTextUtils.DrawText(canvas, title, x, y, _titleFont, titlePaint);
        y += 6f * s;

        // Description
        using var descPaint = new SKPaint { Color = ColorDesc, IsAntialias = true };
        SkiaTextUtils.DrawTextLayout(canvas, descLayout, x, y + _descFont.Size, _descFont, descPaint);
        y += descH;

        // Separator
        y += 6f * s;
        using var sepPaint = new SKPaint { Color = ColorSeparator };
        canvas.DrawLine(x, y, x + contentWidth, y, sepPaint);
        y += 4f * s;

        // Tasks
        using var pendingPaint          = new SKPaint { Color = ColorTaskPending,          IsAntialias = true };
        using var donePaint             = new SKPaint { Color = ColorTaskDone,             IsAntialias = true };
        using var secondaryPendingPaint = new SKPaint { Color = ColorTaskSecondaryPending, IsAntialias = true };
        using var secondaryDonePaint    = new SKPaint { Color = ColorTaskSecondaryDone,    IsAntialias = true };
        using var progressPaint         = new SKPaint { Color = ColorProgress,             IsAntialias = true };

        foreach (var task in _step.PrimaryTasks)
        {
            float taskTop = y;
            y += _taskFont.Size;
            bool done = task.IsCompleted(gameRecord, runRecord, WorldState);
            var taskPaint = done ? donePaint : pendingPaint;
            SkiaTextUtils.DrawText(canvas, done ? "✓" : "☐", x, y, _taskFont, taskPaint);
            string name = _localization.Get(task.NameKey);
            SkiaTextUtils.DrawText(canvas, name, x + taskMarkerW, y, _taskFont, taskPaint);
            DrawProgress(canvas, task, gameRecord, runRecord, WorldState, x + taskMarkerW + _taskFont.MeasureText(name) + 4f * s, y, progressPaint);
            y += 2f * s;
            _taskRects.Add((new SKRect(panelLeft, taskTop, panelLeft + panelWidth, y), task));
        }

        if (hasSecondary)
        {
            y += secondaryGapH;
            using var optionalPaint = new SKPaint { Color = ColorTaskSecondaryPending, IsAntialias = true };
            SkiaTextUtils.DrawText(canvas, _localization.Get("tutorial_optional"), x, y + _optionalFont.Size, _optionalFont, optionalPaint);
            y += secondaryLabelH;

            foreach (var task in _step.SecondaryTasks)
            {
                float taskTop = y;
                y += _taskFont.Size;
                bool done = task.IsCompleted(gameRecord, runRecord, WorldState);
                var taskPaint = done ? secondaryDonePaint : secondaryPendingPaint;
                SkiaTextUtils.DrawText(canvas, done ? "✓" : "☐", x, y, _taskFont, taskPaint);
                string name = _localization.Get(task.NameKey);
                SkiaTextUtils.DrawText(canvas, name, x + taskMarkerW, y, _taskFont, taskPaint);
                DrawProgress(canvas, task, gameRecord, runRecord, WorldState, x + taskMarkerW + _taskFont.MeasureText(name) + 4f * s, y, progressPaint);
                y += 2f * s;
                _taskRects.Add((new SKRect(panelLeft, taskTop, panelLeft + panelWidth, y), task));
            }
        }

        if (_hoveredTask != null)
        {
            string taskDesc = _localization.Get(_hoveredTask.DescKey);
            if (!string.IsNullOrEmpty(taskDesc))
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, [taskDesc], _tooltipFont, uiScale: _lastUiScale);
        }
    }

    private void DrawProgress(SKCanvas canvas, TutorialTask task, GameRecord gameRecord, RunRecord? runRecord, SettlersOfIdlestan.Model.IslandMap.WorldState? WorldState, float x, float y, SKPaint paint)
    {
        if (task.GetProgress == null) return;
        var (current, max) = task.GetProgress(gameRecord, runRecord, WorldState);
        if (max <= 1) return;
        SkiaTextUtils.DrawText(canvas, $"({Math.Min(current, max)}/{max})", x, y, _optionalFont, paint);
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        _lastPointerPosition = e.Position;
        _hoveredTask = null;
        foreach (var (rect, task) in _taskRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredTask = task;
                break;
            }
        }
    }

    public void Dispose()
    {
        _inputService.PointerMoved -= HandlePointerMoved;
    }
}
