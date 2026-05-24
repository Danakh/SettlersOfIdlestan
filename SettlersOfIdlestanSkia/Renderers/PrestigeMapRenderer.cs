using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.Prestige.PrestigeMap;
using SettlersOfIdlestan.Model.Prestige;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers;

public sealed class PrestigeMapRenderer : IGameRenderer
{
    // Hex circumradius — same visual scale as the island hexes
    private const float R = 60f;
    private const float VertexCircleRadius = 10f;
    private static readonly float Sqrt3Half = MathF.Sqrt(3f) / 2f;

    // Vertex positions relative to map center (derived from hex-grid vertex math)
    private static readonly Dictionary<PrestigeVertexId, SKPoint> VertexOffsets = new()
    {
        [PrestigeVertexId.Central]       = new(0f, 0f),
        [PrestigeVertexId.Barracks]      = new(0f, -R),
        [PrestigeVertexId.SeaportMarket] = new(-R * Sqrt3Half, R / 2f),
        [PrestigeVertexId.Laboratory]    = new(R * Sqrt3Half, R / 2f),
    };

    // Hex centers relative to map center
    // Inner 3: centroid of their 3 adjacent prestige vertices (= R from each vertex)
    // Outer 3: on the far side of their single outer vertex (2R from center)
    private static readonly Dictionary<PrestigeHexId, SKPoint> HexOffsets = new()
    {
        [PrestigeHexId.HarvestSpeed]         = new( 0f,               R),
        [PrestigeHexId.StartingResources]    = new(-R * Sqrt3Half, -R / 2f),
        [PrestigeHexId.ResearchSpeed]        = new( R * Sqrt3Half, -R / 2f),
        [PrestigeHexId.UnitProductionSpeed]  = new( 0f,              -2 * R),
        [PrestigeHexId.ResearchCostReduction]= new( 2 * R * Sqrt3Half, R),
        [PrestigeHexId.StorageCapacity]      = new(-2 * R * Sqrt3Half, R),
    };

    // Only the 3 true hex-edge connections: each pair is consecutive on one of the inner hexes
    private static readonly (PrestigeVertexId A, PrestigeVertexId B)[] AllEdges =
    {
        (PrestigeVertexId.Central, PrestigeVertexId.Barracks),
        (PrestigeVertexId.Central, PrestigeVertexId.SeaportMarket),
        (PrestigeVertexId.Central, PrestigeVertexId.Laboratory),
    };

    // Gray shades for hex fill: index = number of adjacent purchased vertices (0..3)
    private static readonly SKColor[] HexGrayByActivity =
    {
        new(210, 213, 218), // 0 purchased: light gray
        new(175, 178, 185), // 1 purchased
        new(135, 138, 148), // 2 purchased
        new( 95,  98, 110), // 3 purchased: dark gray
    };

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;

    private SKSize _canvasSize;
    private SKPoint _mapCenter;

    private PrestigeVertexId? _hoveredVertex;
    private PrestigeHexId? _hoveredHex;

    // Island background color — same as GameBoardRenderer background
    private readonly SKPaint _bgPaint = new() { Color = new SKColor(238, 242, 245), Style = SKPaintStyle.Fill };

