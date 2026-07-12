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
    private MonumentService? _monumentService;

    private SKPaint? _hexBorderPaint;
    private SKPaint? _hexFillPaint;
    private SKPaint? _textPaint;
    private SKFont? _textFont;
    private SKPaint? _ringBgPaint;
    private SKPaint? _ringProgressPaint;
    private SKPaint? _textIconPaint;
    private SKFont? _textIconFont;
    private SKPaint? _selectedMonumentPaint;
    private SKPaint? _corruptionPaint;
    private SKPaint? _dominionPaint;
    private bool _disposed;

    private readonly Dictionary<HexCoord, SKPath> _hexPathCache = new();
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private const float CorruptionCircleRadiusFactor = 0.8f;
    private const float DominionCircleRadiusFactor = 0.55f;

    private const float MonumentSelectionRadius = 28f;
    private const float ManualRingRadius = 5f;
    private const float ResourceIconSize = 12f;
    private const float SvgNaturalSize = 32f;
    private const float ManualRingStroke = 3f;
    // Arc auto : centré au vertex de la ville, rayon juste supérieur à CityRadius (8 px)
    private const float AutoArcBaseRadius = 10f;
    private const float AutoArcGap = 5f;   // décalage entre deux arcs concentriques (mine + carrière)
    private const float AutoArcStroke = 3f;
    private const float PlunderRingRadius = 12f;
    private const float PlunderRingStroke = 3f;

    private static readonly Dictionary<TerrainType, SKColor> TerrainColors = new()
    {
        { TerrainType.Forest,   new SKColor(34, 139, 34) },
        { TerrainType.Hill,     new SKColor(210, 145, 80) },
        { TerrainType.Plain,    new SKColor(144, 238, 144) },
        { TerrainType.Mountain, new SKColor(95, 90, 85) },
        { TerrainType.Desert,      new SKColor(238, 214, 175) },
        { TerrainType.Water,       new SKColor(30, 144, 255) },
        { TerrainType.DeepWater,   new SKColor(8, 32, 74) },   // Bleu marine profond
        { TerrainType.MithrilVein, new SKColor(60, 90, 140) },   // Bleu-gris profond
        { TerrainType.CrystalCave, new SKColor(185, 130, 220) }, // Violet cristallin
        { TerrainType.MushroomCave, new SKColor(150, 105, 125) }, // Mauve fongique
    };

    public GameBoardRenderer(HarvestController harvestController, ResourceManager resourceManager)
    {
        _harvestController = harvestController;
        _resourceManager = resourceManager;
    }

    public void ConnectMonumentService(MonumentService monumentService) => _monumentService = monumentService;

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

        _selectedMonumentPaint = new SKPaint
        {
            Color = new SKColor(255, 215, 0, 230),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };

        _corruptionPaint = new SKPaint
        {
            Color = new SKColor(160, 32, 240, 210),
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        _dominionPaint = new SKPaint
        {
            Color = new SKColor(255, 215, 0, 210),
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState == null)
            return;

        if (context.GameState is MainGameState mainGameState)
        {
            var worldState = mainGameState.CurrentWorldState;
            if (worldState != null && worldState.CurrentViewedLayer == LayerState.UnderworldZ && worldState.Layers.ContainsKey(LayerState.UnderworldZ))
            {
                if (!DebugSettings.ExportTransparentBackground)
                    canvas.DrawColor(new SKColor(15, 8, 30));
                var playerIdx = worldState.PlayerCivilization.Index;
                IslandMap? underworldMap = (DebugSettings.ShowFullMap || mainGameState.GodState.AscensionState.IsEyeOfGodActive)
                    ? worldState.GetMapForZ(LayerState.UnderworldZ)
                    : worldState.Visibility.GetForZ(LayerState.UnderworldZ).TryGetValue(playerIdx, out var uvm) ? uvm : null;
                if (underworldMap != null)
                {
                    var harvestBlockers = new HashSet<IslandFeature>(worldState.Features.Where(f => f.BlocksHarvestFor(worldState.PlayerCivilization)));
                    var uwCorruption = worldState.Features
                        .OfType<Corruption>()
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => g.Max(c => c.Level));
                    var uwDominion = worldState.Features
                        .OfType<Dominion>()
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => g.Max(d => d.Level));
                    var uwAbyssGate = worldState.Features
                        .OfType<AbyssGate>()
                        .ToDictionary(f => f.Position, f => f.Built);
                    var uwFeaturesByPosition = worldState.Features
                        .Where(f => f.ShouldRenderIcon && (f.SvgIconResourceName != null || f.TextIcon != null))
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => (IEnumerable<IslandFeature>)g);
                    DrawIslandMap(canvas, underworldMap, playerIdx, mainGameState.Clock.CurrentTick, null, null, null, harvestBlockers, uwFeaturesByPosition, uwCorruption, uwDominion, uwAbyssGate, context.TotalTime);

                    var selectedInvestable = _monumentService?.SelectedInvestable;
                    if (selectedInvestable != null && selectedInvestable.Position.Z == LayerState.UnderworldZ && _selectedMonumentPaint != null)
                    {
                        var (wx, wy) = AxialToIsland(selectedInvestable.Position.Q, selectedInvestable.Position.R);
                        canvas.DrawCircle(wx, wy, MonumentSelectionRadius, _selectedMonumentPaint);
                    }
                }
                return;
            }
        }

        if (!DebugSettings.ExportTransparentBackground)
            canvas.DrawColor(new SKColor(238, 242, 245));

        if (context.GameState is MainGameState mgs)
        {
            var worldState = mgs.CurrentWorldState;
            if (worldState != null)
            {
                IslandMap? mapToRender = null;
                if (DebugSettings.ShowFullMap || mgs.GodState.AscensionState.IsEyeOfGodActive)
                    mapToRender = worldState.GetMapForZ(IslandMap.SurfaceLayer);
                else if (worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(worldState.PlayerCivilization.Index, out var visibleMap))
                    mapToRender = visibleMap;

                if (mapToRender != null)
                {
                    var playerIdx = worldState.PlayerCivilization.Index;
                    worldState.HarvestLastTimesByCivilization.TryGetValue(playerIdx, out var manualTimes);

                    var harvestBlockers = new HashSet<IslandFeature>(worldState.Features.Where(f => f.BlocksHarvestFor(worldState.PlayerCivilization)));
                    var featuresByPosition = worldState.Features
                        .Where(f => f.ShouldRenderIcon && (f.SvgIconResourceName != null || f.TextIcon != null))
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => (IEnumerable<IslandFeature>)g);
                    var corruptionByHex = worldState.Features
                        .OfType<Corruption>()
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => g.Max(c => c.Level));
                    var dominionByHex = worldState.Features
                        .OfType<Dominion>()
                        .GroupBy(f => f.Position)
                        .ToDictionary(g => g.Key, g => g.Max(d => d.Level));
                    var abyssGateByHex = worldState.Features
                        .OfType<AbyssGate>()
                        .ToDictionary(f => f.Position, f => f.Built);
                    DrawIslandMap(canvas, mapToRender, playerIdx, mgs.Clock.CurrentTick, manualTimes, worldState.PlunderCooldownUntil, worldState.PlunderCooldownDuration, harvestBlockers, featuresByPosition, corruptionByHex, dominionByHex, abyssGateByHex, context.TotalTime);

                    var selectedInvestable = _monumentService?.SelectedInvestable;
                    if (selectedInvestable != null && _selectedMonumentPaint != null)
                    {
                        var (wx, wy) = AxialToIsland(selectedInvestable.Position.Q, selectedInvestable.Position.R);
                        canvas.DrawCircle(wx, wy, MonumentSelectionRadius, _selectedMonumentPaint);
                    }
                }
            }
        }
    }

    private void DrawIslandMap(SKCanvas canvas, IslandMap map, int playerIdx,
        long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        IReadOnlyDictionary<HexCoord, long>? plunderCooldownUntil,
        Dictionary<HexCoord, long>? plunderCooldownDuration,
        HashSet<IslandFeature>? harvestBlockers,
        Dictionary<HexCoord, IEnumerable<IslandFeature>>? featuresByPosition = null,
        Dictionary<HexCoord, int>? corruptionByHex = null,
        Dictionary<HexCoord, int>? dominionByHex = null,
        Dictionary<HexCoord, bool>? abyssGateByHex = null,
        float totalTime = 0f)
    {
        foreach (var (coord, tile) in map.Tiles)
        {
            var (x, y) = AxialToIsland(coord.Q, coord.R);
            DrawHexagonTile(canvas, coord, x, y, tile, playerIdx, currentTick, manualTimes, plunderCooldownUntil, plunderCooldownDuration, harvestBlockers, featuresByPosition, corruptionByHex, dominionByHex, abyssGateByHex, totalTime);
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
        IReadOnlyDictionary<HexCoord, long>? plunderCooldownUntil,
        Dictionary<HexCoord, long>? plunderCooldownDuration,
        HashSet<IslandFeature>? harvestBlockers,
        Dictionary<HexCoord, IEnumerable<IslandFeature>>? featuresByPosition = null,
        Dictionary<HexCoord, int>? corruptionByHex = null,
        Dictionary<HexCoord, int>? dominionByHex = null,
        Dictionary<HexCoord, bool>? abyssGateByHex = null,
        float totalTime = 0f)
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

        if (corruptionByHex?.TryGetValue(coord, out int corruptLevel) == true && corruptLevel > 0)
            DrawCorruptionCircle(canvas, centerX, centerY, corruptLevel);

        if (dominionByHex?.TryGetValue(coord, out int dominionLevel) == true && dominionLevel > 0)
            DrawDominionCircle(canvas, centerX, centerY, dominionLevel);

        if (abyssGateByHex?.TryGetValue(coord, out bool gateBuilt) == true)
            DrawAbyssGatePortal(canvas, centerX, centerY, gateBuilt, totalTime);

        DrawHarvestIndicator(canvas, centerX, centerY, tile, playerIdx, currentTick, manualTimes, plunderCooldownUntil, plunderCooldownDuration, harvestBlockers);

        if (featuresByPosition?.TryGetValue(coord, out var features) == true)
            foreach (var feature in features)
                DrawFeatureMarker(canvas, centerX, centerY, feature);

        if (DebugSettings.ShowHexCoords && _textPaint != null && tile.Coord != null)
        {
            _textPaint.Color = SKColors.Black;
            SkiaTextUtils.DrawText(canvas, $"{tile.Coord.Q},{tile.Coord.R}", centerX, centerY + HexSize / 2.5f, SKTextAlign.Center, _textFont, _textPaint);
        }
    }

    private void DrawCorruptionCircle(SKCanvas canvas, float cx, float cy, int level)
    {
        if (_corruptionPaint == null) return;
        _corruptionPaint.StrokeWidth = Math.Clamp(level, 1, 10);
        canvas.DrawCircle(cx, cy, HexSize * CorruptionCircleRadiusFactor, _corruptionPaint);
    }

    private void DrawDominionCircle(SKCanvas canvas, float cx, float cy, int level)
    {
        if (_dominionPaint == null) return;
        _dominionPaint.StrokeWidth = Math.Clamp(level, 1, 10);
        canvas.DrawCircle(cx, cy, HexSize * DominionCircleRadiusFactor, _dominionPaint);
    }

    /// <summary>
    /// Portail tourbillonnant de la Faille des Abysses : deux dégradés circulaires (sweep gradient)
    /// tournant à contre-sens, plus un cœur sombre pulsant. Tourne plus vite et plus intensément
    /// une fois la Faille bâtie. Une seule instance existe jamais dans une partie — coût négligeable.
    /// </summary>
    private void DrawAbyssGatePortal(SKCanvas canvas, float cx, float cy, bool built, float totalTime)
    {
        float radius = HexSize * CorruptionCircleRadiusFactor * 0.85f;
        float outerSpeed = built ? 70f : 35f;
        float outerAngleRaw = totalTime * outerSpeed;
        float outerAngle = outerAngleRaw % 360f;

        // Épaisseur de l'anneau extérieur pulsant sinusoïdalement ; le bord extérieur reste fixe à `radius`,
        // le bord intérieur respire pour donner l'effet d'anneau qui "ondule".
        float ringThickness = radius * 0.45f + radius * 0.18f * (float)Math.Sin(totalTime * 1.4f);
        float ringCenterRadius = radius - ringThickness / 2f;

        // Disque de fond plein pour qu'aucun trou n'apparaisse sous l'anneau quand il s'amincit.
        using var voidPaint = new SKPaint { Color = new SKColor(8, 4, 18, 255), IsAntialias = true };
        canvas.DrawCircle(cx, cy, radius, voidPaint);

        using var outerShader = SKShader.CreateSweepGradient(
            new SKPoint(cx, cy),
            new[]
            {
                new SKColor(8, 4, 18, 255),
                new SKColor(140, 50, 220, (byte)(built ? 235 : 190)),
                new SKColor(8, 4, 18, 255),
                new SKColor(90, 25, 170, (byte)(built ? 220 : 170)),
                new SKColor(8, 4, 18, 255),
            },
            null);
        using var outerPaint = new SKPaint
        {
            Shader = outerShader,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = ringThickness,
        };

        canvas.Save();
        canvas.RotateDegrees(outerAngle, cx, cy);
        canvas.DrawCircle(cx, cy, ringCenterRadius, outerPaint);
        canvas.Restore();

        using var innerShader = SKShader.CreateSweepGradient(
            new SKPoint(cx, cy),
            new[]
            {
                new SKColor(200, 150, 255, (byte)(built ? 110 : 70)),
                new SKColor(20, 8, 35, 0),
                new SKColor(200, 150, 255, (byte)(built ? 110 : 70)),
            },
            null);
        using var innerPaint = new SKPaint { Shader = innerShader, IsAntialias = true };

        // L'angle est dérivé de l'angle brut (non wrappé) avant le modulo, pour que le bouclage
        // à 360° reste un multiple exact du facteur 0.6 et ne produise aucun saut visuel.
        float innerAngle = (-outerAngleRaw * 0.6f) % 360f;

        canvas.Save();
        canvas.RotateDegrees(innerAngle, cx, cy);
        canvas.DrawCircle(cx, cy, radius * 0.7f, innerPaint);
        canvas.Restore();

        float pulse = 0.5f + 0.5f * (float)Math.Sin(totalTime * (built ? 4f : 2f));
        using var corePaint = new SKPaint { Color = new SKColor(5, 0, 12, 255), IsAntialias = true };
        canvas.DrawCircle(cx, cy, radius * (0.16f + 0.05f * pulse), corePaint);
    }

    private void DrawFeatureMarker(SKCanvas canvas, float cx, float cy, IslandFeature feature)
    {
        var resourceName = feature.SvgIconResourceName;
        if (resourceName != null)
        {
            SKSvg? svg = _resourceManager.LoadImage(resourceName);
            var picture = svg?.Picture;
            if (picture == null) return;

            float size = feature.SvgIconSize * feature.IconSizeFactor;
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
            SkiaTextUtils.DrawText(canvas, textIcon, cx, cy + _textIconFont.Size / 2f - 1f, SKTextAlign.Center, _textIconFont, _textIconPaint);
    }

    /// <summary>
    /// Dessine l'indicateur de récolte :
    /// - Arcs de cooldown automatique dans le coin de l'hex pointant vers la ville (120°, un arc par bâtiment)
    /// - Anneau de cooldown manuel + camembert des ressources au centre
    /// </summary>
    private void DrawHarvestIndicator(SKCanvas canvas, float cx, float cy, HexTile tile,
        int playerIdx, long currentTick,
        Dictionary<HexCoord, long>? manualTimes,
        IReadOnlyDictionary<HexCoord, long>? plunderCooldownUntil,
        Dictionary<HexCoord, long>? plunderCooldownDuration,
        HashSet<IslandFeature>? harvestBlockers)
    {
        if (_ringBgPaint == null || _ringProgressPaint == null)
            return;

        // Anneau pillage (le plus externe)
        IslandFeature? f = harvestBlockers?.FirstOrDefault(f => f.Position.Equals(tile.Coord));
        if (f != null)
        {
            if (f.CanMove)
                DrawPlunderCooldownRing(canvas, cx, cy, ratio: 1f);
            return;
        }
        else if (plunderCooldownUntil != null
            && plunderCooldownUntil.TryGetValue(tile.Coord, out var plunderUntil)
            && currentTick < plunderUntil)
        {
            long duration = plunderCooldownDuration != null && plunderCooldownDuration.TryGetValue(tile.Coord, out var d) ? d : plunderUntil;
            float ratio = Math.Clamp((float)(currentTick - (plunderUntil - duration)) / duration, 0f, 1f);
            DrawPlunderCooldownRing(canvas, cx, cy, ratio);
        }

        // Arcs de cooldown automatique au vertex de la ville (un arc par bâtiment).
        // Le rayon s'incrémente uniquement pour plusieurs bâtiments de la MÊME ville sur le même hex.
        var autoInfo = _harvestController.GetAutoHarvestInfoForHex(playerIdx, tile.Coord);
        var arcIndexByVertex = new Dictionary<Vertex, int>();
        foreach (var (cityVertex, _, _, lastTick, cooldown) in autoInfo)
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
            DrawResourceIcon(canvas, cx, cy, manualResources[0]);
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

    private void DrawPlunderCooldownRing(SKCanvas canvas, float cx, float cy, float ratio)
    {
        var rect = new SKRect(cx - PlunderRingRadius, cy - PlunderRingRadius, cx + PlunderRingRadius, cy + PlunderRingRadius);

        _ringBgPaint!.Color = new SKColor(80, 0, 0, 150);
        _ringBgPaint.StrokeWidth = PlunderRingStroke;
        canvas.DrawOval(rect, _ringBgPaint);

        if (ratio > 0f)
        {
            _ringProgressPaint!.Color = new SKColor(220, 40, 40, 230);
            _ringProgressPaint.StrokeWidth = PlunderRingStroke;
            canvas.DrawArc(rect, -90f, ratio * 360f, false, _ringProgressPaint);
        }
    }

    private void DrawResourceIcon(SKCanvas canvas, float cx, float cy, Resource resource)
    {
        if (!_resourceIcons.TryGetValue(resource, out var svg) || svg?.Picture == null)
            return;

        float scale = ResourceIconSize / SvgNaturalSize;
        canvas.Save();
        canvas.Translate(cx - ResourceIconSize / 2f, cy - ResourceIconSize / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(svg.Picture);
        canvas.Restore();
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
        _ringBgPaint?.Dispose();
        _ringProgressPaint?.Dispose();
        _textIconPaint?.Dispose();
        _textIconFont?.Dispose();
        _selectedMonumentPaint?.Dispose();

        foreach (var path in _hexPathCache.Values)
            path.Dispose();
        _hexPathCache.Clear();

        _disposed = true;
    }
}
