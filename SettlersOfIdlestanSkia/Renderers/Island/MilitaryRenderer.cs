using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Renderers.Overlay;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SettlersOfIdlestanSkia.Renderers.Island;

public class MilitaryRenderer : HexBasedRenderer, IGameRenderer
{
    private const float SegmentDuration = 0.35f;
    private const float ParticleIconSize = 16f;
    private const float SvgNativeSize = 64f;
    private const float ArrowSize = 9f;
    private const float ArrowTipOffset = 18f;

    private sealed class MilitaryParticle
    {
        public List<SKPoint> Path = new();
        public List<Vertex>? VertexPath;
        public Vertex? SourceVertex;
        public Vertex? TargetVertex;
        public float Progress;
    }

    private readonly List<MilitaryParticle> _particles = new();
    private readonly List<MilitaryParticle> _reinforceParticles = new();
    private SKSvg? _attackSvg;
    private SKPaint? _paint;
    private SKPaint? _reinforcePaint;
    private SKPaint? _flowRedPaint;
    private SKPaint? _flowGreenPaint;
    private SKPaint? _arrowPaint;
    private SKPaint? _dragLinePaint;
    private SKPaint? _dragCirclePaint;
    private MilitaryController? _militaryController;
    private GameControllerService? _gameControllerService;
    private MilitaryInteractionService? _interactionService;
    private readonly TooltipRenderer _tooltipRenderer;
    private readonly LocalizationService _localizationService;
    private bool _disposed;

    public MilitaryRenderer(TooltipRenderer tooltipRenderer, LocalizationService localizationService)
    {
        _tooltipRenderer = tooltipRenderer;
        _localizationService = localizationService;
    }

