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

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer de base pour le plateau de jeu hexagonal.
/// Responsable du rendu des hexagones, du terrain et des ressources.
/// </summary>
public class GameBoardRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly HarvestController _harvestController;
    private readonly ResourceManager _resourceManager;

    private SKSvg? _chestSvg;
    private const float ChestIconSize = 18f;
    private const float ChestSvgViewBox = 64f;

    private SKPaint? _hexBorderPaint;
    private SKPaint? _hexFillPaint;
    private SKPaint? _textPaint;
    private SKFont? _textFont;
    private SKPaint? _dotPaint;
    private SKPaint? _ringBgPaint;
    private SKPaint? _ringProgressPaint;
    private bool _disposed;

    private readonly Dictionary<HexCoord, SKPath> _hexPathCache = new();

    // Dimensions de l'indicateur (outer edge ≈ 50 % du rayon de l'hex = 20 px)
    private const float DotRadius = 5f;
    private const float ManualRingRadius = 5f;
    private const float ManualRingStroke = 3f;
    private const float AutoRingRadius = 8f;
    private const float AutoRingStroke = 3f;
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

        try { _chestSvg = _resourceManager.LoadImage("Resources.icons.features.chest.svg"); }
        catch { _chestSvg = null; }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null)
            return;

        canvas.DrawColor(new SKColor(238, 242, 245));

        if (context.GameState is MainGameState mainGameState)
        {
            var islandState = mainGameState.CurrentIslandState;
            if (islandState != null)
            {
                if (islandState.VisibleIslandMaps.TryGetValue(islandState.PlayerCivilization.Index, out var visibleMap))
                {
                    var playerIdx = islandState.PlayerCivilization.Index;
                    islandState.HarvestLastTimesByCivilization.TryGetValue(playerIdx, out var manualTimes);
                    islandState.AutomaticHarvestLastTimesByCivilization.TryGetValue(playerIdx, out var autoTimes);

                    var banditPositions = new HashSet<HexCoord>(islandState.Bandits.Select(b => b.Position));
                    var treasureTrovePositions = new HashSet<HexCoord>(islandState.TreasureTroves
                        .Where(t => !t.Claimed).Select(t => t.Position));
                    DrawIslandMap(canvas, visibleMap, playerIdx, mainGameState.Clock.CurrentTick, manualTimes, autoTimes, islandState.BanditCooldownUntil, banditPositions, treasureTrovePositions);
                }
            }
        }
    }

    private void DrawIslandMap(SKCanvas canvas, IslandMap map, int playerIdx,
        long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        Dictionary<HexCoord, long>? autoTimes,
        Dictionary<HexCoord, long>? banditCooldownUntil,
        HashSet<HexCoord>? banditPositions,
        HashSet<HexCoord>? treasureTrovePositions = null)
    {
        foreach (var (coord, tile) in map.Tiles)
        {
            var (x, y) = AxialToIsland(coord.Q, coord.R);
            DrawHexagonTile(canvas, coord, x, y, tile, playerIdx, currentTick, manualTimes, autoTimes, banditCooldownUntil, banditPositions, treasureTrovePositions);
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
        Dictionary<HexCoord, long>? autoTimes,
        Dictionary<HexCoord, long>? banditCooldownUntil,
        HashSet<HexCoord>? banditPositions,
        HashSet<HexCoord>? treasureTrovePositions = null)
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

        if (treasureTrovePositions?.Contains(coord) == true)
            DrawTreasureTroveMarker(canvas, centerX, centerY);

        DrawHarvestIndicator(canvas, centerX, centerY, tile, playerIdx, currentTick, manualTimes, autoTimes, banditCooldownUntil, banditPositions);

        if (DebugOverlayRenderer.DebugMode && _textPaint != null && tile.Coord != null)
        {
            _textPaint.Color = SKColors.Black;
            canvas.DrawText($"{tile.Coord.Q},{tile.Coord.R}", centerX, centerY + HexSize / 2.5f, SKTextAlign.Center, _textFont, _textPaint);
        }
    }

    private void DrawTreasureTroveMarker(SKCanvas canvas, float cx, float cy)
    {
        var picture = _chestSvg?.Picture;
        if (picture == null) return;

        float scale = ChestIconSize / ChestSvgViewBox;
        canvas.Save();
        canvas.Translate(cx - ChestIconSize / 2f, cy - ChestIconSize / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    /// <summary>
    /// Dessine l'indicateur de récolte au centre de l'hex :
    /// camembert des ressources récoltables + anneau de cooldown manuel + anneau de cooldown automatique.
    /// </summary>
    private void DrawHarvestIndicator(SKCanvas canvas, float cx, float cy, HexTile tile,
        int playerIdx, long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        Dictionary<HexCoord, long>? autoTimes,
        Dictionary<HexCoord, long>? banditCooldownUntil,
        HashSet<HexCoord>? banditPositions)
    {
        if (_dotPaint == null || _ringBgPaint == null || _ringProgressPaint == null)
            return;

        var manualResources = _harvestController.GetManualHarvestableResources(playerIdx, tile.Coord);
        var autoResources = _harvestController.GetAutomaticHarvestableResources(playerIdx, tile.Coord);

        // Anneau bandit (le plus externe)
        bool banditHere = banditPositions?.Contains(tile.Coord) == true;
        if (banditHere)
        {
            // Bandit présent : anneau plein fixe
            DrawBanditCooldownRing(canvas, cx, cy, ratio: 1f);
        }
        else if (banditCooldownUntil != null
            && banditCooldownUntil.TryGetValue(tile.Coord, out var banditUntil)
            && currentTick < banditUntil)
        {
            // Cooldown de départ : anneau qui se vide
            float ratio = Math.Clamp(
                (float)(currentTick - (banditUntil - BanditController.DepartureCooldownTicks)) / BanditController.DepartureCooldownTicks,
                0f, 1f);
            DrawBanditCooldownRing(canvas, cx, cy, ratio);
        }

        if (autoResources.Count > 0)
            DrawCooldownRing(canvas, cx, cy, AutoRingRadius, AutoRingStroke,
                tile.Coord, currentTick, autoTimes,
                _harvestController.GetEffectiveAutoHarvestCooldownTicks(playerIdx, tile.Coord),
                new SKColor(60, 60, 60, 150),
                new SKColor(255, 200, 60, 230));

        if (manualResources.Count > 0)
            DrawCooldownRing(canvas, cx, cy, ManualRingRadius, ManualRingStroke,
                tile.Coord, currentTick, manualTimes,
                _harvestController.GetManualHarvestCooldownTicks(playerIdx),
                new SKColor(60, 60, 60, 150),
                new SKColor(160, 230, 160, 230));

        // Point central : camembert des ressources manuelles récoltables
        if (manualResources.Count > 0)
            DrawResourcePie(canvas, cx, cy, DotRadius, manualResources);
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

        foreach (var path in _hexPathCache.Values)
            path.Dispose();
        _hexPathCache.Clear();

        _disposed = true;
    }
}
