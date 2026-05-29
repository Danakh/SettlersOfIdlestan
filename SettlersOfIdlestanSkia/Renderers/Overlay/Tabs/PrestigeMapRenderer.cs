using SettlersOfIdlestan.Controller.Expand;
using SettlersOfIdlestan.Model.Civilization;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Services.Localization;
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
    // Hex circumradius — same visual scale as the island hexes
    private const float R = 60f;
    private const float VertexCircleRadius = 10f;
    private static readonly float Sqrt3     = MathF.Sqrt(3f);
    private static readonly float Sqrt3Half = MathF.Sqrt(3f) / 2f;

    // Local-space position of the Central vertex (used to offset all other positions)
    private static readonly SKPoint CentralLocal = LocalVertexPos(PrestigeMap.CentralVertex);

    // Gray shades for hex fill: index = number of adjacent purchased vertices (0..3)
    private static readonly SKColor[] HexGrayByActivity =
    {
        new(210, 213, 218),
        new(175, 178, 185),
        new(135, 138, 148),
        new( 95,  98, 110),
    };

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;

    private SKSize _canvasSize;
    private SKPoint _mapCenter;

    private Vertex? _hoveredVertex;
    private HexCoord? _hoveredHex;

    private readonly HashSet<Vertex> _visibleVertices = new();
    private readonly HashSet<HexCoord> _visibleHexes = new();

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(238, 242, 245), Style = SKPaintStyle.Fill };

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

    private readonly SKPaint _textBlackPaint = new() { Color = SKColors.Black, IsAntialias = true };
    private readonly SKPaint _textWhitePaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont  _labelFont     = new() { Size = 10, Typeface = SkiaFonts.Regular };
    private readonly SKFont  _labelFontBold = new() { Size = 10, Typeface = SkiaFonts.Bold };

    public PrestigeMapRenderer(
        GameControllerService gameControllerService,
        ILocalizationService localization,
        TooltipRenderer tooltipRenderer)
    {
        _gameControllerService = gameControllerService;
        _localization = localization;
        _tooltipRenderer = tooltipRenderer;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        float barH = PlayerResourcesOverlayRenderer.BarHeight;
        _mapCenter = new SKPoint(canvasSize.Width / 2f, barH + (canvasSize.Height - barH) * 0.45f);
    }

    public void Render(SKCanvas canvas, GameRenderContext context) { }

    public void RenderPrestigeMap(SKCanvas canvas, GameRenderContext context)
    {
        if (_canvasSize == default) return;

        var mainState = context.GameState as MainGameState;
        var prestigeState = mainState?.PrestigeState;
        if (prestigeState == null) return;

        float barH = PlayerResourcesOverlayRenderer.BarHeight;
        canvas.DrawRect(0f, barH, _canvasSize.Width, _canvasSize.Height - barH, _bgPaint);

        UpdateVisibility(prestigeState);
        DrawHexes(canvas, prestigeState);
        DrawRoads(canvas, prestigeState);
        DrawVertices(canvas, prestigeState);

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
            if (DebugOverlayRenderer.DebugMode
                || v.Coord.Equals(PrestigeMap.CentralVertex)
                || map.GetNeighbors(v.Coord).Any(n => state.PurchasedVertices.Contains(n.Coord)))
                _visibleVertices.Add(v.Coord);
        }

        _visibleHexes.Clear();
        foreach (var hex in map.Hexes)
        {
            if (DebugOverlayRenderer.DebugMode
                || hex.AdjacentVertices.Any(v => state.PurchasedVertices.Contains(v)))
                _visibleHexes.Add(hex.Coord);
        }
    }

    private void DrawHexes(SKCanvas canvas, PrestigeState state)
    {
        foreach (var hex in PrestigeMapController.DefaultMap.Hexes)
        {
            if (!_visibleHexes.Contains(hex.Coord)) continue;
            var pos = ScreenPosHex(hex.Coord);
            int adjCount = hex.AdjacentVertices.Count(v => state.PurchasedVertices.Contains(v));
            bool isHovered = hex.Coord.Equals(_hoveredHex);

            var points = GetHexPoints(pos.X, pos.Y, R);
            using var path = PointsToPath(points);

            var color = HexGrayByActivity[Math.Clamp(adjCount, 0, HexGrayByActivity.Length - 1)];
            if (isHovered) color = Brighten(color, 20);

            _hexFillPaint.Color = color;
            canvas.DrawPath(path, _hexFillPaint);
            canvas.DrawPath(path, _hexBorderPaint);

            string name = _localization.Get(hex.LocalizationKey);
            canvas.DrawText(name, pos.X, pos.Y + 4f, SKTextAlign.Center, _labelFont, _textBlackPaint);
        }
    }

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

        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            if (!_visibleVertices.Contains(vertex.Coord)) continue;
            var pos = ScreenPosVertex(vertex.Coord);
            bool purchased = state.PurchasedVertices.Contains(vertex.Coord);
            bool canBuy    = controller.CanPurchaseVertex(state, vertex.Coord);
            bool isHovered = vertex.Coord.Equals(_hoveredVertex);

            SKColor fill = purchased  ? new SKColor(220, 50, 50)
                : canBuy              ? new SKColor(60, 160, 255, 200)
                                      : new SKColor(110, 110, 120, 200);

            if (isHovered && !purchased)
                fill = new SKColor(255, 235, 59, 220);

            _vertexFillPaint.Color = fill;
            canvas.DrawCircle(pos, VertexCircleRadius, _vertexFillPaint);

            _vertexBorderPaint.StrokeWidth = isHovered ? 2.5f : 1.5f;
            canvas.DrawCircle(pos, VertexCircleRadius, _vertexBorderPaint);

            if (purchased)
                canvas.DrawText("✓", pos.X, pos.Y + 4f, SKTextAlign.Center, _labelFontBold, _textWhitePaint);

            // Label placed radially outward from map center
            var local = LocalVertexPos(vertex.Coord);
            var offset = new SKPoint(local.X - CentralLocal.X, local.Y - CentralLocal.Y);
            var labelPos = RadialLabelPos(offset, pos, VertexCircleRadius + 13f);
            string name = _localization.Get(vertex.LocalizationKey);
            canvas.DrawText(name, labelPos.X, labelPos.Y, SKTextAlign.Center, _labelFont, _textBlackPaint);
        }
    }

    // ─── Pointer handling ────────────────────────────────────────────────────

    public void HandlePointerMoved(SKPoint position)
    {
        _hoveredVertex = null;
        _hoveredHex    = null;

        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            if (!_visibleVertices.Contains(vertex.Coord)) continue;
            var pos = ScreenPosVertex(vertex.Coord);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= VertexCircleRadius * VertexCircleRadius)
            {
                _hoveredVertex = vertex.Coord;
                return;
            }
        }

        foreach (var hex in PrestigeMapController.DefaultMap.Hexes)
        {
            if (!_visibleHexes.Contains(hex.Coord)) continue;
            var pos = ScreenPosHex(hex.Coord);
            var pts = GetHexPoints(pos.X, pos.Y, R);
            if (IsPointInPolygon(position.X, position.Y, pts))
            {
                _hoveredHex = hex.Coord;
                return;
            }
        }
    }

    public bool HandlePointerPressed(SKPoint position)
    {
        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            if (!_visibleVertices.Contains(vertex.Coord)) continue;
            var pos = ScreenPosVertex(vertex.Coord);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= VertexCircleRadius * VertexCircleRadius)
            {
                var mainState = _gameControllerService.MainGameController.CurrentMainState;
                if (mainState?.PrestigeState != null)
                    _gameControllerService.MainGameController.PrestigeMapController
                        .PurchaseVertex(mainState.PrestigeState, vertex.Coord);
                return true;
            }
        }
        return false;
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

        if (coord.Equals(PrestigeMap.CentralVertex))
            lines.Add(_localization.Get("prestige_tooltip_unlocks_research_system"));

        foreach (var mod in vertex.Modifiers.Where(m => m.Category == Modifier.ECategory.UNLOCK_RESEARCH))
            lines.Add($"{_localization.Get("prestige_tooltip_unlocks_research")}: {UnlockResearchName(mod.SubCategory)}");

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

        bool purchased = state.PurchasedVertices.Contains(coord);
        if (purchased)
        {
            lines.Add(_localization.Get("prestige_tooltip_purchased"));
        }
        else
        {
            bool canBuy = _gameControllerService.MainGameController.PrestigeMapController
                .CanPurchaseVertex(state, coord);
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
                        or Modifier.ECategory.RESEARCH_COST_REDUCTION;
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
        Modifier.ECategory.UNLOCK_RESEARCH           => _localization.Get("prestige_tooltip_unlocks_research"),
        Modifier.ECategory.UNLOCK_MARITIME_ROUTES    => _localization.Get("prestige_tooltip_unlocks_maritime_routes"),
        _ => $"+{mod.Value}"
    };

    // ─── Position helpers ─────────────────────────────────────────────────────

    // Local-space hex center (pointy-top, circumradius R, origin at hex-grid (0,0))
    private static SKPoint LocalHexPos(HexCoord c)
        => new(R * (Sqrt3 * c.Q + Sqrt3Half * c.R), R * 1.5f * c.R);

    // Local-space vertex position = centroid of its 3 adjacent hex centers
    private static SKPoint LocalVertexPos(Vertex v)
    {
        var p1 = LocalHexPos(v.Hex1);
        var p2 = LocalHexPos(v.Hex2);
        var p3 = LocalHexPos(v.Hex3);
        return new((p1.X + p2.X + p3.X) / 3f, (p1.Y + p2.Y + p3.Y) / 3f);
    }

    // Screen positions — Central vertex is anchored to _mapCenter
    private SKPoint ScreenPosVertex(Vertex v)
    {
        var local = LocalVertexPos(v);
        return new(_mapCenter.X + local.X - CentralLocal.X, _mapCenter.Y + local.Y - CentralLocal.Y);
    }

    private SKPoint ScreenPosHex(HexCoord c)
    {
        var local = LocalHexPos(c);
        return new(_mapCenter.X + local.X - CentralLocal.X, _mapCenter.Y + local.Y - CentralLocal.Y);
    }

    // Place label at 'dist' pixels radially outward from map center; Central falls back to below.
    private static SKPoint RadialLabelPos(SKPoint offset, SKPoint screenPos, float dist)
    {
        float len = MathF.Sqrt(offset.X * offset.X + offset.Y * offset.Y);
        if (len < 0.01f)
            return new SKPoint(screenPos.X, screenPos.Y + dist);
        return new SKPoint(
            screenPos.X + offset.X / len * dist,
            screenPos.Y + offset.Y / len * dist + 4f);
    }

    // Pointy-top hex points — matches HexBasedRenderer.GetHexagonPoints exactly
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
        _roadPaint.Dispose();
        _hexFillPaint.Dispose();
        _hexBorderPaint.Dispose();
        _vertexFillPaint.Dispose();
        _vertexBorderPaint.Dispose();
        _textBlackPaint.Dispose();
        _textWhitePaint.Dispose();
        _labelFont.Dispose();
        _labelFontBold.Dispose();
    }
}
