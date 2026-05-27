using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class ResearchRenderer : IGameRenderer
{
    private const float NodeWidth = 140f;
    private const float NodeHeight = 58f;
    private const float ColSpacing = 180f;
    private const float RowSpacing = 76f;
    private const float PanelPadding = 16f;
    private const float TopOffset = PlayerResourcesOverlayRenderer.BarHeight + 8f;

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly InputHandlingService _inputService;

    private SKSize _canvasSize;
    private readonly Dictionary<TechnologyId, SKRect> _nodeRects = new();
    private bool _disposed;
    public bool IsActive { get; set; }

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(15, 17, 25, 230), Style = SKPaintStyle.Fill };
    private readonly SKPaint _inactiveNodePaint = new() { Color = new SKColor(55, 55, 65), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _availableNodePaint = new() { Color = new SKColor(30, 60, 110), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inProgressNodePaint = new() { Color = new SKColor(100, 60, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _completedNodePaint = new() { Color = new SKColor(20, 80, 30), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _progressBarPaint = new() { Color = new SKColor(220, 140, 20), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _nodeBorderPaint = new() { Color = SKColors.SlateGray, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _availableBorderPaint = new() { Color = SKColors.CornflowerBlue, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _completedBorderPaint = new() { Color = new SKColor(80, 200, 80), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _linePaint = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _activeLinePaint = new() { Color = new SKColor(80, 160, 80), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _dimTextPaint = new() { Color = new SKColor(150, 150, 160), IsAntialias = true };
    private readonly SKFont _nameFont = new() { Size = 12, Typeface = SkiaFonts.Bold };
    private readonly SKFont _smallFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    // Layout: column index → row index → TechnologyId
    private static readonly Dictionary<TechnologyId, (int col, int row)> Layout = ComputeLayout();

    public ResearchRenderer(GameControllerService gameControllerService, ILocalizationService localization, InputHandlingService inputService)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _inputService = inputService;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        ComputeNodeRects(canvasSize);
    }

    private void ComputeNodeRects(SKSize canvasSize)
    {
        _nodeRects.Clear();

        // Group techs by column
        var byCol = Layout.GroupBy(kv => kv.Value.col).OrderBy(g => g.Key).ToList();
        int maxRowsInAnyCol = byCol.Max(g => g.Max(kv => kv.Value.row) + 1);

        float totalTreeHeight = maxRowsInAnyCol * RowSpacing - (RowSpacing - NodeHeight);
        float startY = TopOffset + PanelPadding + (canvasSize.Height - TopOffset - PanelPadding * 2 - totalTreeHeight) / 2f;
        startY = Math.Max(TopOffset + PanelPadding, startY);

        int maxCol = byCol.Max(g => g.Key);
        float totalTreeWidth = (maxCol + 1) * NodeWidth + maxCol * (ColSpacing - NodeWidth);
        float startX = PanelPadding + (canvasSize.Width - PanelPadding * 2 - totalTreeWidth) / 2f;
        startX = Math.Max(PanelPadding, startX);

        foreach (var (techId, (col, row)) in Layout)
        {
            float x = startX + col * ColSpacing;
            float y = startY + row * RowSpacing;
            _nodeRects[techId] = new SKRect(x, y, x + NodeWidth, y + NodeHeight);
        }
    }

    private static Dictionary<TechnologyId, (int col, int row)> ComputeLayout()
    {
        var layout = new Dictionary<TechnologyId, (int col, int row)>();

        // Group by depth (column)
        var byDepth = TechnologyDefinitions.All
            .GroupBy(t => TechnologyDefinitions.GetDepth(t.Id))
            .OrderBy(g => g.Key)
            .ToList();

        // For each tech, align rows so a tech is in the same row as its first prerequisite if possible
        var rowAssigned = new Dictionary<TechnologyId, int>();

        foreach (var group in byDepth)
        {
            int col = group.Key;
            var techs = group.ToList();

            // Try to align each tech to the row of its first prereq
            var usedRows = new HashSet<int>();
            int nextFreeRow = 0;

            foreach (var tech in techs)
            {
                int preferredRow = -1;
                if (tech.Prerequisites.Count > 0)
                {
                    var firstPrereq = tech.Prerequisites[0];
                    if (rowAssigned.TryGetValue(firstPrereq, out var prereqRow))
                        preferredRow = prereqRow;
                }

                int assignedRow;
                if (preferredRow >= 0 && !usedRows.Contains(preferredRow))
                {
                    assignedRow = preferredRow;
                }
                else
                {
                    while (usedRows.Contains(nextFreeRow)) nextFreeRow++;
                    assignedRow = nextFreeRow;
                }

                usedRows.Add(assignedRow);
                rowAssigned[tech.Id] = assignedRow;
                layout[tech.Id] = (col, assignedRow);
                if (assignedRow == nextFreeRow) nextFreeRow++;
            }
        }

        return layout;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;

        var ctrl = _gameControllerService.MainGameController.ResearchController;

        // Background
        canvas.DrawRect(new SKRect(0, TopOffset, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        // Research points
        var tree = _gameControllerService.PlayerCivilization?.TechnologyTree;
        if (tree != null)
        {
            string rpLabel = $"{_localization.Get("research_points_label")}: {tree.ResearchPoints}";
            canvas.DrawText(rpLabel, PanelPadding, TopOffset + 24f, _nameFont, _textPaint);
        }

        // Lines between nodes
        foreach (var tech in TechnologyDefinitions.All)
        {
            if (!_nodeRects.TryGetValue(tech.Id, out var childRect)) continue;
            foreach (var prereqId in tech.Prerequisites)
            {
                if (!_nodeRects.TryGetValue(prereqId, out var prereqRect)) continue;
                bool prereqDone = ctrl.GetStatus(prereqId) == TechnologyStatus.Completed;
                bool childDone = ctrl.GetStatus(tech.Id) != TechnologyStatus.Inactive;
                var linePaint = (prereqDone && childDone) ? _activeLinePaint : _linePaint;
                canvas.DrawLine(prereqRect.Right, prereqRect.MidY, childRect.Left, childRect.MidY, linePaint);
            }
        }

        // Nodes
        foreach (var tech in TechnologyDefinitions.All)
        {
            if (!_nodeRects.TryGetValue(tech.Id, out var rect)) continue;
            var status = ctrl.GetStatus(tech.Id);
            DrawNode(canvas, tech, rect, status, ctrl);
        }
    }

    private void DrawNode(SKCanvas canvas, Technology tech, SKRect rect, TechnologyStatus status, ResearchController ctrl)
    {
        var bgPaint = status switch
        {
            TechnologyStatus.Completed => _completedNodePaint,
            TechnologyStatus.InProgress => _inProgressNodePaint,
            TechnologyStatus.Available => _availableNodePaint,
            _ => _inactiveNodePaint,
        };
        var borderPaint = status switch
        {
            TechnologyStatus.Completed => _completedBorderPaint,
            TechnologyStatus.Available => _availableBorderPaint,
            _ => _nodeBorderPaint,
        };

        canvas.DrawRoundRect(rect, 5, 5, bgPaint);

        // Progress bar for InProgress
        if (status == TechnologyStatus.InProgress)
        {
            var (consumed, total) = ctrl.GetResearchProgress(tech.Id);
            float fraction = total > 0 ? (float)consumed / total : 0f;
            var progressRect = new SKRect(rect.Left + 2, rect.Bottom - 6, rect.Left + 2 + (rect.Width - 4) * fraction, rect.Bottom - 2);
            canvas.DrawRect(progressRect, _progressBarPaint);
        }

        canvas.DrawRoundRect(rect, 5, 5, borderPaint);

        // Name
        var textPaint = status == TechnologyStatus.Inactive ? _dimTextPaint : _textPaint;
        string name = _localization.Get(tech.NameKey);
        canvas.DrawText(name, rect.MidX, rect.Top + 18f, SKTextAlign.Center, _nameFont, textPaint);

        // Cost / status
        string subText;
        if (status == TechnologyStatus.Completed)
        {
            subText = "✓";
        }
        else if (status == TechnologyStatus.InProgress)
        {
            var (consumed, total) = ctrl.GetResearchProgress(tech.Id);
            subText = $"{consumed}/{total} PR";
        }
        else
        {
            var (_, total) = ctrl.GetResearchProgress(tech.Id);
            subText = $"{total} PR";
        }
        canvas.DrawText(subText, rect.MidX, rect.Top + 36f, SKTextAlign.Center, _smallFont, textPaint);
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!IsActive || e.Button != PointerButton.Left) return;

        var ctrl = _gameControllerService.MainGameController.ResearchController;
        foreach (var (techId, rect) in _nodeRects)
        {
            if (!rect.Contains(e.Position.X, e.Position.Y)) continue;
            if (ctrl.GetStatus(techId) == TechnologyStatus.Available)
                ctrl.StartResearch(techId);
            return;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerPressed -= HandlePointerPressed;
        _bgPaint.Dispose();
        _inactiveNodePaint.Dispose();
        _availableNodePaint.Dispose();
        _inProgressNodePaint.Dispose();
        _completedNodePaint.Dispose();
        _progressBarPaint.Dispose();
        _nodeBorderPaint.Dispose();
        _availableBorderPaint.Dispose();
        _completedBorderPaint.Dispose();
        _linePaint.Dispose();
        _activeLinePaint.Dispose();
        _textPaint.Dispose();
        _dimTextPaint.Dispose();
        _nameFont.Dispose();
        _smallFont.Dispose();
        _disposed = true;
    }
}