    // Road paint: only drawn when both vertices are purchased
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
    private readonly SKFont  _labelFont     = new() { Size = 10 };
    private readonly SKFont  _labelFontBold = new() { Size = 10, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

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

    // Called by IGameRenderer pipeline — no-op; rendering is driven by OverlayRenderer
    public void Render(SKCanvas canvas, GameRenderContext context) { }

    public void RenderPrestigeMap(SKCanvas canvas, GameRenderContext context)
    {
        if (_canvasSize == default) return;

        var mainState = context.GameState as MainGameState;
        var prestigeState = mainState?.PrestigeState;
        if (prestigeState == null) return;

        // Cover the island map with the same background color (hides it without changing render order)
        float barH = PlayerResourcesOverlayRenderer.BarHeight;
        canvas.DrawRect(0f, barH, _canvasSize.Width, _canvasSize.Height - barH, _bgPaint);

        DrawHexes(canvas, prestigeState);
        DrawRoads(canvas, prestigeState);
        DrawVertices(canvas, prestigeState);

        if (_hoveredVertex.HasValue)
            BuildVertexTooltip(_hoveredVertex.Value, prestigeState);
        else if (_hoveredHex.HasValue)
            BuildHexTooltip(_hoveredHex.Value, prestigeState);
    }

    private void DrawHexes(SKCanvas canvas, PrestigeState state)
    {
        foreach (var hex in PrestigeMapController.DefaultMap.Hexes)
        {
            var pos = ScreenPos(hex.Id);
            int adjCount = hex.AdjacentVertices.Count(v => state.PurchasedVertices.Contains(v));
            bool isHovered = _hoveredHex == hex.Id;

            var points = GetHexPoints(pos.X, pos.Y, R);
            using var path = PointsToPath(points);

            var color = HexGrayByActivity[Math.Clamp(adjCount, 0, HexGrayByActivity.Length - 1)];
            if (isHovered)
                color = Brighten(color, 20);

            _hexFillPaint.Color = color;
            canvas.DrawPath(path, _hexFillPaint);
            canvas.DrawPath(path, _hexBorderPaint);

            // Hex name inside
            string name = _localization.Get(HexLocKey(hex.Id));
            canvas.DrawText(name, pos.X, pos.Y + 4f, SKTextAlign.Center, _labelFont, _textBlackPaint);
        }
    }

    private void DrawRoads(SKCanvas canvas, PrestigeState state)
    {
        foreach (var (a, b) in AllEdges)
        {
            if (state.PurchasedVertices.Contains(a) && state.PurchasedVertices.Contains(b))
                canvas.DrawLine(ScreenPos(a), ScreenPos(b), _roadPaint);
        }
    }

    private void DrawVertices(SKCanvas canvas, PrestigeState state)
    {
        var controller = _gameControllerService.MainGameController.PrestigeMapController;

        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            var pos = ScreenPos(vertex.Id);
            bool purchased = state.PurchasedVertices.Contains(vertex.Id);
            bool canBuy    = controller.CanPurchaseVertex(state, vertex.Id);
            bool isHovered = _hoveredVertex == vertex.Id;

            SKColor fill = purchased  ? new SKColor(220, 50, 50)         // red — like island city
                : canBuy              ? new SKColor(60, 160, 255, 200)   // blue hint — like buildable vertex
                                      : new SKColor(110, 110, 120, 200); // locked — dim gray

            if (isHovered && !purchased)
                fill = new SKColor(255, 235, 59, 220); // yellow hover — same as island hover

            _vertexFillPaint.Color = fill;
            canvas.DrawCircle(pos, VertexCircleRadius, _vertexFillPaint);

            _vertexBorderPaint.StrokeWidth = isHovered ? 2.5f : 1.5f;
            canvas.DrawCircle(pos, VertexCircleRadius, _vertexBorderPaint);

            if (purchased)
                canvas.DrawText("✓", pos.X, pos.Y + 4f, SKTextAlign.Center, _labelFontBold, _textWhitePaint);

            // Label placed radially outward from map center
            var labelPos = RadialLabelPos(VertexOffsets[vertex.Id], pos, VertexCircleRadius + 13f);
            string name = _localization.Get(VertexLocKey(vertex.Id));
            canvas.DrawText(name, labelPos.X, labelPos.Y, SKTextAlign.Center, _labelFont, _textBlackPaint);
        }
    }

    // ─── Pointer handling ────────────────────────────────────────────────────

    public void HandlePointerMoved(SKPoint position)
    {
        _hoveredVertex = null;
        _hoveredHex    = null;

        foreach (var (id, offset) in VertexOffsets)
        {
            var pos = new SKPoint(_mapCenter.X + offset.X, _mapCenter.Y + offset.Y);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= VertexCircleRadius * VertexCircleRadius)
            {
                _hoveredVertex = id;
                return;
            }
        }

