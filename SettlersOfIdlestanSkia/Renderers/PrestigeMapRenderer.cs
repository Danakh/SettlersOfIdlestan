using SettlersOfIdlestan.Controller;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.GameplayModifier;
using SettlersOfIdlestan.Model.PrestigeMap;
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
    private const float VertexRadius = 26f;
    private const float HexRadius = 30f;

    private static readonly Dictionary<PrestigeVertexId, SKPoint> VertexOffsets = new()
    {
        [PrestigeVertexId.Central]       = new(0, 0),
        [PrestigeVertexId.SeaportMarket] = new(-160, -110),
        [PrestigeVertexId.Laboratory]    = new(160, -110),
        [PrestigeVertexId.Barracks]      = new(0, 220),
    };

    private static readonly Dictionary<PrestigeHexId, SKPoint> HexOffsets = new()
    {
        [PrestigeHexId.HarvestSpeed]      = new(0, -73),
        [PrestigeHexId.StartingResources] = new(-53, 37),
        [PrestigeHexId.ResearchSpeed]     = new(53, 37),
    };

    private static readonly Dictionary<PrestigeHexId, SKColor> HexBaseColors = new()
    {
        [PrestigeHexId.StartingResources] = new SKColor(200, 140, 50, 150),
        [PrestigeHexId.HarvestSpeed]      = new SKColor(50, 160, 80, 150),
        [PrestigeHexId.ResearchSpeed]     = new SKColor(120, 60, 200, 150),
    };

    private static readonly (PrestigeVertexId A, PrestigeVertexId B)[] Edges =
    {
        (PrestigeVertexId.Central, PrestigeVertexId.SeaportMarket),
        (PrestigeVertexId.Central, PrestigeVertexId.Laboratory),
        (PrestigeVertexId.Central, PrestigeVertexId.Barracks),
    };

    private readonly GameControllerService _gameControllerService;
    private readonly ILocalizationService _localization;
    private readonly TooltipRenderer _tooltipRenderer;

    private SKSize _canvasSize;
    private SKPoint _mapCenter;

    private PrestigeVertexId? _hoveredVertex;
    private PrestigeHexId? _hoveredHex;

    private readonly SKPaint _bgPaint = new() { Color = new SKColor(0, 0, 0, 170), Style = SKPaintStyle.Fill };
    private readonly SKPaint _edgeActivePaint = new() { Color = SKColors.Gold, StrokeWidth = 3, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _edgeInactivePaint = new() { Color = new SKColor(75, 75, 90), StrokeWidth = 2, Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _hexFillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _hexBorderPaint = new() { Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _vertexFillPaint = new() { Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _vertexBorderPaint = new() { Style = SKPaintStyle.Stroke, StrokeWidth = 2f, IsAntialias = true };
    private readonly SKPaint _labelPaint = new() { Color = SKColors.White, IsAntialias = true };
    private readonly SKFont _labelFont = new() { Size = 10 };
    private readonly SKFont _labelFontBold = new() { Size = 11, Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold) };

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
        _mapCenter = new SKPoint(canvasSize.Width / 2f, barH + (canvasSize.Height - barH) * 0.48f);
    }

    // IGameRenderer.Render — not called directly; OverlayRenderer calls RenderPrestigeMap
    public void Render(SKCanvas canvas, GameRenderContext context) { }

    public void RenderPrestigeMap(SKCanvas canvas, GameRenderContext context)
    {
        if (_canvasSize == default) return;

        var mainState = context.GameState as MainGameState;
        var prestigeState = mainState?.PrestigeState;
        if (prestigeState == null) return;

        float barH = PlayerResourcesOverlayRenderer.BarHeight;
        canvas.DrawRect(0, barH, _canvasSize.Width, _canvasSize.Height - barH, _bgPaint);

        DrawEdges(canvas, prestigeState);
        DrawHexes(canvas, prestigeState);
        DrawVertices(canvas, prestigeState);

        if (_hoveredVertex.HasValue)
            BuildVertexTooltip(_hoveredVertex.Value, prestigeState);
        else if (_hoveredHex.HasValue)
            BuildHexTooltip(_hoveredHex.Value, prestigeState);
    }

    private void DrawEdges(SKCanvas canvas, PrestigeState state)
    {
        foreach (var (a, b) in Edges)
        {
            var p1 = ScreenPos(a);
            var p2 = ScreenPos(b);
            bool active = state.PurchasedVertices.Contains(a) && state.PurchasedVertices.Contains(b);
            canvas.DrawLine(p1, p2, active ? _edgeActivePaint : _edgeInactivePaint);
        }
    }

    private void DrawHexes(SKCanvas canvas, PrestigeState state)
    {
        foreach (var hex in PrestigeMapController.DefaultMap.Hexes)
        {
            var pos = ScreenPos(hex.Id);
            int adjCount = hex.AdjacentVertices.Count(v => state.PurchasedVertices.Contains(v));
            bool isHovered = _hoveredHex == hex.Id;
            bool isActive = adjCount > 0;

            using var path = BuildHexPath(pos, HexRadius);

            var baseColor = HexBaseColors[hex.Id];
            if (isHovered)
                baseColor = new SKColor(
                    (byte)Math.Min(255, baseColor.Red + 50),
                    (byte)Math.Min(255, baseColor.Green + 50),
                    (byte)Math.Min(255, baseColor.Blue + 50),
                    210);

            _hexFillPaint.Color = baseColor;
            canvas.DrawPath(path, _hexFillPaint);

            _hexBorderPaint.Color = isActive ? SKColors.Gold : new SKColor(140, 140, 160, 180);
            _hexBorderPaint.StrokeWidth = isActive ? 2.5f : 1.5f;
            canvas.DrawPath(path, _hexBorderPaint);

            var name = _localization.Get(HexLocKey(hex.Id));
            canvas.DrawText(name, pos.X, pos.Y + 4, SKTextAlign.Center, _labelFont, _labelPaint);
        }
    }

    private void DrawVertices(SKCanvas canvas, PrestigeState state)
    {
        var controller = _gameControllerService.MainGameController.PrestigeMapController;

        foreach (var vertex in PrestigeMapController.DefaultMap.Vertices)
        {
            var pos = ScreenPos(vertex.Id);
            bool purchased = state.PurchasedVertices.Contains(vertex.Id);
            bool canBuy = controller.CanPurchaseVertex(state, vertex.Id);
            bool isHovered = _hoveredVertex == vertex.Id;

            SKColor fillColor = purchased
                ? new SKColor(220, 170, 0)
                : canBuy ? new SKColor(55, 115, 195)
                : new SKColor(50, 50, 62);

            if (isHovered && !purchased)
                fillColor = new SKColor(
                    (byte)Math.Min(255, fillColor.Red + 40),
                    (byte)Math.Min(255, fillColor.Green + 40),
                    (byte)Math.Min(255, fillColor.Blue + 40));

            _vertexFillPaint.Color = fillColor;
            canvas.DrawCircle(pos, VertexRadius, _vertexFillPaint);

            _vertexBorderPaint.Color = purchased ? SKColors.Gold : new SKColor(180, 180, 200);
            _vertexBorderPaint.StrokeWidth = isHovered ? 2.5f : 1.5f;
            canvas.DrawCircle(pos, VertexRadius, _vertexBorderPaint);

            // Checkmark for purchased
            if (purchased)
            {
                _labelPaint.Color = SKColors.Black;
                canvas.DrawText("✓", pos.X, pos.Y + 5, SKTextAlign.Center, _labelFontBold, _labelPaint);
                _labelPaint.Color = SKColors.White;
            }

            var name = _localization.Get(VertexLocKey(vertex.Id));
            canvas.DrawText(name, pos.X, pos.Y + VertexRadius + 14, SKTextAlign.Center, _labelFont, _labelPaint);
        }
    }

    public void HandlePointerMoved(SKPoint position)
    {
        _hoveredVertex = null;
        _hoveredHex = null;

        foreach (var (id, offset) in VertexOffsets)
        {
            var pos = new SKPoint(_mapCenter.X + offset.X, _mapCenter.Y + offset.Y);
            float dx = position.X - pos.X, dy = position.Y - pos.Y;
            if (dx * dx + dy * dy <= VertexRadius * VertexRadius)
            {
                _hoveredVertex = id;
                return;
            }
        }

        foreach (var (id, offset) in HexOffsets)
        {
            var pos = new SKPoint(_mapCenter.X + offset.X, _mapCenter.Y + offset.Y);
            using var path = BuildHexPath(pos, HexRadius);
            if (path.Contains(position.X, position.Y))
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
            if (dx * dx + dy * dy <= VertexRadius * VertexRadius)
            {
                var mainState = _gameControllerService.MainGameController.CurrentMainState;
                if (mainState?.PrestigeState != null)
                    _gameControllerService.MainGameController.PrestigeMapController.PurchaseVertex(mainState.PrestigeState, id);
                return true;
            }
        }
        return false;
    }

    private void BuildVertexTooltip(PrestigeVertexId id, PrestigeState state)
    {
        var vertex = PrestigeMapController.DefaultMap.GetVertex(id);
        if (vertex == null) return;

        var lines = new List<string> { _localization.Get(VertexLocKey(id)), "" };

        foreach (var mod in vertex.Modifiers)
            lines.Add(FormatModifierEffect(mod));

        if (vertex.StartingBuildings.Count > 0)
        {
            var names = string.Join(", ", vertex.StartingBuildings.Select(b => _localization.Get($"building_{b.ToString().ToLower()}_name")));
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
            var missingPrereqs = vertex.Prerequisites.Where(p => !state.PurchasedVertices.Contains(p)).ToList();
            if (missingPrereqs.Count > 0)
            {
                var prereqNames = string.Join(", ", missingPrereqs.Select(p => _localization.Get(VertexLocKey(p))));
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

        if (hex.PerVertexModifier != null)
            lines.Add($"{FormatModifierEffect(hex.PerVertexModifier)} {_localization.Get("prestige_tooltip_per_vertex")}");

        if (hex.StartingResourceBonusPerVertex > 0)
            lines.Add($"+{hex.StartingResourceBonusPerVertex} {_localization.Get("prestige_tooltip_resources_per_vertex")}");

        int adjCount = hex.AdjacentVertices.Count(v => state.PurchasedVertices.Contains(v));
        lines.Add("");
        lines.Add($"{_localization.Get("prestige_tooltip_active_vertices")}: {adjCount}/{hex.AdjacentVertices.Count}");

        if (adjCount > 0)
        {
            if (hex.PerVertexModifier != null)
            {
                double total = hex.PerVertexModifier.Value * adjCount;
                string formatted = (hex.PerVertexModifier.Category == Modifier.ECategory.HARVEST_SPEED
                    || hex.PerVertexModifier.Category == Modifier.ECategory.RESEARCH_SPEED)
                    ? $"+{(int)(total * 100)}%"
                    : $"+{total}";
                lines.Add($"{_localization.Get("prestige_tooltip_current_bonus")}: {formatted}");
            }
            else if (hex.StartingResourceBonusPerVertex > 0)
            {
                lines.Add($"{_localization.Get("prestige_tooltip_current_bonus")}: +{hex.StartingResourceBonusPerVertex * adjCount}");
            }
        }

        _tooltipRenderer.SetTooltipLines(lines.ToArray(), ScreenPos(id));
    }

    private string FormatModifierEffect(Modifier mod)
    {
        if (mod.Category == Modifier.ECategory.BUILDING_MAX_LEVEL)
        {
            string sub = string.IsNullOrEmpty(mod.SubCategory) ? "" : _localization.Get($"building_{mod.SubCategory.ToLower()}_name");
            return $"+{(int)mod.Value} {sub} max";
        }
        if (mod.Category == Modifier.ECategory.HARVEST_SPEED)
            return $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_harvest_speed")}";
        if (mod.Category == Modifier.ECategory.RESEARCH_SPEED)
            return $"+{(int)(mod.Value * 100)}% {_localization.Get("prestige_tooltip_research_speed")}";
        return $"+{mod.Value}";
    }

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

    private static SKPath BuildHexPath(SKPoint center, float radius)
    {
        var path = new SKPath();
        for (int i = 0; i < 6; i++)
        {
            float angle = (float)(Math.PI / 3 * i - Math.PI / 6);
            float x = center.X + radius * MathF.Cos(angle);
            float y = center.Y + radius * MathF.Sin(angle);
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }

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
        PrestigeHexId.StartingResources => "prestige_hex_starting_resources",
        PrestigeHexId.HarvestSpeed      => "prestige_hex_harvest_speed",
        PrestigeHexId.ResearchSpeed     => "prestige_hex_research_speed",
        _ => id.ToString()
    };

    public void Dispose()
    {
        _bgPaint.Dispose();
        _edgeActivePaint.Dispose();
        _edgeInactivePaint.Dispose();
        _hexFillPaint.Dispose();
        _hexBorderPaint.Dispose();
        _vertexFillPaint.Dispose();
        _vertexBorderPaint.Dispose();
        _labelPaint.Dispose();
        _labelFont.Dispose();
        _labelFontBold.Dispose();
    }
}