    public void Initialize(SKSize canvasSize)
    {
        _paint = new SKPaint { IsAntialias = true };
        _reinforcePaint = new SKPaint { IsAntialias = true };
        _flowRedPaint = new SKPaint
        {
            Color = new SKColor(220, 60, 60, 220),
            StrokeWidth = 2.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 12f, 5f }, 0f),
        };
        _flowGreenPaint = new SKPaint
        {
            Color = new SKColor(50, 200, 80, 220),
            StrokeWidth = 2.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 12f, 5f }, 0f),
        };
        _arrowPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        _dragLinePaint = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 180),
            StrokeWidth = 2f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash(new float[] { 10f, 6f }, 0f),
        };
        _dragCirclePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3f };

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(
            $"{assembly.GetName().Name}.Resources.icons.military.attack.svg");
        if (stream != null)
        {
            _attackSvg = new SKSvg();
            _attackSvg.Load(stream);
        }
    }

    public void Connect(
        MilitaryController militaryController,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive)
    {
        _militaryController = militaryController;
        _gameControllerService = gameControllerService;
        militaryController.SoldierAttackedCity += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            if (args.TargetCity.Z != gameControllerService.CurrentWorldState?.CurrentViewedLayer) return;
            EmitParticle(args.Path);
        };
        militaryController.ReinforcementSent += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            if (args.TargetCity.Z != gameControllerService.CurrentWorldState?.CurrentViewedLayer) return;
            EmitReinforceParticle(args.Path);
        };
    }

    public void ConnectInteractionService(MilitaryInteractionService service)
    {
        _interactionService = service;
    }

    private void EmitParticle(List<Vertex> vertexPath)
    {
        if (vertexPath.Count == 0) return;
        var pathPoints = vertexPath.Select(v => VertexToIsland(v)).ToList();
        _particles.Add(new MilitaryParticle
        {
            Path = pathPoints,
            VertexPath = vertexPath,
            SourceVertex = vertexPath[0],
            TargetVertex = vertexPath[^1],
            Progress = 0f,
        });
    }

    private void EmitReinforceParticle(List<Vertex> vertexPath)
    {
        if (vertexPath.Count == 0) return;
        var pathPoints = vertexPath.Select(v => VertexToIsland(v)).ToList();
        _reinforceParticles.Add(new MilitaryParticle
        {
            Path = pathPoints,
            VertexPath = vertexPath,
            SourceVertex = vertexPath[0],
            TargetVertex = vertexPath[^1],
            Progress = 0f,
        });
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        DrawFlowLines(canvas);
        DrawDragInteraction(canvas, context);

        float dt = context.DeltaTime;

        IslandMap? visibleMap = null;
        var worldState = _gameControllerService?.CurrentWorldState;
        if (worldState != null)
        {
            if (DebugSettings.ShowFullMap)
                visibleMap = worldState.CurrentViewedMap;
            else if (worldState.Visibility.GetForZ(worldState.CurrentViewedLayer)
                .TryGetValue(worldState.PlayerCivilization.Index, out var vm))
                visibleMap = vm;
        }

        AdvanceParticles(canvas, _particles, dt, reinforce: false, visibleMap: visibleMap);
        AdvanceParticles(canvas, _reinforceParticles, dt, reinforce: true, visibleMap: visibleMap);
    }

    private void DrawFlowLines(SKCanvas canvas)
    {
        var worldState = _gameControllerService?.CurrentWorldState;
        var playerCiv = _gameControllerService?.PlayerCivilization;
        if (worldState == null || playerCiv == null || _flowRedPaint == null || _flowGreenPaint == null || _arrowPaint == null) return;

        var allCities = worldState.Civilizations.SelectMany(c => c.Cities).ToList();

        foreach (var civ in worldState.Civilizations)
        {
            foreach (var sourceCity in civ.Cities)
            {
                if (sourceCity.FlowTarget == null) continue;
                if (sourceCity.Position.Z != worldState.CurrentViewedLayer) continue;

                var targetCity = allCities.FirstOrDefault(c => c.Position.Equals(sourceCity.FlowTarget));
                if (targetCity == null) continue;

                bool sourceIsPlayer = sourceCity.CivilizationIndex == playerCiv.Index;
                bool targetIsPlayer = targetCity.CivilizationIndex == playerCiv.Index;

                // Affiche uniquement les flux impliquant au moins une cité du joueur
                if (!sourceIsPlayer && !targetIsPlayer) continue;

                bool isReinforcement = targetCity.CivilizationIndex == sourceCity.CivilizationIndex;
                var linePaint = isReinforcement ? _flowGreenPaint : _flowRedPaint;
                var arrowColor = isReinforcement ? new SKColor(50, 200, 80, 220) : new SKColor(220, 60, 60, 220);

                var sourcePt = VertexToIsland(sourceCity.Position);
                var targetPt = VertexToIsland(targetCity.Position);
                canvas.DrawLine(sourcePt, targetPt, linePaint);
                DrawArrowhead(canvas, sourcePt, targetPt, arrowColor);
            }

            foreach (var sourceCity in civ.Cities)
            {
                if (sourceCity.MonsterAttackTarget == null) continue;
                if (sourceCity.Position.Z != worldState.CurrentViewedLayer) continue;
                if (civ.Index != playerCiv.Index) continue;

                var monster = worldState.Features.OfType<MonsterFeature>()
                    .FirstOrDefault(f => f.Position.Equals(sourceCity.MonsterAttackTarget));
                if (monster == null) continue;

                var sourcePt = VertexToIsland(sourceCity.Position);
                var targetPt = HexToIsland(monster.Position);
                var arrowColor = new SKColor(220, 60, 60, 220);
                canvas.DrawLine(sourcePt, targetPt, _flowRedPaint);
                DrawArrowhead(canvas, sourcePt, targetPt, arrowColor);
            }
        }
    }

    private SKPoint HexToIsland(HexCoord hex)
    {
        var (x, y) = AxialToIsland(hex.Q, hex.R);
        return new SKPoint(x, y);
    }

    private void DrawDragInteraction(SKCanvas canvas, GameRenderContext context)
    {
        if (_interactionService?.DragSourceCity is not { } sourceCity) return;
        if (sourceCity.Position.Z != context.CurrentLayer) return;
        if (_dragLinePaint == null || _dragCirclePaint == null || _arrowPaint == null) return;

        var sourcePt = VertexToIsland(sourceCity.Position);
        var screen = _interactionService.DragCurrentScreenPoint;
        var cursorIsland = new SKPoint(
            screen.X / context.ZoomLevel + context.CameraPosition.X,
            screen.Y / context.ZoomLevel + context.CameraPosition.Y);

        canvas.DrawLine(sourcePt, cursorIsland, _dragLinePaint);
        DrawArrowhead(canvas, sourcePt, cursorIsland, new SKColor(255, 255, 255, 180), tipOffset: 4f);

        float pulse = 0.7f + 0.3f * MathF.Sin(context.TotalTime * 5f);
        _dragCirclePaint.Color = new SKColor(255, 215, 0, (byte)(200 * pulse));
        canvas.DrawCircle(sourcePt, 18f, _dragCirclePaint);

        if (_interactionService.DragTargetCity is { } targetCity && targetCity != sourceCity)
        {
            var targetPt = VertexToIsland(targetCity.Position);
            bool inRange = _interactionService.DragTargetIsInRange;
            var playerIndex = _gameControllerService?.PlayerCivilization?.Index ?? -1;
            bool isAlly = targetCity.CivilizationIndex == playerIndex;

            _dragCirclePaint.Color = inRange
                ? (isAlly ? new SKColor(50, 200, 80, 220) : new SKColor(220, 60, 60, 220))
                : new SKColor(150, 150, 150, 180);
            canvas.DrawCircle(targetPt, 20f, _dragCirclePaint);

            if (!inRange)
                _tooltipRenderer.SetTooltip(_localizationService.Get(MilitaryInteractionService.TooFarMessageKey), screen);
        }
        else if (_interactionService.DragTargetMonster is { } targetMonster)
        {
            var targetPt = HexToIsland(targetMonster.Position);
            var availability = _interactionService.DragTargetMonsterAvailability;
            _dragCirclePaint.Color = availability switch
            {
                MonsterAttackAvailability.Available => new SKColor(220, 60, 60, 220),
                MonsterAttackAvailability.RequiresWatchtower => new SKColor(230, 180, 40, 220),
                _ => new SKColor(150, 150, 150, 180),
            };
            canvas.DrawCircle(targetPt, 20f, _dragCirclePaint);

            string? reasonKey = availability switch
            {
                MonsterAttackAvailability.RequiresWatchtower => MilitaryInteractionService.RequiresWatchtowerMessageKey,
                MonsterAttackAvailability.TooFar => MilitaryInteractionService.TooFarMessageKey,
                _ => null,
            };
            if (reasonKey != null)
                _tooltipRenderer.SetTooltip(_localizationService.Get(reasonKey), screen);
        }
    }

    /// <summary>Dessine une tête de flèche remplie à l'extrémité d'un segment.</summary>
    private void DrawArrowhead(SKCanvas canvas, SKPoint from, SKPoint to, SKColor color,
        float tipOffset = ArrowTipOffset, float size = ArrowSize)
    {
        if (_arrowPaint == null) return;
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < tipOffset + size) return;

        float ux = dx / len;
        float uy = dy / len;

        var tip = new SKPoint(to.X - ux * tipOffset, to.Y - uy * tipOffset);
        var p1 = new SKPoint(tip.X - ux * size - uy * (size * 0.45f), tip.Y - uy * size + ux * (size * 0.45f));
        var p2 = new SKPoint(tip.X - ux * size + uy * (size * 0.45f), tip.Y - uy * size - ux * (size * 0.45f));

        using var path = new SKPath();
        path.MoveTo(tip);
        path.LineTo(p1);
        path.LineTo(p2);
        path.Close();

        _arrowPaint.Color = color;
        canvas.DrawPath(path, _arrowPaint);
    }

    private void AdvanceParticles(SKCanvas canvas, List<MilitaryParticle> particles, float dt, bool reinforce, IslandMap? visibleMap)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            int segments = Math.Max(1, p.Path.Count - 1);
            float duration = segments * SegmentDuration;
            p.Progress = Math.Min(1f, p.Progress + dt / duration);

            float t = p.Progress * segments;
            int seg = Math.Min((int)t, segments - 1);
            float segT = Smoothstep(Math.Clamp(t - seg, 0f, 1f));

            if (visibleMap != null)
            {
                bool sourceVisible = p.SourceVertex != null && IsVertexVisible(p.SourceVertex, visibleMap);
                bool targetVisible = p.TargetVertex != null && IsVertexVisible(p.TargetVertex, visibleMap);
                if (!sourceVisible && !targetVisible)
                {
                    if (p.Progress >= 1f) particles.RemoveAt(i);
                    continue;
                }

                if (p.VertexPath != null && p.VertexPath.Count > 1)
                {
                    var vFrom = p.VertexPath[seg];
                    var vTo = p.VertexPath[Math.Min(seg + 1, p.VertexPath.Count - 1)];
                    if (!IsSegmentVisible(vFrom, vTo, visibleMap))
                    {
                        if (p.Progress >= 1f) particles.RemoveAt(i);
                        continue;
                    }
                }
            }

            var from = p.Path[seg];
            var to = p.Path[Math.Min(seg + 1, p.Path.Count - 1)];
            var pos = new SKPoint(
                from.X + (to.X - from.X) * segT,
                from.Y + (to.Y - from.Y) * segT);

            float alpha = p.Progress > 0.8f ? (1f - p.Progress) / 0.2f : 1f;
            if (reinforce)
                DrawReinforceIcon(canvas, pos, alpha);
            else
                DrawIcon(canvas, pos, alpha);

            if (p.Progress >= 1f)
                particles.RemoveAt(i);
        }
    }

    private static bool IsVertexVisible(Vertex v, IslandMap visibleMap)
    {
        if (v.Z != visibleMap.Z) return false;
        foreach (var hex in v.GetHexes())
            if (visibleMap.HasTile(hex)) return true;
        return false;
    }

    private static bool IsSegmentVisible(Vertex v1, Vertex v2, IslandMap visibleMap)
    {
        if (v1.Z != visibleMap.Z) return false;
        var v1Hexes = v1.GetHexes();
        var v2Hexes = v2.GetHexes();
        int sharedCount = 0;
        foreach (var h1 in v1Hexes)
            foreach (var h2 in v2Hexes)
                if (h1.Equals(h2))
                {
                    if (!visibleMap.HasTile(h1)) return false;
                    sharedCount++;
                }
        return sharedCount > 0;
    }

    private void DrawIcon(SKCanvas canvas, SKPoint center, float alpha)
        => DrawSvgIcon(canvas, center, alpha, new SKColor(220, 60, 60), _paint);

    private void DrawReinforceIcon(SKCanvas canvas, SKPoint center, float alpha)
        => DrawSvgIcon(canvas, center, alpha, new SKColor(50, 180, 80), _reinforcePaint);

    private void DrawSvgIcon(SKCanvas canvas, SKPoint center, float alpha, SKColor color, SKPaint? paint)
    {
        var picture = _attackSvg?.Picture;
        if (picture == null || paint == null) return;

        byte a = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        var tinted = new SKColor(color.Red, color.Green, color.Blue, a);
        paint.Color = tinted;
        paint.ColorFilter = SKColorFilter.CreateBlendMode(tinted, SKBlendMode.SrcIn);

        float scale = ParticleIconSize / SvgNativeSize;
        canvas.Save();
        canvas.Translate(center.X - ParticleIconSize / 2f, center.Y - ParticleIconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, SvgNativeSize, SvgNativeSize), paint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
    }

    private static float Smoothstep(float t) => t * t * (3f - 2f * t);

    public void Dispose()
    {
        if (_disposed) return;
        _attackSvg = null;
        _paint?.Dispose();
        _paint = null;
        _reinforcePaint?.Dispose();
        _reinforcePaint = null;
        _flowRedPaint?.Dispose();
        _flowRedPaint = null;
        _flowGreenPaint?.Dispose();
        _flowGreenPaint = null;
        _arrowPaint?.Dispose();
        _arrowPaint = null;
        _dragLinePaint?.Dispose();
        _dragLinePaint = null;
        _dragCirclePaint?.Dispose();
        _dragCirclePaint = null;
        _disposed = true;
    }
}
