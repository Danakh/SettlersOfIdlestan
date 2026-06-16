using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Tabs;

public sealed class PrestigeMapRenderer : IGameRenderer
{
    private const float R = 60f;
    private const float VertexCircleRadius = 10f;
    private static readonly float Sqrt3     = MathF.Sqrt(3f);
    private static readonly float Sqrt3Half = MathF.Sqrt(3f) / 2f;

    private static readonly SKPoint CentralLocal = LocalVertexPos(PrestigeMap.CentralVertex);

    private static readonly SKColor ColorExploit      = new(180, 220, 185);
    private static readonly SKColor ColorExplore      = new(175, 200, 235);
    private static readonly SKColor ColorExpand       = new(235, 205, 155);
    private static readonly SKColor ColorExterminate  = new(230, 175, 175);
    private static readonly SKColor ColorNone         = new(210, 213, 218);


    private const float HeaderHeight = 32f;

    private const float MinZoom = 0.4f;
    private const float MaxZoom = 2.5f;
    private const float ZoomStep = 1.12f;
    private const float PanThresholdSq = 16f;
    private const float PanClampMargin = 80f;

    private readonly GameControllerService _gameControllerService;
    private readonly LocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;

    private SKSize _canvasSize;
    private SKPoint _mapCenter;
    private float _zoom = 1f;
    private float _barH = PlayerResourcesOverlayRenderer.BarHeight;
    private float _maxLocalExtentX;
    private float _maxLocalExtentY;

    private bool _pointerDown;
    private bool _isPanning;
    private SKPoint _pressPosition;
    private SKPoint _lastPanMovePosition;

    private bool _showVertexNames = true;
    private bool _showHexNames = true;

    private SKRect ToggleVertexNamesBtn => new(_canvasSize.Width - 176f, _barH + 5f, _canvasSize.Width - 96f, _barH + 27f);
    private SKRect ToggleHexNamesBtn    => new(_canvasSize.Width - 88f,  _barH + 5f, _canvasSize.Width - 8f,  _barH + 27f);

    private Vertex? _hoveredVertex;
    private HexCoord? _hoveredHex;