        foreach (var (id, offset) in HexOffsets)
        {
            var pos = new SKPoint(_mapCenter.X + offset.X, _mapCenter.Y + offset.Y);
            var pts = GetHexPoints(pos.X, pos.Y, R);
            if (IsPointInPolygon(position.X, position.Y, pts))
            {
                _hoveredHex = id;
                return;
            }
        }
    }

    public bool HandlePointerPressed(SKPoint position)
    {
        foreach (var (id, offset) in VertexOffsets)
        {
            var pos = new SKPoint(_mapCenter.X + offset.X, _mapCenter.Y + offset.Y);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= VertexCircleRadius * VertexCircleRadius)
            {
                var mainState = _gameControllerService.MainGameController.CurrentMainState;
                if (mainState?.PrestigeState != null)
                    _gameControllerService.MainGameController.PrestigeMapController
                        .PurchaseVertex(mainState.PrestigeState, id);
                return true;
            }
        }
        return false;
    }

    // ─── Tooltip builders ────────────────────────────────────────────────────

    private void BuildVertexTooltip(PrestigeVertexId id, PrestigeState state)
    {
        var vertex = PrestigeMapController.DefaultMap.GetVertex(id);
        if (vertex == null) return;

        var lines = new List<string> { _localization.Get(VertexLocKey(id)), "" };

        foreach (var mod in vertex.Modifiers)
            lines.Add(FormatModifier(mod));

        if (vertex.StartingBuildings.Count > 0)
        {
            var names = string.Join(", ", vertex.StartingBuildings
                .Select(b => _localization.Get($"building_{b.ToString().ToLower()}_name")));
            lines.Add($"{_localization.Get("prestige_tooltip_starts_with")}: {names}");
        }

        lines.Add("");

        bool purchased = state.PurchasedVertices.Contains(id);
        if (purchased)
        {
            lines.Add(_localization.Get("prestige_tooltip_purchased"));
        }
        else
        {
            var missing = vertex.Prerequisites.Where(p => !state.PurchasedVertices.Contains(p)).ToList();
            if (missing.Count > 0)
            {
                var prereqNames = string.Join(", ", missing.Select(p => _localization.Get(VertexLocKey(p))));
                lines.Add($"{_localization.Get("prestige_tooltip_requires")}: {prereqNames}");
            }
            else
            {
                lines.Add($"{_localization.Get("prestige_tooltip_cost")}: {vertex.Cost} pts");
            }
        }

        _tooltipRenderer.SetTooltipLines(lines.ToArray(), ScreenPos(id));
    }

    private void BuildHexTooltip(PrestigeHexId id, PrestigeState state)
    {
        var hex = PrestigeMapController.DefaultMap.GetHex(id);
        if (hex == null) return;

        var lines = new List<string> { _localization.Get(HexLocKey(id)), "" };

        foreach (var mod in hex.PerVertexModifiers)
            lines.Add($"{FormatModifier(mod)} {_localization.Get("prestige_tooltip_per_vertex")}");

        if (hex.StartingResourceBonusPerVertex > 0)
            lines.Add($"+{hex.StartingResourceBonusPerVertex} {_localization.Get("prestige_tooltip_resources_per_vertex")}");

        int adjCount = hex.AdjacentVertices.Count(v => state.PurchasedVertices.Contains(v));
        lines.Add("");
        lines.Add($"{_localization.Get("prestige_tooltip_active_vertices")}: {adjCount}/{hex.AdjacentVertices.Count}");

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
                    string val = isPct ? $"+{(int)(total * 100)}%" : $"+{(int)total}";
                    lines.Add($"{_localization.Get("prestige_tooltip_current_bonus")}: {FormatModifier(mod)} × {adjCount} = {val}");
                }
            }
            else if (hex.StartingResourceBonusPerVertex > 0)
            {
                lines.Add($"{_localization.Get("prestige_tooltip_current_bonus")}: +{hex.StartingResourceBonusPerVertex * adjCount}");
            }
        }

        _tooltipRenderer.SetTooltipLines(lines.ToArray(), ScreenPos(id));
    }

    private string FormatModifier(Modifier mod) => mod.Category switch
    {
        Modifier.ECategory.BUILDING_MAX_LEVEL => $"+{(int)mod.Value} {(string.IsNullOrEmpty(mod.SubCategory) ? "" : _localization.Get($"building_{mod.SubCategory.ToLower()}_name"))} max",
        Modifier.ECategory.HARVEST_SPEED         => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_harvest_speed")}",
        Modifier.ECategory.RESEARCH_SPEED        => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_research_speed")}",
        Modifier.ECategory.UNIT_PRODUCTION_SPEED => $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_unit_speed")}",
        Modifier.ECategory.RESEARCH_COST_REDUCTION => $"-{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_research_cost")}",
        Modifier.ECategory.STORAGE_CAPACITY_BASIC    => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_storage_basic")}",
        Modifier.ECategory.STORAGE_CAPACITY_ADVANCED => $"+{(int)mod.Value} {_localization.Get("prestige_tooltip_storage_advanced")}",
        _ => $"+{mod.Value}"
    };

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private SKPoint ScreenPos(PrestigeVertexId id)
    {
        var o = VertexOffsets[id];
        return new SKPoint(_mapCenter.X + o.X, _mapCenter.Y + o.Y);
    }

    private SKPoint ScreenPos(PrestigeHexId id)
    {
        var o = HexOffsets[id];
        return new SKPoint(_mapCenter.X + o.X, _mapCenter.Y + o.Y);
    }

    // Place the label at 'dist' pixels outward from map center along the offset vector.
    // Falls back to directly below for Central (offset zero).
    private SKPoint RadialLabelPos(SKPoint offset, SKPoint screenPos, float dist)
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

    private static string VertexLocKey(PrestigeVertexId id) => id switch
    {
        PrestigeVertexId.Central       => "prestige_vertex_central",
        PrestigeVertexId.SeaportMarket => "prestige_vertex_seaport_market",
        PrestigeVertexId.Laboratory    => "prestige_vertex_laboratory",
        PrestigeVertexId.Barracks      => "prestige_vertex_barracks",
        _ => id.ToString()
    };

    private static string HexLocKey(PrestigeHexId id) => id switch
    {
        PrestigeHexId.StartingResources     => "prestige_hex_starting_resources",
        PrestigeHexId.HarvestSpeed          => "prestige_hex_harvest_speed",
        PrestigeHexId.ResearchSpeed         => "prestige_hex_research_speed",
        PrestigeHexId.UnitProductionSpeed   => "prestige_hex_unit_production_speed",
        PrestigeHexId.ResearchCostReduction => "prestige_hex_research_cost_reduction",
        PrestigeHexId.StorageCapacity       => "prestige_hex_storage_capacity",
        _ => id.ToString()
    };

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
