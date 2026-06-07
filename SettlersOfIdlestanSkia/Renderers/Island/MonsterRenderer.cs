using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Model.Monsters;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers.Island;

public class MonsterRenderer : HexBasedRenderer, IGameRenderer
{
    private const float AnimationDuration = 1f;
    private const float AttackAnimDuration = 0.8f;
    private const float ResourceFlyDuration = 0.6f;
    private const float ResourceIconSize = 18f;
    private const float AttackParticleDuration = 0.5f;
    private const float AttackParticleIconSize = 16f;

    private sealed class MonsterVisual
    {
        public HexCoord ModelPosition = new(0, 0, IslandMap.SurfaceLayer);
        // Movement
        public HexCoord FromHex = new(0, 0, IslandMap.SurfaceLayer);
        public SKPoint From;
        public SKPoint To;
        public float MoveProgress = 1f;
        // Attack
        public long KnownLastAttackTick = -1;
        public float AttackAnimProgress = 1f;
        public SKPoint HomePos;
        public SKPoint TargetPos;
        public Resource? FlyingResource;
        public float ResourceFlyProgress = 1f;
    }

    private sealed class AttackParticle
    {
        public SKPoint From;
        public SKPoint To;
        public float Progress;
    }

    private readonly List<MonsterVisual> _monsterVisuals = new();
    private readonly List<AttackParticle> _attackParticles = new();
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    private SKSvg? _attackSvg;
    private SKPaint? _resourceFlyPaint;
    private SKPaint? _attackParticlePaint;
    private bool _disposed;

    public MonsterRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        foreach (Resource resource in Enum.GetValues<Resource>())
        {
            string name = resource.ToString().ToLower();
            try { _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg"); }
            catch { _resourceIcons[resource] = null; }
        }

        _resourceFlyPaint = new SKPaint { Color = SKColors.White };
        _attackParticlePaint = new SKPaint { IsAntialias = true };
        try { _attackSvg = _resourceManager.LoadImage("Resources.icons.military.attack.svg"); } catch { }
    }