    private readonly HashSet<Vertex> _visibleVertices = new();
    private readonly HashSet<HexCoord> _visibleHexes = new();

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(238, 242, 245), Style = SKPaintStyle.Fill };
    private readonly SKPaint _headerBgPaint = new() { Color = new SKColor(15, 17, 25, 210), Style = SKPaintStyle.Fill };
    private readonly SKFont  _headerFont = new() { Size = 12, Typeface = SkiaFonts.Bold };

    private readonly SKPaint _roadPaint = new()
    {
        Color = new SKColor(220, 50, 50),
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 4f,
        StrokeCap = SKStrokeCap.Round,
        IsAntialias = true,
    };

    private readonly SKPaint _hexFillPaint    = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _hexBorderPaint  = new()
    {
        Color = SKColors.Black, StrokeWidth = 2f,
        Style = SKPaintStyle.Stroke, IsAntialias = true,
    };

    private readonly SKPaint _vertexFillPaint   = new() { Style = SKPaintStyle.Fill,   IsAntialias = true };
    private readonly SKPaint _vertexBorderPaint = new()
    {
        Color = SKColors.Black, StrokeWidth = 1.5f,
        Style = SKPaintStyle.Stroke, IsAntialias = true,
    };

    private readonly SKPaint _textBlackPaint  = new() { Color = SKColors.Black, IsAntialias = true };
    private readonly SKPaint _textWhitePaint  = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKPaint _textGreenPaint  = new() { Color = new SKColor(20, 160, 50), IsAntialias = true };
    private readonly SKFont  _labelFont      = new() { Size = 10, Typeface = SkiaFonts.Regular };
    private readonly SKFont  _labelFontBold  = new() { Size = 10, Typeface = SkiaFonts.Bold };
    private readonly SKFont  _checkFont      = new() { Size = 16, Typeface = SkiaFonts.Bold };
    private readonly SKPaint _toggleActivePaint   = new() { Color = new SKColor(60, 100, 180), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _toggleInactivePaint = new() { Color = new SKColor(80, 80, 90),  Style = SKPaintStyle.Fill, IsAntialias = true };

    public PrestigeMapRenderer(
        GameControllerService gameControllerService,
        LocalizationService localization,
        TooltipRenderer tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _tooltipRenderer = tooltipRenderer;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        _mapCenter = new SKPoint(canvasSize.Width / 2f, _barH + (canvasSize.Height - _barH) * 0.45f);
        _zoom = 1f;
        _pointerDown = false;
        _isPanning = false;
        ComputeLocalExtents();
    }

    private void ComputeLocalExtents()
    {
        float maxX = 0f, maxY = 0f;
        var map = PrestigeMapController.DefaultMap;
        foreach (var v in map.Vertices)
        {
            var local = LocalVertexPos(v.Coord);
            float dx = MathF.Abs(local.X - CentralLocal.X);
            float dy = MathF.Abs(local.Y - CentralLocal.Y);
            if (dx > maxX) maxX = dx;
            if (dy > maxY) maxY = dy;
        }
        foreach (var h in map.Hexes)
        {
            var local = LocalHexPos(h.Coord);
            float dx = MathF.Abs(local.X - CentralLocal.X);
            float dy = MathF.Abs(local.Y - CentralLocal.Y);
            if (dx > maxX) maxX = dx;
            if (dy > maxY) maxY = dy;
        }
        // Add hex radius as padding
        _maxLocalExtentX = maxX + R;
        _maxLocalExtentY = maxY + R;
    }

    public void Render(SKCanvas canvas, GameRenderContext context) { }

    public void RenderPrestigeMap(SKCanvas canvas, GameRenderContext context)
    {
        if (_canvasSize == default) return;

        var mainState = context.GameState as MainGameState;
        var prestigeState = mainState?.PrestigeState;
        if (prestigeState == null) return;

        float newBarH = PlayerResourcesOverlayRenderer.BarHeight * context.UiScale;
        if (Math.Abs(newBarH - _barH) > 0.5f)
        {
            _barH = newBarH;
            _mapCenter = new SKPoint(_canvasSize.Width / 2f, _barH + (_canvasSize.Height - _barH) * 0.45f);
        }
        canvas.DrawRect(0f, _barH, _canvasSize.Width, _canvasSize.Height - _barH, _bgPaint);

        UpdateVisibility(prestigeState);

        canvas.Save();
        canvas.ClipRect(new SKRect(0, _barH + HeaderHeight, _canvasSize.Width, _canvasSize.Height));
        DrawHexes(canvas, prestigeState);
        DrawRoads(canvas, prestigeState);
        DrawVertices(canvas, prestigeState);
        canvas.Restore();

        canvas.DrawRect(new SKRect(0, _barH, _canvasSize.Width, _barH + HeaderHeight), _headerBgPaint);
        string ppLabel = $"{_localization.Get("prestige_points_label")}: {prestigeState.PrestigePoints}";
        SkiaTextUtils.DrawText(canvas, ppLabel, 16f, _barH + 24f, _headerFont, _textWhitePaint);
        DrawToggleButtons(canvas);

        if (_hoveredVertex != null)
            BuildVertexTooltip(_hoveredVertex, prestigeState);
        else if (_hoveredHex != null)
            BuildHexTooltip(_hoveredHex, prestigeState);
    }

    private void UpdateVisibility(PrestigeState state)
    {
        var map = PrestigeMapController.DefaultMap;

        _visibleVertices.Clear();
        foreach (var v in map.Vertices)
        {
            if (DebugSettings.ShowFullMap
                || v.Coord.Equals(PrestigeMap.CentralVertex)
                || map.GetNeighbors(v.Coord).Any(n => state.PurchasedVertices.Contains(n.Coord)))
                _visibleVertices.Add(v.Coord);
        }

        _visibleHexes.Clear();
        foreach (var hex in map.Hexes)
        {
            if (DebugSettings.ShowFullMap
                || hex.AdjacentVertices.Any(v => state.PurchasedVertices.Contains(v)))
                _visibleHexes.Add(hex.Coord);
        }
    }

    private void DrawHexes(SKCanvas canvas, PrestigeState state)
    {
        float hexR = R * _zoom;
        foreach (var hex in PrestigeMapController.DefaultMap.Hexes)
        {
            if (!_visibleHexes.Contains(hex.Coord)) continue;
            var pos = ScreenPosHex(hex.Coord);
            bool isHovered = hex.Coord.Equals(_hoveredHex);

            var points = GetHexPoints(pos.X, pos.Y, hexR);
            using var path = PointsToPath(points);

            var color = DomainColor(hex.Domain);
            if (isHovered) color = Brighten(color, 25);

            _hexFillPaint.Color = color;
            canvas.DrawPath(path, _hexFillPaint);
            canvas.DrawPath(path, _hexBorderPaint);

            DrawHexPieChart(canvas, hex, pos, hexR, state);

            if (_showHexNames)
            {
                string name = _localization.Get(hex.LocalizationKey);
                SkiaTextUtils.DrawText(canvas, name, pos.X, pos.Y + 4f, SKTextAlign.Center, _labelFont, _textBlackPaint);
            }
        }
    }

    private void DrawHexPieChart(SKCanvas canvas, PrestigeHex hex, SKPoint center, float hexR, PrestigeState state)
    {
        float outerR = hexR * 0.8f;
        float innerR = hexR * 0.6f;
        const float GapDeg = 3f;
        float cx = center.X, cy = center.Y;

        // Map each adjacent vertex to its hex corner sector (0 = top, clockwise)
        var hexLocalPos = LocalHexPos(hex.Coord);
        var vertexBySector = new Vertex?[6];
        foreach (var v in hex.AdjacentVertices)
        {
            var vLocal = LocalVertexPos(v);
            float dx = vLocal.X - hexLocalPos.X;
            float dy = vLocal.Y - hexLocalPos.Y;
            float angleDeg = MathF.Atan2(dy, dx) * 180f / MathF.PI;
            float normalized = ((angleDeg + 90f) % 360f + 360f) % 360f;
            int sector = (int)MathF.Round(normalized / 60f) % 6;
            vertexBySector[sector] = v;
        }

        for (int i = 0; i < 6; i++)
        {
            var v = vertexBySector[i];
            if (v == null) continue;

            bool purchased = state.PurchasedVertices.Contains(v);
            float arcStart = -120f + i * 60f + GapDeg * 0.5f;
            float arcSweep = 60f - GapDeg;
            float arcEnd   = arcStart + arcSweep;
            float startRad = arcStart * MathF.PI / 180f;
            float endRad   = arcEnd   * MathF.PI / 180f;

            using var slicePath = new SKPath();
            slicePath.MoveTo(cx + innerR * MathF.Cos(startRad), cy + innerR * MathF.Sin(startRad));
            slicePath.LineTo(cx + outerR * MathF.Cos(startRad), cy + outerR * MathF.Sin(startRad));
            slicePath.ArcTo(new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR), arcStart, arcSweep, false);
            slicePath.LineTo(cx + innerR * MathF.Cos(endRad), cy + innerR * MathF.Sin(endRad));
            slicePath.ArcTo(new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR), arcEnd, -arcSweep, false);
            slicePath.Close();

            _hexFillPaint.Color = purchased
                ? new SKColor(255, 210, 50, 230)
                : new SKColor(220, 220, 230, 90);
            canvas.DrawPath(slicePath, _hexFillPaint);
        }
    }

    private static SKColor DomainColor(PrestigeHexDomain domain) => domain switch
    {
        PrestigeHexDomain.Exploit     => ColorExploit,
        PrestigeHexDomain.Explore     => ColorExplore,
        PrestigeHexDomain.Expand      => ColorExpand,
        PrestigeHexDomain.Exterminate => ColorExterminate,
        _                             => ColorNone,
    };

    private void DrawRoads(SKCanvas canvas, PrestigeState state)
    {
        var map = PrestigeMapController.DefaultMap;
        var purchased = state.PurchasedVertices;
        var vertices = map.Vertices;

        for (int i = 0; i < vertices.Count; i++)
        {
            if (!purchased.Contains(vertices[i].Coord)) continue;
            for (int j = i + 1; j < vertices.Count; j++)
            {
                if (!purchased.Contains(vertices[j].Coord)) continue;
                if (vertices[i].Coord.IsAdjacentTo(vertices[j].Coord))
                    canvas.DrawLine(ScreenPosVertex(vertices[i].Coord), ScreenPosVertex(vertices[j].Coord), _roadPaint);
            }
        }
    }

    private void DrawVertices(SKCanvas canvas, PrestigeState state)
    {
        var controller = _gameControllerService.MainGameController.PrestigeMapController;
        bool demoMode  = _gameControllerService.MainGameController.CurrentMainState?.Settings.DemoMode ?? false;
        float vr = VertexCircleRadius * _zoom;

        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            if (!_visibleVertices.Contains(vertex.Coord)) continue;
            var pos = ScreenPosVertex(vertex.Coord);
            bool purchased   = state.PurchasedVertices.Contains(vertex.Coord);
            bool demoLocked  = demoMode && vertex.Cost > 100 && !purchased;
            bool canBuy      = !demoLocked && controller.CanPurchaseVertex(state, vertex.Coord, demoMode);
            bool isHovered   = vertex.Coord.Equals(_hoveredVertex);

            SKColor baseGrey = new(85, 90, 100);
            SKColor fill = demoLocked
                ? new SKColor(30, 30, 40, 220)
                : baseGrey.WithAlpha(canBuy || purchased ? (byte)220 : (byte)130);

            if (isHovered && !purchased && !demoLocked)
                fill = Brighten(baseGrey, 50);

            int dist = vertex.Coord.EdgeDistanceTo(PrestigeMap.CentralVertex);
            var (sides, startAngle) = VertexShape(dist);
            using var shapePath = CreatePolygonPath(pos, vr, sides, startAngle);

            _vertexFillPaint.Color = fill;
            canvas.DrawPath(shapePath, _vertexFillPaint);

            _vertexBorderPaint.StrokeWidth = isHovered ? 2.5f : 1.5f;
            canvas.DrawPath(shapePath, _vertexBorderPaint);

            if (purchased)
                SkiaTextUtils.DrawText(canvas, "✓", pos.X, pos.Y + 6f, SKTextAlign.Center, _checkFont, _textGreenPaint);
            else if (demoLocked)
                SkiaTextUtils.DrawText(canvas, "D", pos.X, pos.Y + 4f, SKTextAlign.Center, _labelFontBold, _textWhitePaint);

            if (_showVertexNames)
            {
                string name = _localization.Get(vertex.LocalizationKey);
                float labelY = IsSouthTip(vertex.Coord) ? pos.Y + 6f : pos.Y + 2f;
                SkiaTextUtils.DrawText(canvas, name, pos.X + vr, labelY, SKTextAlign.Left, _labelFont, _textBlackPaint);
            }
        }
    }

    // ─── Pointer handling ────────────────────────────────────────────────────

    public void HandlePointerMoved(SKPoint position)
    {
        if (position.Y <= _barH + HeaderHeight)
        {
            _hoveredVertex = null;
            _hoveredHex = null;
            return;
        }

        if (_pointerDown)
        {
            float dx = position.X - _pressPosition.X;
            float dy = position.Y - _pressPosition.Y;
            if (!_isPanning && dx * dx + dy * dy > PanThresholdSq)
                _isPanning = true;

            if (_isPanning)
            {
                _mapCenter = new SKPoint(
                    _mapCenter.X + position.X - _lastPanMovePosition.X,
                    _mapCenter.Y + position.Y - _lastPanMovePosition.Y);
                ClampMapCenter();
                _lastPanMovePosition = position;
                _hoveredVertex = null;
                _hoveredHex = null;
                return;
            }
        }

        _hoveredVertex = null;
        _hoveredHex    = null;

        float vr = VertexCircleRadius * _zoom;
        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            if (!_visibleVertices.Contains(vertex.Coord)) continue;
            var pos = ScreenPosVertex(vertex.Coord);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= vr * vr)
            {
                _hoveredVertex = vertex.Coord;
                return;
            }
        }

        float hexR = R * _zoom;
        foreach (var hex in PrestigeMapController.DefaultMap.Hexes)
        {
            if (!_visibleHexes.Contains(hex.Coord)) continue;
            var pos = ScreenPosHex(hex.Coord);
            var pts = GetHexPoints(pos.X, pos.Y, hexR);
            if (IsPointInPolygon(position.X, position.Y, pts))
            {
                _hoveredHex = hex.Coord;
                return;
            }
        }
    }

    public bool HandlePointerPressed(SKPoint position)
    {
        _pointerDown = true;
        _isPanning = false;
        _pressPosition = position;
        _lastPanMovePosition = position;
        return false;
    }

    public void HandlePointerReleased(SKPoint position)
    {
        bool wasPanning = _isPanning;
        _pointerDown = false;
        _isPanning = false;

        if (wasPanning) return;

        if (ToggleVertexNamesBtn.Contains(position))
        {
            _showVertexNames = !_showVertexNames;
            return;
        }
        if (ToggleHexNamesBtn.Contains(position))
        {
            _showHexNames = !_showHexNames;
            return;
        }

        float vr = VertexCircleRadius * _zoom;
        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            if (!_visibleVertices.Contains(vertex.Coord)) continue;
            var pos = ScreenPosVertex(vertex.Coord);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= vr * vr)
            {
                var mainState = _gameControllerService.MainGameController.CurrentMainState;
                if (mainState?.PrestigeState != null)
                {
                    bool demoMode = mainState.Settings.DemoMode;
                    _gameControllerService.MainGameController.PrestigeMapController
                        .PurchaseVertex(mainState.PrestigeState, vertex.Coord, demoMode);
                }
                return;
            }
        }
    }

    public void HandleZoom(ZoomEventArgs e)
    {
        if (_canvasSize == default) return;
        float newZoom = Math.Clamp(_zoom * (e.ZoomDelta > 0 ? ZoomStep : 1f / ZoomStep), MinZoom, MaxZoom);
        float ratio = newZoom / _zoom;
        _mapCenter = new SKPoint(
            e.Center.X - (e.Center.X - _mapCenter.X) * ratio,
            e.Center.Y - (e.Center.Y - _mapCenter.Y) * ratio);
        _zoom = newZoom;
        ClampMapCenter();
    }

    private void ClampMapCenter()
    {
        float extW = _maxLocalExtentX * _zoom;
        float extH = _maxLocalExtentY * _zoom;

        float cx = _mapCenter.X;
        float cy = _mapCenter.Y;

        // Content bounding box must overlap visible area by at least PanClampMargin
        if (cx + extW < PanClampMargin) cx = PanClampMargin - extW;
        else if (cx - extW > _canvasSize.Width - PanClampMargin) cx = _canvasSize.Width - PanClampMargin + extW;

        if (cy + extH < _barH + PanClampMargin) cy = _barH + PanClampMargin - extH;
        else if (cy - extH > _canvasSize.Height - PanClampMargin) cy = _canvasSize.Height - PanClampMargin + extH;

        _mapCenter = new SKPoint(cx, cy);
    }

    // ─── Tooltip builders ────────────────────────────────────────────────────

    private void BuildVertexTooltip(Vertex coord, PrestigeState state)
    {
        var vertex = PrestigeMapController.DefaultMap.GetVertex(coord);
        if (vertex == null) return;

        var lines = new List<string> { _localization.Get(vertex.LocalizationKey), "" };

        foreach (var mod in vertex.Modifiers.Where(m =>
            m.Category != Modifier.ECategory.STARTING_CITY_BUILDING &&
            m.Category != Modifier.ECategory.NEW_CITY_BUILDING &&
            m.Category != Modifier.ECategory.UNLOCK_RESEARCH))
            lines.Add(FormatModifier(mod));

        foreach (var mod in vertex.Modifiers.Where(m => m.Category == Modifier.ECategory.UNLOCK_RESEARCH))
            lines.Add($"{_localization.Get("prestige_tooltip_unlocks_research")}: {UnlockResearchName(mod.SubCategory)}");

        if (vertex.Modifiers.Any(m => m.Category == Modifier.ECategory.UNLOCK_ABYSS))
        {
            int abyssLevel = PrestigeMapController.DefaultMap.Vertices
                .Where(v => state.PurchasedVertices.Contains(v.Coord))
                .SelectMany(v => v.Modifiers)
                .Where(m => m.Category == Modifier.ECategory.UNLOCK_ABYSS)
                .Sum(m => (int)m.Value);
            lines.Add($"{_localization.Get("prestige_tooltip_abyss_access")}: {abyssLevel}/3");
        }

        var startingCityBuildings = vertex.StartingCityBuildings;
        if (startingCityBuildings.Count > 0)
        {
            var names = string.Join(", ", startingCityBuildings
                .Select(b => _localization.Get($"building_{b.ToString().ToLower()}_name")));
            lines.Add($"{_localization.Get("prestige_tooltip_starts_with")}: {names}");
        }

        var newCityBuildings = vertex.NewCityBuildings;
        if (newCityBuildings.Count > 0)
        {
            var names = string.Join(", ", newCityBuildings
                .Select(b => _localization.Get($"building_{b.ToString().ToLower()}_name")));
            lines.Add($"{_localization.Get("prestige_tooltip_new_city_building")}: {names}");
        }

        lines.Add("");

        bool purchased  = state.PurchasedVertices.Contains(coord);
        bool demoMode   = _gameControllerService.MainGameController.CurrentMainState?.Settings.DemoMode ?? false;
        bool demoLocked = demoMode && vertex.Cost > 100 && !purchased;
        if (purchased)
        {
            lines.Add(_localization.Get("prestige_tooltip_purchased"));
        }
        else if (demoLocked)
        {
            lines.Add(_localization.Get("demo_mode_vertex_locked"));
        }
        else
        {
            bool canBuy = _gameControllerService.MainGameController.PrestigeMapController
                .CanPurchaseVertex(state, coord, demoMode);
            if (!canBuy && state.PrestigePoints >= vertex.Cost)
            {
                var neighbors = PrestigeMapController.DefaultMap.GetNeighbors(coord);
                var neighborNames = string.Join(", ", neighbors.Select(n => _localization.Get(n.LocalizationKey)));
                lines.Add($"{_localization.Get("prestige_tooltip_requires")}: {neighborNames}");
            }
            else
            {
                lines.Add($"{_localization.Get("prestige_tooltip_cost")}: {vertex.Cost} pts");
            }
        }

        _tooltipRenderer.SetTooltipLines(lines.ToArray(), ScreenPosVertex(coord));
    }

    private void BuildHexTooltip(HexCoord coord, PrestigeState state)
    {
        var hex = PrestigeMapController.DefaultMap.GetHex(coord);
        if (hex == null) return;

        var lines = new List<string> { _localization.Get(hex.LocalizationKey), "" };

        foreach (var mod in hex.PerVertexModifiers)
            lines.Add($"{FormatModifier(mod)} {_localization.Get("prestige_tooltip_per_vertex")}");

        if (hex.StartingResourceBonusPerVertex > 0)
            lines.Add($"+{hex.StartingResourceBonusPerVertex} {_localization.Get("prestige_tooltip_resources_per_vertex")}");

        int adjCount = hex.AdjacentVertices.Count(v => state.PurchasedVertices.Contains(v));
        lines.Add("");

        if (adjCount > 0)
        {
            if (hex.PerVertexModifiers.Count > 0)
            {
                foreach (var mod in hex.PerVertexModifiers)
                {
                    double total = mod.Value * adjCount;
                    bool isPct = mod.Category is Modifier.ECategory.HARVEST_SPEED
                        or Modifier.ECategory.RESEARCH_SPEED
                        or Modifier.ECategory.UNIT_PRODUCTION_SPEED
                        or Modifier.ECategory.RESEARCH_COST_REDUCTION
                        or Modifier.ECategory.MARKET_GOLD_SPEED;
                    bool isFloat = mod.Category is Modifier.ECategory.TRADE_GOLD_PACKAGES;
                    string totalStr = isPct ? $"+{(int)(total * 100)}%" : isFloat ? $"{total:0.##}" : $"+{(int)total}";
                    lines.Add($"{_localization.Get("prestige_tooltip_current_bonus")}: {totalStr}");
                    lines.Add($"({FormatModifier(mod)} × {adjCount})");
                }
            }
            else if (hex.StartingResourceBonusPerVertex > 0)
            {
                lines.Add($"{_localization.Get("prestige_tooltip_current_bonus")}: +{hex.StartingResourceBonusPerVertex * adjCount}");
                lines.Add($"(+{hex.StartingResourceBonusPerVertex} × {adjCount})");
            }
        }

        _tooltipRenderer.SetTooltipLines(lines.ToArray(), ScreenPosHex(coord));
    }

    private string UnlockResearchName(string subCategory)
    {
        if (Enum.TryParse<TechnologyId>(subCategory, out var techId))
        {
            var tech = TechnologyDefinitions.Get(techId);
            if (tech != null) return _localization.Get(tech.NameKey);
        }
        return subCategory;
    }

    private string FormatModifier(Modifier mod) => mod.Category switch
    {
        Modifier.ECategory.BUILDING_MAX_LEVEL => string.IsNullOrEmpty(mod.SubCategory)
            ? $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_max_level")}"
            : $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_max_level")} — {_localization.Get($"building_{mod.SubCategory.ToLower()}_name")}",
        Modifier.ECategory.HARVEST_SPEED            => string.IsNullOrEmpty(mod.SubCategory)
            ? $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_harvest_speed")}"
            : $"+{(int)(mod.Value * 100)}% {_localization.Get($"building_{mod.SubCategory.ToLower()}_name")} {_localization.Get("prestige_tooltip_harvest_speed")}",
        Modifier.ECategory.RESEARCH_SPEED           => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_research_speed")}",
        Modifier.ECategory.UNIT_PRODUCTION_SPEED    => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_unit_speed")}",
        Modifier.ECategory.RESEARCH_COST_REDUCTION  => $"-{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_research_cost")}",
        Modifier.ECategory.STORAGE_CAPACITY_BASIC    => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_storage_basic")}",
        Modifier.ECategory.STORAGE_CAPACITY_ADVANCED => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_storage_advanced")}",
        Modifier.ECategory.TRADE_GOLD_PACKAGES       => $"{mod.Value:0.##} {_localization.Get("prestige_tooltip_gold_packages")}",
        Modifier.ECategory.CITY_DEFENSE              => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_city_defense")}",
        Modifier.ECategory.CITY_MAX_SOLDIERS_BONUS   => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_city_max_soldiers")}",
        Modifier.ECategory.CITY_DEFENSE_REGEN_SPEED  => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_city_defense_regen")}",
        Modifier.ECategory.BUILDING_PRODUCTION       => string.IsNullOrEmpty(mod.SubCategory)
            ? $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_production")}"
            : $"+{(int)mod.Value} {_localization.Get($"building_{mod.SubCategory.ToLower()}_name")} {_localization.Get("prestige_tooltip_production")}",
        Modifier.ECategory.UNLOCK_RESEARCH           => _localization.Get("prestige_tooltip_unlocks_research"),
        Modifier.ECategory.UNLOCK_MARITIME_ROUTES    => _localization.Get("prestige_tooltip_unlocks_maritime_routes"),
        Modifier.ECategory.UNLOCK_RESEARCH_SYSTEM    => _localization.Get("prestige_tooltip_unlocks_research_system"),
        Modifier.ECategory.UNLOCK_RESEARCH_QUEUE     => _localization.Get("prestige_tooltip_unlocks_research_queue"),
        Modifier.ECategory.UNLOCK_RESOURCE            => $"{_localization.Get("prestige_tooltip_unlocks_resource")} {_localization.Get($"resource_{mod.SubCategory.ToLower()}")}",
        Modifier.ECategory.PRESTIGE_GAIN              => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_prestige_gain")}",
        Modifier.ECategory.SMELTER_SPEED              => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_smelter_speed")}",
        Modifier.ECategory.CITY_ATTACK_RANGE          => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_city_attack_range")}",
        Modifier.ECategory.REINFORCEMENT_RANGE        => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_reinforcement_range")}",
        Modifier.ECategory.PASSIVE_RESOURCE_GENERATION => $"+{(int)mod.Value} {_localization.Get($"resource_{mod.SubCategory.ToLower()}")}{_localization.Get("prestige_tooltip_passive_generation")}",
        Modifier.ECategory.UNLOCK_DEEPEST_MINE        => _localization.Get("prestige_tooltip_unlocks_deepest_mine"),
        Modifier.ECategory.UNDERWORLD_TREASURE_CHANCE_PERCENT => $"+{(int)mod.Value}% {_localization.Get("prestige_tooltip_underworld_treasure")}",
        Modifier.ECategory.MINE_GOLD_CHANCE_PERCENT   => $"+{(int)mod.Value}% {_localization.Get("prestige_tooltip_mine_gold_chance")}",
        Modifier.ECategory.NEW_CITY_BUILDING          => $"{_localization.Get("prestige_tooltip_new_city_building")} {_localization.Get($"building_{mod.SubCategory.ToLower()}_name")}",
        Modifier.ECategory.UNLOCK_MAGIC               => _localization.Get("prestige_tooltip_unlocks_magic"),
        Modifier.ECategory.UNLOCK_ABYSS               => _localization.Get("prestige_tooltip_unlocks_abyss"),
        Modifier.ECategory.RITUAL_MAX_COUNT           => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_ritual_max_count")}",
        Modifier.ECategory.RITUAL_TOTAL_POWER         => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_ritual_total_power")}",
        Modifier.ECategory.RITUAL_UPKEEP_REDUCTION    => $"-{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_ritual_upkeep")}",
        Modifier.ECategory.MAGIC_FEATURE_COUNT        => $"+{(int)mod.Value} {_localization.Get($"prestige_tooltip_magic_feature_{mod.SubCategory.ToLower()}")}",
        Modifier.ECategory.MARKET_GOLD_SPEED               => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_market_gold_speed")}",
        Modifier.ECategory.CITY_DEFENSE_PROTECTS_SOLDIERS => _localization.Get("prestige_tooltip_city_defense_protects_soldiers"),
        Modifier.ECategory.UNLOCK_SEAPORT_AUTOMATION  => _localization.Get("prestige_tooltip_unlocks_seaport_automation"),
        Modifier.ECategory.PRESTIGE_GAIN_PER_SEAPORT_LEVEL4 => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_prestige_per_seaport")}",
        Modifier.ECategory.UNDERWORLD_ROAD_BASE_REDUCTION   => _localization.Get("prestige_tooltip_underworld_road_reduction"),
        Modifier.ECategory.UNLOCK_RAID                      => _localization.Get("prestige_tooltip_unlocks_raid"),
        Modifier.ECategory.SOLDIER_FOOD_FREE_PER_CITY       => $"{(int)mod.Value} {_localization.Get("prestige_tooltip_soldier_food_free_per_city")}",
        _ => $"+{mod.Value}"
    };

    // ─── Toggle buttons ───────────────────────────────────────────────────────

    private void DrawToggleButtons(SKCanvas canvas)
    {
        DrawToggleButton(canvas, ToggleVertexNamesBtn, _localization.Get("prestige_toggle_vertex_names"), _showVertexNames);
        DrawToggleButton(canvas, ToggleHexNamesBtn,    _localization.Get("prestige_toggle_hex_names"),    _showHexNames);
    }

    private void DrawToggleButton(SKCanvas canvas, SKRect rect, string label, bool active)
    {
        canvas.DrawRoundRect(rect, 4f, 4f, active ? _toggleActivePaint : _toggleInactivePaint);
        SkiaTextUtils.DrawText(canvas, label, rect.MidX, rect.MidY + 4f, SKTextAlign.Center, _labelFont, _textWhitePaint);
    }

    // ─── Vertex shape helpers ─────────────────────────────────────────────────

    // Impair → pointe en bas (90°) ; pair → symétrie horizontale
    private static (int sides, float startAngleDeg) VertexShape(int distanceFromCenter) => distanceFromCenter switch
    {
        0 => (3,   90f),    // triangle,  pointe en bas
        1 => (4,  -90f),    // losange,   pointe en haut
        2 => (5,   90f),    // pentagone, pointe en bas
        3 => (6,    0f),    // hexagone,  bord plat en haut
        4 => (7,   90f),    // heptagone, pointe en bas
        5 => (8,  22.5f),   // octogone,  pointe en bas
        6 => (9,   90f),    // nonagone,  pointe en bas
        _ => (10,  18f),    // décagone,  pointe en bas
    };

    private static SKPath CreatePolygonPath(SKPoint center, float radius, int sides, float startAngleDeg)
    {
        var path = new SKPath();
        float step = 360f / sides;
        for (int i = 0; i < sides; i++)
        {
            float rad = (startAngleDeg + i * step) * MathF.PI / 180f;
            var pt = new SKPoint(center.X + radius * MathF.Cos(rad), center.Y + radius * MathF.Sin(rad));
            if (i == 0) path.MoveTo(pt); else path.LineTo(pt);
        }
        path.Close();
        return path;
    }

    // ─── Position helpers ─────────────────────────────────────────────────────

    private static SKPoint LocalHexPos(HexCoord c)
        => new(R * (Sqrt3 * c.Q + Sqrt3Half * c.R), R * 1.5f * c.R);

    private static SKPoint LocalVertexPos(Vertex v)
    {
        var p1 = LocalHexPos(v.Hex1);
        var p2 = LocalHexPos(v.Hex2);
        var p3 = LocalHexPos(v.Hex3);
        return new((p1.X + p2.X + p3.X) / 3f, (p1.Y + p2.Y + p3.Y) / 3f);
    }

    // Central vertex anchored to _mapCenter; zoom applied around that anchor
    private SKPoint ScreenPosVertex(Vertex v)
    {
        var local = LocalVertexPos(v);
        return new(_mapCenter.X + (local.X - CentralLocal.X) * _zoom,
                   _mapCenter.Y + (local.Y - CentralLocal.Y) * _zoom);
    }

    private SKPoint ScreenPosHex(HexCoord c)
    {
        var local = LocalHexPos(c);
        return new(_mapCenter.X + (local.X - CentralLocal.X) * _zoom,
                   _mapCenter.Y + (local.Y - CentralLocal.Y) * _zoom);
    }

    // Un vertex est la pointe sud d'un hex si exactement 1 de ses 3 hex adjacents est au-dessus de lui.
    // (pointe nord : 2 hex centres au-dessus)
    private static bool IsSouthTip(Vertex v)
    {
        var vPos = LocalVertexPos(v);
        int above = 0;
        if (LocalHexPos(v.Hex1).Y < vPos.Y - 0.1f) above++;
        if (LocalHexPos(v.Hex2).Y < vPos.Y - 0.1f) above++;
        if (LocalHexPos(v.Hex3).Y < vPos.Y - 0.1f) above++;
        return above == 1;
    }

    private static SKPoint[] GetHexPoints(float cx, float cy, float size)
    {
        var pts = new SKPoint[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = -MathF.PI / 2f + MathF.PI / 3f * i;
            pts[i] = new SKPoint(cx + size * MathF.Cos(angle), cy + size * MathF.Sin(angle));
        }
        return pts;
    }

    private static SKPath PointsToPath(SKPoint[] pts)
    {
        var path = new SKPath();
        if (pts.Length == 0) return path;
        path.MoveTo(pts[0]);
        for (int i = 1; i < pts.Length; i++) path.LineTo(pts[i]);
        path.Close();
        return path;
    }

    private static bool IsPointInPolygon(float x, float y, SKPoint[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            float xi = poly[i].X, yi = poly[i].Y, xj = poly[j].X, yj = poly[j].Y;
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    private static SKColor Brighten(SKColor c, int amount) => new(
        (byte)Math.Min(255, c.Red   + amount),
        (byte)Math.Min(255, c.Green + amount),
        (byte)Math.Min(255, c.Blue  + amount),
        c.Alpha);

    public void Dispose()
    {
        _bgPaint.Dispose();
        _headerBgPaint.Dispose();
        _headerFont.Dispose();
        _roadPaint.Dispose();
        _hexFillPaint.Dispose();
        _hexBorderPaint.Dispose();
        _vertexFillPaint.Dispose();
        _vertexBorderPaint.Dispose();
        _textBlackPaint.Dispose();
        _textWhitePaint.Dispose();
        _textGreenPaint.Dispose();
        _labelFont.Dispose();
        _labelFontBold.Dispose();
        _checkFont.Dispose();
        _toggleActivePaint.Dispose();
        _toggleInactivePaint.Dispose();
    }
}
