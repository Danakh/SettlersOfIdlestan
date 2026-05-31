using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using System;
using System.Collections.Generic;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.Bandits;
using System.Linq;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestan.Model.IslandFeatures;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Renderer de base pour le plateau de jeu hexagonal.
/// Responsable du rendu des hexagones, du terrain et des ressources.
/// </summary>
public class GameBoardRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly HarvestController _harvestController;
    private readonly ResourceManager _resourceManager;
    private WonderService? _wonderService;

    private SKPaint? _hexBorderPaint;
    private SKPaint? _hexFillPaint;
    private SKPaint? _textPaint;
    private SKFont? _textFont;
    private SKPaint? _dotPaint;
    private SKPaint? _ringBgPaint;
    private SKPaint? _ringProgressPaint;
    private SKPaint? _textIconPaint;
    private SKFont? _textIconFont;
    private SKPaint? _selectedWonderPaint;
    private bool _disposed;

    private readonly Dictionary<HexCoord, SKPath> _hexPathCache = new();

    private const float WonderSelectionRadius = 28f;
    private const float DotRadius = 5f;
    private const float ManualRingRadius = 5f;
    private const float ManualRingStroke = 3f;
    // Arc auto : centré au vertex de la ville, rayon juste supérieur à CityRadius (8 px)
    private const float AutoArcBaseRadius = 10f;
    private const float AutoArcGap = 5f;   // décalage entre deux arcs concentriques (mine + carrière)
    private const float AutoArcStroke = 3f;
    private const float BanditRingRadius = 12f;
    private const float BanditRingStroke = 3f;

    private static readonly Dictionary<TerrainType, SKColor> TerrainColors = new()
    {
        { TerrainType.Forest,   new SKColor(34, 139, 34) },
        { TerrainType.Hill,     new SKColor(210, 180, 140) },
        { TerrainType.Plain,    new SKColor(144, 238, 144) },
        { TerrainType.Mountain, new SKColor(139, 69, 19) },
        { TerrainType.Desert,   new SKColor(238, 214, 175) },
        { TerrainType.Water,    new SKColor(30, 144, 255) },
    };

    public GameBoardRenderer(HarvestController harvestController, ResourceManager resourceManager)
    {
        _harvestController = harvestController;
        _resourceManager = resourceManager;
    }

    public void ConnectWonderService(WonderService wonderService) => _wonderService = wonderService;

    public void Initialize(SKSize canvasSize)
    {
        _hexBorderPaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 2,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _hexFillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _textPaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
        };

        _textFont = new SKFont(SKTypeface.Default, 12);

        _textIconFont = new SKFont(SkiaFonts.Regular, 16f) { Edging = SKFontEdging.Antialias };
        _textIconPaint = new SKPaint { IsAntialias = true, Color = SKColors.White };

        _dotPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        _ringBgPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _ringProgressPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = true
        };

        _selectedWonderPaint = new SKPaint
        {
            Color = new SKColor(255, 215, 0, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null)
            return;

        if (context.GameState is MainGameState mainGameState)
        {
            var islandState = mainGameState.CurrentIslandState;
            if (islandState != null && islandState.IsViewingUnderworld && islandState.Underworld != null)
            {
                // Underworld view: dark cave background
                canvas.DrawColor(new SKColor(15, 8, 30));
                DrawIslandMap(canvas, islandState.Underworld.Map, islandState.PlayerCivilization.Index,
                    mainGameState.Clock.CurrentTick, null, null, null, null);
                return;
            }
        }

        canvas.DrawColor(new SKColor(238, 242, 245));

        if (context.GameState is MainGameState mgs)
        {
            var islandState = mgs.CurrentIslandState;
            if (islandState != null)
            {
                IslandMap? mapToRender = null;
                if (DebugSettings.ShowFullMap)
                    mapToRender = islandState.Map;
                else if (islandState.VisibleIslandMaps.TryGetValue(islandState.PlayerCivilization.Index, out var visibleMap))
                    mapToRender = visibleMap;

                if (mapToRender != null)
                {
                    var playerIdx = islandState.PlayerCivilization.Index;
                    islandState.HarvestLastTimesByCivilization.TryGetValue(playerIdx, out var manualTimes);

                    var banditPositions = new HashSet<HexCoord>(islandState.Features.OfType<Bandit>().Select(b => b.Position));
                    var harvestBlockedPositions = new HashSet<HexCoord>(islandState.Features.Where(f => f.BlocksHarvest).Select(f => f.Position));
                    var featuresByPosition = islandState.Features
                        .Where(f => f.ShouldRenderIcon && (f.SvgIconResourceName != null || f.TextIcon != null))
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => (IEnumerable<IslandFeature>)g);
                    DrawIslandMap(canvas, mapToRender, playerIdx, mgs.Clock.CurrentTick, manualTimes, islandState.BanditCooldownUntil, banditPositions, harvestBlockedPositions, featuresByPosition);

                    var selectedWonder = _wonderService?.SelectedWonder;
                    if (selectedWonder != null && _selectedWonderPaint != null)
                    {
                        var (wx, wy) = AxialToIsland(selectedWonder.Position.Q, selectedWonder.Position.R);
                        canvas.DrawCircle(wx, wy, WonderSelectionRadius, _selectedWonderPaint);
                    }
                }
            }
        }
    }

    private void DrawIslandMap(SKCanvas canvas, IslandMap map, int playerIdx,
        long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        Dictionary<HexCoord, long>? banditCooldownUntil,
        HashSet<HexCoord>? banditPositions,
        HashSet<HexCoord>? harvestBlockedPositions,
        Dictionary<HexCoord, IEnumerable<IslandFeature>>? featuresByPosition = null)
    {
        foreach (var (coord, tile) in map.Tiles)
        {
            var (x, y) = AxialToIsland(coord.Q, coord.R);
            DrawHexagonTile(canvas, coord, x, y, tile, playerIdx, currentTick, manualTimes, banditCooldownUntil, banditPositions, harvestBlockedPositions, featuresByPosition);
        }
    }

    private SKPath GetOrCreateHexPath(HexCoord coord, float cx, float cy)
    {
        if (_hexPathCache.TryGetValue(coord, out var cached))
            return cached;

        var points = GetHexagonPoints(cx, cy, HexSize);
        var path = new SKPath();
        path.MoveTo(points[0]);
        for (int i = 1; i < 6; i++)
            path.LineTo(points[i]);
        path.Close();

        _hexPathCache[coord] = path;
        return path;
    }

    private void DrawHexagonTile(SKCanvas canvas, HexCoord coord, float centerX, float centerY, HexTile tile,
        int playerIdx, long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        Dictionary<HexCoord, long>? banditCooldownUntil,
        HashSet<HexCoord>? banditPositions,
        HashSet<HexCoord>? harvestBlockedPositions,
        Dictionary<HexCoord, IEnumerable<IslandFeature>>? featuresByPosition = null)
    {
        var path = GetOrCreateHexPath(coord, centerX, centerY);

        if (_hexFillPaint != null)
        {
            _hexFillPaint.Color = TerrainColors.TryGetValue(tile.TerrainType, out var color)
                ? color
                : new SKColor(200, 200, 200);

            canvas.DrawPath(path, _hexFillPaint);
        }

        if (_hexBorderPaint != null)
            canvas.DrawPath(path, _hexBorderPaint);

        if (featuresByPosition?.TryGetValue(coord, out var features) == true)
            foreach (var feature in features)
                DrawFeatureMarker(canvas, centerX, centerY, feature);

        DrawHarvestIndicator(canvas, centerX, centerY, tile, playerIdx, currentTick, manualTimes, banditCooldownUntil, banditPositions, harvestBlockedPositions);

        if (DebugSettings.ShowHexCoords && _textPaint != null && tile.Coord != null)
        {
            _textPaint.Color = SKColors.Black;
            canvas.DrawText($"{tile.Coord.Q},{tile.Coord.R}", centerX, centerY + HexSize / 2.5f, SKTextAlign.Center, _textFont, _textPaint);
        }
    }

    private void DrawFeatureMarker(SKCanvas canvas, float cx, float cy, IslandFeature feature)
    {
        var resourceName = feature.SvgIconResourceName;
        if (resourceName != null)
        {
            SKSvg? svg = null;
            try { svg = _resourceManager.LoadImage(resourceName); } catch { }
            var picture = svg?.Picture;
            if (picture == null) return;

            float size = feature.SvgIconSize;
            float naturalSize = Math.Max(picture.CullRect.Width, picture.CullRect.Height);
            float scale = naturalSize > 0f ? size / naturalSize : 1f;

            canvas.Save();
            canvas.Translate(cx - size / 2f, cy - size / 2f);
            canvas.Scale(scale);
            canvas.DrawPicture(picture);
            canvas.Restore();
            return;
        }

        var textIcon = feature.TextIcon;
        if (textIcon != null && _textIconFont != null && _textIconPaint != null)
            canvas.DrawText(textIcon, cx, cy + _textIconFont.Size / 2f - 1f, SKTextAlign.Center, _textIconFont, _textIconPaint);
    }

    /// <summary>
    /// Dessine l'indicateur de récolte :
    /// - Arcs de cooldown automatique dans le coin de l'hex pointant vers la ville (120°, un arc par bâtiment)
    /// - Anneau de cooldown manuel + camembert des ressources au centre
    /// </summary>
    private void DrawHarvestIndicator(SKCanvas canvas, float cx, float cy, HexTile tile,
        int playerIdx, long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        Dictionary<HexCoord, long>? banditCooldownUntil,
        HashSet<HexCoord>? banditPositions,
        HashSet<HexCoord>? harvestBlockedPositions)
    {
        if (_dotPaint == null || _ringBgPaint == null || _ringProgressPaint == null)
            return;

        // Anneau bandit (le plus externe)
        bool banditHere = banditPositions?.Contains(tile.Coord) == true;
        if (banditHere)
        {
            DrawBanditCooldownRing(canvas, cx, cy, ratio: 1f);
        }
        else if (banditCooldownUntil != null
            && banditCooldownUntil.TryGetValue(tile.Coord, out var banditUntil)
            && currentTick < banditUntil)
        {
            float ratio = Math.Clamp(
                (float)(currentTick - (banditUntil - BanditController.DepartureCooldownTicks)) / BanditController.DepartureCooldownTicks,
                0f, 1f);
            DrawBanditCooldownRing(canvas, cx, cy, ratio);
        }

        if (harvestBlockedPositions?.Contains(tile.Coord) == true)
            return;

        // Arcs de cooldown automatique au vertex de la ville (un arc par bâtiment).
        // Le rayon s'incrémente uniquement pour plusieurs bâtiments de la MÊME ville sur le même hex.
        var autoInfo = _harvestController.GetAutoHarvestInfoForHex(playerIdx, tile.Coord);
        var arcIndexByVertex = new Dictionary<Vertex, int>();
        foreach (var (cityVertex, _, lastTick, cooldown) in autoInfo)
        {
            arcIndexByVertex.TryGetValue(cityVertex, out int arcIdx);
            arcIndexByVertex[cityVertex] = arcIdx + 1;
            var vp = VertexToIsland(cityVertex);
            float radius = AutoArcBaseRadius + arcIdx * AutoArcGap;
            DrawAutoHarvestCornerArc(canvas, vp.X, vp.Y, cx, cy, radius, AutoArcStroke, lastTick, cooldown, currentTick);
        }

        var manualResources = _harvestController.GetManualHarvestableResources(playerIdx, tile.Coord);

        if (manualResources.Count > 0)
            DrawCooldownRing(canvas, cx, cy, ManualRingRadius, ManualRingStroke,
                tile.Coord, currentTick, manualTimes,
                _harvestController.GetManualHarvestCooldownTicks(playerIdx),
                new SKColor(60, 60, 60, 150),
                new SKColor(160, 230, 160, 230));

        if (manualResources.Count > 0)
            DrawResourcePie(canvas, cx, cy, DotRadius, manualResources);
    }

    /// <summary>
    /// Dessine un arc de 120° centré sur le vertex de la ville, représentant le cooldown de
    /// récolte automatique d'un bâtiment. L'arc entoure la ville et pointe vers le centre du hex.
    /// Plusieurs bâtiments sur le même hex (mine + carrière) donnent des arcs concentriques.
    /// </summary>
    private void DrawAutoHarvestCornerArc(SKCanvas canvas, float vx, float vy, float cx, float cy,
        float radius, float strokeWidth, long lastTick, long cooldownTicks, long currentTick)
    {
        // L'arc balaie 120° centré sur la direction vertex → centre du hex (angle intérieur du coin)
        double angleRad = Math.Atan2(cy - vy, cx - vx);
        float angleDeg = (float)(angleRad * 180.0 / Math.PI);
        float startAngle = angleDeg - 60f;

        var rect = new SKRect(vx - radius, vy - radius, vx + radius, vy + radius);

        _ringBgPaint!.Color = new SKColor(60, 60, 60, 150);
        _ringBgPaint.StrokeWidth = strokeWidth;
        canvas.DrawArc(rect, startAngle, 120f, false, _ringBgPaint);

        float ratio = lastTick == 0 ? 1f : Math.Clamp((float)(currentTick - lastTick) / cooldownTicks, 0f, 1f);
        if (ratio > 0f)
        {
            _ringProgressPaint!.Color = new SKColor(255, 200, 60, 230);
            _ringProgressPaint.StrokeWidth = strokeWidth;
            canvas.DrawArc(rect, startAngle, ratio * 120f, false, _ringProgressPaint);
        }
    }

    private void DrawBanditCooldownRing(SKCanvas canvas, float cx, float cy, float ratio)
    {
        var rect = new SKRect(cx - BanditRingRadius, cy - BanditRingRadius, cx + BanditRingRadius, cy + BanditRingRadius);

        _ringBgPaint!.Color = new SKColor(80, 0, 0, 150);
        _ringBgPaint.StrokeWidth = BanditRingStroke;
        canvas.DrawOval(rect, _ringBgPaint);

        if (ratio > 0f)
        {
            _ringProgressPaint!.Color = new SKColor(220, 40, 40, 230);
            _ringProgressPaint.StrokeWidth = BanditRingStroke;
            canvas.DrawArc(rect, -90f, ratio * 360f, false, _ringProgressPaint);
        }
    }

    /// <summary>
    /// Dessine un cercle plein (1 ressource) ou un camembert à parts égales (N ressources).
    /// </summary>
    private void DrawResourcePie(SKCanvas canvas, float cx, float cy, float radius, IReadOnlyList<Resource> resources)
    {
        if (resources.Count == 1)
        {
            _dotPaint!.Color = IslandMainRenderer.ResourceColors.GetValueOrDefault(resources[0], SKColors.White);
            canvas.DrawCircle(cx, cy, radius, _dotPaint);
        }
        else
        {
            float sliceDeg = 360f / resources.Count;
            var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

            for (int i = 0; i < resources.Count; i++)
            {
                using var path = new SKPath();
                path.MoveTo(cx, cy);
                path.ArcTo(rect, -90f + i * sliceDeg, sliceDeg, false);
                path.Close();

                _dotPaint!.Color = IslandMainRenderer.ResourceColors.GetValueOrDefault(resources[i], SKColors.White);
                canvas.DrawPath(path, _dotPaint);
            }
        }

        // Contour du cercle
        _ringBgPaint!.Color = new SKColor(0, 0, 0, 90);
        _ringBgPaint.StrokeWidth = 1f;
        canvas.DrawCircle(cx, cy, radius, _ringBgPaint);
    }

    /// <summary>
    /// Dessine un anneau de fond semi-transparent et, par-dessus, un arc coloré
    /// représentant la fraction écoulée du cooldown (0 = vient de récolter, 1 = prêt).
    /// </summary>
    private void DrawCooldownRing(SKCanvas canvas, float cx, float cy,
        float radius, float strokeWidth,
        HexCoord coord, long currentTick,
        Dictionary<HexCoord, long>? lastTimes,
        long cooldownTicks,
        SKColor bgColor, SKColor progressColor)
    {
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        _ringBgPaint!.Color = bgColor;
        _ringBgPaint.StrokeWidth = strokeWidth;
        canvas.DrawOval(rect, _ringBgPaint);

        float ratio = 1f;
        if (lastTimes != null && lastTimes.TryGetValue(coord, out var lastTick))
            ratio = Math.Clamp((float)(currentTick - lastTick) / cooldownTicks, 0f, 1f);

        if (ratio > 0f)
        {
            _ringProgressPaint!.Color = progressColor;
            _ringProgressPaint.StrokeWidth = strokeWidth;
            canvas.DrawArc(rect, -90f, ratio * 360f, false, _ringProgressPaint);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _hexBorderPaint?.Dispose();
        _hexFillPaint?.Dispose();
        _textPaint?.Dispose();
        _textFont?.Dispose();
        _dotPaint?.Dispose();
        _ringBgPaint?.Dispose();
        _ringProgressPaint?.Dispose();
        _textIconPaint?.Dispose();
        _textIconFont?.Dispose();
        _selectedWonderPaint?.Dispose();

        foreach (var path in _hexPathCache.Values)
            path.Dispose();
        _hexPathCache.Clear();

        _disposed = true;
    }
}