    public void Connect(
        MilitaryController militaryController,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive)
    {
        militaryController.SoldierAttackedMonster += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            var worldState = gameControllerService.CurrentWorldState;
            if (worldState == null) return;
            if (!IsSourceOrDestinationVisible(worldState, args.CityVertex, args.MonsterPosition)) return;
            EmitAttackParticle(args.CityVertex, args.MonsterPosition);
        };
    }

    private static bool IsSourceOrDestinationVisible(WorldState worldState, Vertex source, HexCoord target)
    {
        if (!worldState.Visibility.GetForZ(source.Z).TryGetValue(worldState.PlayerCivilization.Index, out var visibleMap))
            return true;
        if (visibleMap.HasTile(target)) return true;
        foreach (var hex in source.GetHexes())
            if (visibleMap.HasTile(hex)) return true;
        return false;
    }

    private void EmitAttackParticle(Vertex cityVertex, HexCoord targetPosition)
    {
        var from = VertexToIsland(cityVertex);
        var (bx, by) = AxialToIsland(targetPosition.Q, targetPosition.R);
        _attackParticles.Add(new AttackParticle { From = from, To = new SKPoint(bx, by), Progress = 0f });
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return;
        var worldState = mgs.CurrentWorldState;
        if (worldState == null) return;

        VisibleIslandMap? visibleMap = null;
        if (!DebugSettings.ShowFullMap)
            worldState.Visibility.GetForZ(worldState.CurrentViewedLayer).TryGetValue(worldState.PlayerCivilization.Index, out visibleMap);

        float dt = context.DeltaTime;

        var monsters = worldState.Features.OfType<MonsterFeature>().ToList();
        SyncMonsterVisuals(monsters);

        for (int i = 0; i < monsters.Count; i++)
        {
            var monster = monsters[i];
            var v = _monsterVisuals[i];

            // Movement animation
            var targetPoint = HexToPoint(monster.Position);
            if (!v.ModelPosition.Equals(monster.Position))
            {
                v.FromHex = v.ModelPosition;
                v.From = CurrentVisualPoint(v);
                v.To = targetPoint;
                v.MoveProgress = 0f;
                v.ModelPosition = monster.Position;
            }
            if (v.MoveProgress < 1f)
                v.MoveProgress = Math.Min(1f, v.MoveProgress + dt / AnimationDuration);

            var normalPos = Lerp(v.From, v.To, Smoothstep(v.MoveProgress));

            // Attack animation
            if (monster.LastAttackTick > 0
                && monster.LastAttackTick != v.KnownLastAttackTick
                && monster.LastAttackTick != monster.LastMovedTick
                && monster.LastAttackTargetVertex != null)
            {
                v.KnownLastAttackTick = monster.LastAttackTick;
                v.AttackAnimProgress = 0f;
                v.HomePos = normalPos;
                v.TargetPos = VertexToIsland(monster.LastAttackTargetVertex);
                v.FlyingResource = null;
                if (monster.LastAttackResourcesString != null)
                {
                    var first = monster.LastAttackResourcesString.Split(',')[0];
                    if (Enum.TryParse<Resource>(first, out var res))
                        v.FlyingResource = res;
                }
                v.ResourceFlyProgress = 1f;
            }
            if (v.AttackAnimProgress < 1f)
            {
                v.AttackAnimProgress = Math.Min(1f, v.AttackAnimProgress + dt / AttackAnimDuration);
                if (v.AttackAnimProgress >= 0.45f && v.ResourceFlyProgress >= 1f && v.FlyingResource != null)
                    v.ResourceFlyProgress = 0f;
            }
            if (v.ResourceFlyProgress < 1f)
                v.ResourceFlyProgress = Math.Min(1f, v.ResourceFlyProgress + dt / ResourceFlyDuration);

            // Rendering — filtered by found / layer / visibility
            if (!monster.Found) continue;
            if (monster.Position.Z != context.CurrentLayer) continue;
            if (visibleMap != null)
            {
                bool destVisible = visibleMap.HasTile(monster.Position);
                bool fromVisible = v.MoveProgress < 1f && visibleMap.HasTile(v.FromHex);
                if (!destVisible && !fromVisible) continue;
            }

            var svgName = monster.SvgIconResourceName;
            if (svgName == null) continue;

            SKPoint pos;
            if (v.AttackAnimProgress < 1f)
            {
                float t = v.AttackAnimProgress;
                pos = t < 0.5f
                    ? Lerp(v.HomePos, v.TargetPos, Smoothstep(t * 2f))
                    : Lerp(v.TargetPos, v.HomePos, Smoothstep((t - 0.5f) * 2f));
            }
            else
            {
                pos = normalPos;
            }

            SKSvg? svg = null;
            try { svg = _resourceManager.LoadImage(svgName); } catch { }
            DrawSvgMonsterIcon(canvas, pos, svg, monster.SvgIconSize * monster.IconSizeFactor);

            if (v.ResourceFlyProgress < 1f && v.FlyingResource != null)
            {
                var flyPos = Lerp(v.TargetPos, v.HomePos, Smoothstep(v.ResourceFlyProgress));
                DrawResourceIcon(canvas, flyPos, v.FlyingResource.Value, 1f - v.ResourceFlyProgress);
            }
        }

        // Attack particles (soldiers → monster)
        for (int i = _attackParticles.Count - 1; i >= 0; i--)
        {
            var p = _attackParticles[i];
            p.Progress = Math.Min(1f, p.Progress + dt / AttackParticleDuration);
            float t = Smoothstep(p.Progress);
            var pos2 = Lerp(p.From, p.To, t);
            float alpha = p.Progress < 0.7f ? 1f : (1f - p.Progress) / 0.3f;
            DrawAttackParticle(canvas, pos2, alpha);
            if (p.Progress >= 1f)
                _attackParticles.RemoveAt(i);
        }
    }

    private void SyncMonsterVisuals(IList<MonsterFeature> monsters)
    {
        while (_monsterVisuals.Count < monsters.Count)
        {
            var hex = monsters[_monsterVisuals.Count].Position;
            var pos = HexToPoint(hex);
            _monsterVisuals.Add(new MonsterVisual
            {
                ModelPosition = hex,
                FromHex = hex,
                From = pos,
                To = pos,
                HomePos = pos,
            });
        }
        while (_monsterVisuals.Count > monsters.Count)
            _monsterVisuals.RemoveAt(_monsterVisuals.Count - 1);
    }

    private SKPoint CurrentVisualPoint(MonsterVisual v)
        => Lerp(v.From, v.To, Smoothstep(v.MoveProgress));

    private static void DrawSvgMonsterIcon(SKCanvas canvas, SKPoint center, SKSvg? svg, float size)
    {
        var picture = svg?.Picture;
        if (picture == null) return;

        float naturalSize = Math.Max(picture.CullRect.Width, picture.CullRect.Height);
        float scale = naturalSize > 0f ? size / naturalSize : 1f;
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private void DrawAttackParticle(SKCanvas canvas, SKPoint center, float alpha)
    {
        var picture = _attackSvg?.Picture;
        if (picture == null || _attackParticlePaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _attackParticlePaint.Color = new SKColor(255, 120, 80, alphaB);
        _attackParticlePaint.ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(255, 120, 80, alphaB), SKBlendMode.SrcIn);

        const float size = AttackParticleIconSize;
        float scale = size / 64f;
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, 64, 64), _attackParticlePaint);
        canvas.DrawPicture(picture);
        canvas.Restore();
        canvas.Restore();
    }

    private void DrawResourceIcon(SKCanvas canvas, SKPoint center, Resource resource, float alpha)
    {
        if (!_resourceIcons.TryGetValue(resource, out var svg) || svg?.Picture == null) return;
        if (_resourceFlyPaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _resourceFlyPaint.Color = SKColors.White.WithAlpha(alphaB);

        const float size = ResourceIconSize;
        float scale = size / 64f;
        canvas.Save();
        canvas.Translate(center.X - size / 2f, center.Y - size / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, 64, 64), _resourceFlyPaint);
        canvas.DrawPicture(svg.Picture);
        canvas.Restore();
        canvas.Restore();
    }

    private SKPoint HexToPoint(HexCoord hex)
    {
        var (x, y) = AxialToIsland(hex.Q, hex.R);
        return new SKPoint(x, y);
    }

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static float Smoothstep(float t)
        => t * t * (3f - 2f * t);

    public void Dispose()
    {
        if (_disposed) return;
        _attackSvg = null;
        _resourceFlyPaint?.Dispose();
        _resourceFlyPaint = null;
        _attackParticlePaint?.Dispose();
        _attackParticlePaint = null;
        _disposed = true;
    }
}
