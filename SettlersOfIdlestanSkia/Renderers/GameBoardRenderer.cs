using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Controller;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer de base pour le plateau de jeu hexagonal.
/// Responsable du rendu des hexagones, du terrain et des ressources.
/// </summary>
public class GameBoardRenderer : HexBasedRenderer, IGameRenderer
{
    private readonly HarvestController _harvestController;

    private SKPaint? _hexBorderPaint;
    private SKPaint? _hexFillPaint;
    private SKPaint? _textPaint;
    private SKFont? _textFont;
    private SKPaint? _dotPaint;
    private SKPaint? _ringBgPaint;
    private SKPaint? _ringProgressPaint;
    private bool _disposed;

    private static readonly TimeSpan ManualCooldown = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AutoCooldown = TimeSpan.FromSeconds(5);

    // Dimensions de l'indicateur (outer edge ≈ 50 % du rayon de l'hex = 20 px)
    private const float DotRadius = 5f;
    private const float ManualRingRadius = 5f;
    private const float ManualRingStroke = 3f;
    private const float AutoRingRadius = 8f;
    private const float AutoRingStroke = 3f;

    private static readonly Dictionary<TerrainType, SKColor> TerrainColors = new()
    {
        { TerrainType.Forest,   new SKColor(34, 139, 34) },
        { TerrainType.Hill,     new SKColor(210, 180, 140) },
        { TerrainType.Plain,    new SKColor(144, 238, 144) },
        { TerrainType.Mountain, new SKColor(139, 69, 19) },
        { TerrainType.Desert,   new SKColor(238, 214, 175) },
        { TerrainType.Water,    new SKColor(30, 144, 255) },
    };

    public GameBoardRenderer(HarvestController harvestController)
    {
        _harvestController = harvestController;
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

                    DrawIslandMap(canvas, visibleMap, playerIdx, mainGameState.Clock.CurrentTime, manualTimes, autoTimes);
                }
            }
        }
    }

    private void DrawIslandMap(SKCanvas canvas, IslandMap map, int playerIdx,
        DateTimeOffset currentTime,
        Dictionary<HexCoord, DateTimeOffset>? manualTimes,
        Dictionary<HexCoord, DateTimeOffset>? autoTimes)
    {
        foreach (var (coord, tile) in map.Tiles)
        {
            var (x, y) = AxialToIsland(coord.Q, coord.R);
            DrawHexagonTile(canvas, x, y, HexSize, tile, playerIdx, currentTime, manualTimes, autoTimes);
        }
    }

    private void DrawHexagonTile(SKCanvas canvas, float centerX, float centerY, float size, HexTile tile,
        int playerIdx, DateTimeOffset currentTime,
        Dictionary<HexCoord, DateTimeOffset>? manualTimes,
        Dictionary<HexCoord, DateTimeOffset>? autoTimes)
    {
        var points = GetHexagonPoints(centerX, centerY, size);

        if (_hexFillPaint != null)
        {
            _hexFillPaint.Color = TerrainColors.TryGetValue(tile.TerrainType, out var color)
                ? color
                : new SKColor(200, 200, 200);

            canvas.DrawPath(PointsToPath(points), _hexFillPaint);
        }

        if (_hexBorderPaint != null)
            canvas.DrawPath(PointsToPath(points), _hexBorderPaint);

        DrawHarvestIndicator(canvas, centerX, centerY, tile, playerIdx, currentTime, manualTimes, autoTimes);

        if (DebugOverlayRenderer.DebugMode && _textPaint != null && tile.Coord != null)
        {
            _textPaint.Color = SKColors.Black;
            canvas.DrawText($"{tile.Coord.Q},{tile.Coord.R}", centerX, centerY + size / 2.5f, SKTextAlign.Center, _textFont, _textPaint);
        }
    }

    /// <summary>
    /// Dessine l'indicateur de récolte au centre de l'hex :
    /// camembert des ressources récoltables + anneau de cooldown manuel + anneau de cooldown automatique.
    /// </summary>
    private void DrawHarvestIndicator(SKCanvas canvas, float cx, float cy, HexTile tile,
        int playerIdx, DateTimeOffset currentTime,
        Dictionary<HexCoord, DateTimeOffset>? manualTimes,
        Dictionary<HexCoord, DateTimeOffset>? autoTimes)
    {
        if (_dotPaint == null || _ringBgPaint == null || _ringProgressPaint == null)
            return;

        var manualResources = _harvestController.GetManualHarvestableResources(playerIdx, tile.Coord);
        var autoResources = _harvestController.GetAutomaticHarvestableResources(playerIdx, tile.Coord);

        // Anneau extérieur : cooldown automatique (or/jaune) — seulement si des ressources auto existent
        if (autoResources.Count > 0)
            DrawCooldownRing(canvas, cx, cy, AutoRingRadius, AutoRingStroke,
                tile.Coord, currentTime, autoTimes, AutoCooldown,
                new SKColor(60, 60, 60, 150),
                new SKColor(255, 200, 60, 230));

        // Anneau intérieur : cooldown manuel (vert clair) — seulement si des ressources manuelles existent
        if (manualResources.Count > 0)
            DrawCooldownRing(canvas, cx, cy, ManualRingRadius, ManualRingStroke,
                tile.Coord, currentTime, manualTimes, ManualCooldown,
                new SKColor(60, 60, 60, 150),
                new SKColor(160, 230, 160, 230));

        // Point central : camembert des ressources manuelles récoltables
        if (manualResources.Count > 0)
            DrawResourcePie(canvas, cx, cy, DotRadius, manualResources);
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
        HexCoord coord, DateTimeOffset currentTime,
        Dictionary<HexCoord, DateTimeOffset>? lastTimes,
        TimeSpan cooldownDuration,
        SKColor bgColor, SKColor progressColor)
    {
        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);

        _ringBgPaint!.Color = bgColor;
        _ringBgPaint.StrokeWidth = strokeWidth;
        canvas.DrawOval(rect, _ringBgPaint);

        float ratio = 1f;
        if (lastTimes != null && lastTimes.TryGetValue(coord, out var lastTime))
            ratio = Math.Clamp((float)((currentTime - lastTime).TotalSeconds / cooldownDuration.TotalSeconds), 0f, 1f);

        if (ratio > 0f)
        {
            _ringProgressPaint!.Color = progressColor;
            _ringProgressPaint.StrokeWidth = strokeWidth;
            canvas.DrawArc(rect, -90f, ratio * 360f, false, _ringProgressPaint);
        }
    }

    private void DrawHexagon(SKCanvas canvas, float centerX, float centerY, float size)
    {
        var points = GetHexagonPoints(centerX, centerY, size);

        if (_hexFillPaint != null)
        {
            _hexFillPaint.Color = new SKColor(200, 220, 240);
            canvas.DrawPath(PointsToPath(points), _hexFillPaint);
        }

        if (_hexBorderPaint != null)
            canvas.DrawPath(PointsToPath(points), _hexBorderPaint);

        if (_textPaint != null)
        {
            _textPaint.Color = SKColors.Black;
            canvas.DrawText($"({centerX:F0},{centerY:F0})", centerX, centerY, SKTextAlign.Center, _textFont, _textPaint);
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
        _disposed = true;
    }
}
