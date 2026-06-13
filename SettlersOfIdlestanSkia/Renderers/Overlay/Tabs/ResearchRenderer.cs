using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
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
    private float _topOffset = PlayerResourcesOverlayRenderer.BarHeight + 8f;
    private const float HeaderHeight = 32f;

    private const float MinZoom = 0.4f;
    private const float MaxZoom = 2.5f;
    private const float ZoomStep = 1.12f;
    private const float PanThresholdSq = 16f;
    private const float PanClampMargin = 100f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly InputHandlingService _inputService;

    private SKSize _canvasSize;
    private readonly Dictionary<TechnologyId, SKRect> _nodeRects = new();
    private SKRect _contentBounds;
    private TechnologyId? _hoveredTechId;
    private SKPoint _lastPointerPosition;
    private bool _disposed;
    public bool IsActive { get; set; }

    private float _zoom = 1f;
    private SKPoint _panOffset;
    private bool _pointerDown;
    private bool _isPanning;
    private SKPoint _pressPosition;
    private SKPoint _lastPanMovePosition;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(15, 17, 25, 230), Style = SKPaintStyle.Fill };
    private readonly SKPaint _inactiveNodePaint = new() { Color = new SKColor(55, 55, 65), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _availableNodePaint = new() { Color = new SKColor(30, 60, 110), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _inProgressNodePaint = new() { Color = new SKColor(100, 60, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _completedNodePaint = new() { Color = new SKColor(20, 80, 30), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _progressBarPaint = new() { Color = new SKColor(220, 140, 20), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _nodeBorderPaint = new() { Color = SKColors.SlateGray, StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _availableBorderPaint = new() { Color = SKColors.CornflowerBlue, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _completedBorderPaint = new() { Color = new SKColor(80, 200, 80), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _queuedBorderPaint = new() { Color = new SKColor(255, 200, 50), StrokeWidth = 2.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _queuedTextPaint = new() { Color = new SKColor(255, 220, 80), IsAntialias = true };
    private readonly SKPaint _linePaint = new() { Color = new SKColor(100, 100, 120), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _activeLinePaint = new() { Color = new SKColor(80, 160, 80), StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _textPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _dimTextPaint = new() { Color = new SKColor(150, 150, 160), IsAntialias = true };
    private readonly SKFont _nameFont = new() { Size = 12, Typeface = SkiaFonts.Bold };
    private readonly SKFont _smallFont = new() { Size = 10, Typeface = SkiaFonts.Regular };
    private float _lastUiScale = 0f;
    private SKFont _tooltipFont = new() { Size = 10, Typeface = SkiaFonts.Regular };

    // Layout: column index → row index → TechnologyId
    private static readonly Dictionary<TechnologyId, (int col, int row)> Layout =
        TechnologyDefinitions.All.ToDictionary(t => t.Id, t => (t.Tier, t.Line));

    public ResearchRenderer(GameControllerService gameControllerService, LocalizationService localization, InputHandlingService inputService)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _inputService = inputService;
        _inputService.PointerPressed += HandlePointerPressed;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.PointerReleased += HandlePointerReleased;
        _inputService.ZoomChanged += HandleZoom;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _zoom = 1f;
        _panOffset = SKPoint.Empty;
        _pointerDown = false;
        _isPanning = false;
        ComputeNodeRects(canvasSize);
    }

    private void ComputeNodeRects(SKSize canvasSize)
    {
        _nodeRects.Clear();

        var byCol = Layout.GroupBy(kv => kv.Value.col).OrderBy(g => g.Key).ToList();
        int maxRowsInAnyCol = byCol.Max(g => g.Max(kv => kv.Value.row) + 1);

        float totalTreeHeight = maxRowsInAnyCol * RowSpacing - (RowSpacing - NodeHeight);
        float startY = _topOffset + PanelPadding + (canvasSize.Height - _topOffset - PanelPadding * 2 - totalTreeHeight) / 2f;
        startY = Math.Max(_topOffset + PanelPadding, startY);

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

        if (_nodeRects.Count > 0)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var r in _nodeRects.Values)
            {
                if (r.Left < minX) minX = r.Left;
                if (r.Top < minY) minY = r.Top;
                if (r.Right > maxX) maxX = r.Right;
                if (r.Bottom > maxY) maxY = r.Bottom;
            }
            _contentBounds = new SKRect(minX, minY, maxX, maxY);
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;
        if (context.GameState is not MainGameState) return;
        if (context.UiScale != _lastUiScale)
        {
            _lastUiScale = context.UiScale;
            _tooltipFont.Dispose();
            _tooltipFont = new SKFont { Size = 10 * _lastUiScale, Typeface = SkiaFonts.Regular };
        }

        var ctrl = _gameControllerService.MainGameController.ResearchController;

        float scaled = PlayerResourcesOverlayRenderer.BarHeight * context.UiScale + 8f;
        if (Math.Abs(scaled - _topOffset) > 0.5f)
        {
            _topOffset = scaled;
            ComputeNodeRects(_canvasSize);
        }

        canvas.DrawRect(new SKRect(0, _topOffset, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        // Zoomed content
        canvas.Save();
        canvas.ClipRect(new SKRect(0, _topOffset + HeaderHeight, _canvasSize.Width, _canvasSize.Height));
        canvas.Translate(_panOffset.X, _panOffset.Y);
        canvas.Scale(_zoom);
        DrawLines(canvas, ctrl);
        DrawNodes(canvas, ctrl);
        canvas.Restore();

        // Fixed header (drawn on top so nodes can't overlap it)
        canvas.DrawRect(new SKRect(0, _topOffset, _canvasSize.Width, _topOffset + HeaderHeight), _bgPaint);
        if (_gameControllerService.PlayerCivilization != null)
        {
            double rps = ctrl.GetResearchPointsPerSecond();
            string rpLabel = rps > 0
                ? $"{_localization.Get("research_points_label")}: {ctrl.ResearchPoints} (+{rps.ToString("0.##")}/s)"
                : $"{_localization.Get("research_points_label")}: {ctrl.ResearchPoints}";
            var (investPct, investPs) = ctrl.GetResearchConsumptionInfo();
            if (investPs > 0)
                rpLabel += $"  |  {_localization.Get("research_investment_label")} ({investPct.ToString("0")}%): {investPs.ToString("0.##")}/s";
            SkiaTextUtils.DrawText(canvas, rpLabel, PanelPadding, _topOffset + 24f, _nameFont, _textPaint);
        }

        // Tooltip
        if (_hoveredTechId.HasValue)
        {
            var hoveredTech = TechnologyDefinitions.All.FirstOrDefault(t => t.Id == _hoveredTechId.Value);
            if (hoveredTech != null)
            {
                string desc = _localization.Get(hoveredTech.DescKey);
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, new[] { desc }, _tooltipFont, uiScale: _lastUiScale);
            }
        }
    }

    private void DrawLines(SKCanvas canvas, ResearchController ctrl)
    {
        foreach (var tech in TechnologyDefinitions.All)
        {
            if (!DebugSettings.ShowFullMap && !ctrl.ShouldDisplay(tech.Id)) continue;
            if (!_nodeRects.TryGetValue(tech.Id, out var childRect)) continue;
            foreach (var prereqId in tech.Prerequisites)
            {
                if (!DebugSettings.ShowFullMap && !ctrl.ShouldDisplay(prereqId)) continue;
                if (!_nodeRects.TryGetValue(prereqId, out var prereqRect)) continue;
                bool prereqDone = ctrl.GetStatus(prereqId) == TechnologyStatus.Completed;
                bool childDone = ctrl.GetStatus(tech.Id) != TechnologyStatus.Inactive;
                var linePaint = (prereqDone && childDone) ? _activeLinePaint : _linePaint;
                canvas.DrawLine(prereqRect.Right, prereqRect.MidY, childRect.Left, childRect.MidY, linePaint);
            }
        }
    }

    private void DrawNodes(SKCanvas canvas, ResearchController ctrl)
    {
        foreach (var tech in TechnologyDefinitions.All)
        {
            if (!DebugSettings.ShowFullMap && !ctrl.ShouldDisplay(tech.Id)) continue;
            if (!_nodeRects.TryGetValue(tech.Id, out var rect)) continue;
            var status = ctrl.GetStatus(tech.Id);
            DrawNode(canvas, tech, rect, status, ctrl);
        }
    }

    private void DrawNode(SKCanvas canvas, Technology tech, SKRect rect, TechnologyStatus status, ResearchController ctrl)
    {
        bool isQueued    = ctrl.GetQueuedResearch() == tech.Id;
        bool isDemoLocked = ctrl.IsDemoLocked(tech.Id);

        var bgPaint = status switch
        {
            TechnologyStatus.Completed => _completedNodePaint,
            TechnologyStatus.InProgress => _inProgressNodePaint,
            TechnologyStatus.Available => _availableNodePaint,
            _ => isQueued ? _availableNodePaint : _inactiveNodePaint,
        };
        var borderPaint = isQueued ? _queuedBorderPaint : status switch
        {
            TechnologyStatus.Completed => _completedBorderPaint,
            TechnologyStatus.Available => _availableBorderPaint,
            _ => _nodeBorderPaint,
        };

        canvas.DrawRoundRect(rect, 5, 5, bgPaint);

        if (status == TechnologyStatus.InProgress)
        {
            var (consumed, total) = ctrl.GetResearchProgress(tech.Id);
            float fraction = total > 0 ? (float)consumed / total : 0f;
            var progressRect = new SKRect(rect.Left + 2, rect.Bottom - 6, rect.Left + 2 + (rect.Width - 4) * fraction, rect.Bottom - 2);
            canvas.DrawRect(progressRect, _progressBarPaint);
        }

        canvas.DrawRoundRect(rect, 5, 5, borderPaint);

        var textPaint = (status == TechnologyStatus.Inactive && !isQueued) ? _dimTextPaint : _textPaint;
        string name = _localization.Get(tech.NameKey);
        SkiaTextUtils.DrawText(canvas, name, rect.MidX, rect.Top + 18f, SKTextAlign.Center, _nameFont, textPaint);

        string subText;
        if (isDemoLocked)
        {
            subText = _localization.Get("demo_mode_research_locked");
        }
        else if (status == TechnologyStatus.Completed)
        {
            subText = "✓";
        }
        else if (status == TechnologyStatus.InProgress)
        {
            var (consumed, total) = ctrl.GetResearchProgress(tech.Id);
            subText = $"{consumed}/{total} PR";
        }
        else if (isQueued)
        {
            subText = _localization.Get("research_next_label");
        }
        else
        {
            var (_, total) = ctrl.GetResearchProgress(tech.Id);
            subText = $"{total} PR";
        }
        SkiaTextUtils.DrawText(canvas, subText, rect.MidX, rect.Top + 36f, SKTextAlign.Center, _smallFont, isQueued ? _queuedTextPaint : textPaint);
    }

    // ─── Input handling ──────────────────────────────────────────────────────

    private SKPoint ToContentSpace(SKPoint screen)
        => new((screen.X - _panOffset.X) / _zoom, (screen.Y - _panOffset.Y) / _zoom);

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!IsActive || e.Button != PointerButton.Left) return;
        _pointerDown = true;
        _isPanning = false;
        _pressPosition = e.Position;
        _lastPanMovePosition = e.Position;
    }

    private void HandlePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsActive) { _hoveredTechId = null; return; }
        _lastPointerPosition = e.Position;

        if (_pointerDown)
        {
            float dx = e.Position.X - _pressPosition.X;
            float dy = e.Position.Y - _pressPosition.Y;
            if (!_isPanning && dx * dx + dy * dy > PanThresholdSq)
                _isPanning = true;

            if (_isPanning)
            {
                _panOffset = new SKPoint(
                    _panOffset.X + e.Position.X - _lastPanMovePosition.X,
                    _panOffset.Y + e.Position.Y - _lastPanMovePosition.Y);
                ClampPan();
                _lastPanMovePosition = e.Position;
                _hoveredTechId = null;
                return;
            }
        }

        _hoveredTechId = null;
        var contentPos = ToContentSpace(e.Position);
        var ctrl = _gameControllerService.MainGameController.ResearchController;
        foreach (var (techId, rect) in _nodeRects)
        {
            if (!DebugSettings.ShowFullMap && !ctrl.ShouldDisplay(techId)) continue;
            if (rect.Contains(contentPos.X, contentPos.Y))
            {
                _hoveredTechId = techId;
                return;
            }
        }
    }

    private void HandlePointerReleased(object? sender, PointerEventArgs e)
    {
        if (!IsActive) { _pointerDown = false; _isPanning = false; return; }
        bool wasPanning = _isPanning;
        _pointerDown = false;
        _isPanning = false;

        if (wasPanning || e.Button != PointerButton.Left) return;

        var ctrl = _gameControllerService.MainGameController.ResearchController;
        var contentPos = ToContentSpace(e.Position);
        foreach (var (techId, rect) in _nodeRects)
        {
            if (!DebugSettings.ShowFullMap && !ctrl.ShouldDisplay(techId)) continue;
            if (!rect.Contains(contentPos.X, contentPos.Y)) continue;

            var status = ctrl.GetStatus(techId);
            if (status == TechnologyStatus.InProgress) return;

            if (ctrl.ActiveResearch == null)
            {
                if (status == TechnologyStatus.Available)
                    ctrl.StartResearch(techId);
            }
            else if (ctrl.ActiveResearchConsumed == 0 && status == TechnologyStatus.Available)
            {
                ctrl.StartResearch(techId);
            }
            else
            {
                if (ctrl.GetQueuedResearch() == techId)
                    ctrl.SetQueuedResearch(null);
                else if (ctrl.CanBeQueued(techId))
                    ctrl.SetQueuedResearch(techId);
            }
            return;
        }
    }

    private void HandleZoom(object? sender, ZoomEventArgs e)
    {
        if (!IsActive) return;
        float newZoom = Math.Clamp(_zoom * (e.ZoomDelta > 0 ? ZoomStep : 1f / ZoomStep), MinZoom, MaxZoom);
        float ratio = newZoom / _zoom;
        _panOffset = new SKPoint(
            e.Center.X - (e.Center.X - _panOffset.X) * ratio,
            e.Center.Y - (e.Center.Y - _panOffset.Y) * ratio);
        _zoom = newZoom;
        ClampPan();
    }

    private void ClampPan()
    {
        if (_nodeRects.Count == 0) return;

        // Content bounding box in screen space: screenX = contentX * _zoom + _panOffset.X
        float cL = _contentBounds.Left * _zoom + _panOffset.X;
        float cR = _contentBounds.Right * _zoom + _panOffset.X;
        float cT = _contentBounds.Top * _zoom + _panOffset.Y;
        float cB = _contentBounds.Bottom * _zoom + _panOffset.Y;

        float px = _panOffset.X, py = _panOffset.Y;

        if (cR < PanClampMargin) px += PanClampMargin - cR;
        else if (cL > _canvasSize.Width - PanClampMargin) px -= cL - (_canvasSize.Width - PanClampMargin);

        if (cB < _topOffset + PanClampMargin) py += _topOffset + PanClampMargin - cB;
        else if (cT > _canvasSize.Height - PanClampMargin) py -= cT - (_canvasSize.Height - PanClampMargin);

        _panOffset = new SKPoint(px, py);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _inputService.PointerMoved -= HandlePointerMoved;
        _inputService.PointerPressed -= HandlePointerPressed;
        _inputService.PointerReleased -= HandlePointerReleased;
        _inputService.ZoomChanged -= HandleZoom;
        _bgPaint.Dispose();
        _inactiveNodePaint.Dispose();
        _availableNodePaint.Dispose();
        _inProgressNodePaint.Dispose();
        _completedNodePaint.Dispose();
        _progressBarPaint.Dispose();
        _nodeBorderPaint.Dispose();
        _availableBorderPaint.Dispose();
        _completedBorderPaint.Dispose();
        _queuedBorderPaint.Dispose();
        _queuedTextPaint.Dispose();
        _linePaint.Dispose();
        _activeLinePaint.Dispose();
        _textPaint.Dispose();
        _dimTextPaint.Dispose();
        _nameFont.Dispose();
        _smallFont.Dispose();
        _tooltipFont.Dispose();
        _disposed = true;
    }
}
