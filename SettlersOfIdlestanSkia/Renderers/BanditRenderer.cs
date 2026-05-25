using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using System.Collections.Generic;
using System.Reflection;

namespace SettlersOfIdlestanSkia.Renderers;

public class BanditRenderer : HexBasedRenderer, IGameRenderer
{
    private const float AnimationDuration = 1f;
    private const float RaidAnimDuration = 0.8f;
    private const float ResourceFlyDuration = 0.6f;
    private const float IconSize = 24f;
    private const float ResourceIconSize = 18f;

    private sealed class BanditVisual
    {
        public HexCoord ModelPosition = new(0, 0);
        public SKPoint From;
        public SKPoint To;
        public float Progress = 1f;

        // Raid bounce animation
        public long KnownLastRaidTick = -1;
        public float RaidAnimProgress = 1f;  // 0..1, 1 = idle
        public SKPoint RaidHomePos;
        public SKPoint RaidTargetPos;

        // Flying resource animation
        public Resource? FlyingResource = null;
        public float ResourceFlyProgress = 1f; // 0..1, 1 = done
    }

    private const float AttackParticleDuration = 0.5f;
    private const float AttackParticleIconSize = 16f;

    private sealed class AttackParticle
    {
        public SKPoint From;
        public SKPoint To;
        public float Progress; // 0..1
    }

    private readonly List<BanditVisual> _visuals = new();
    private readonly List<AttackParticle> _attackParticles = new();
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();
    private SKSvg? _svg;
    private SKSvg? _attackSvg;
    private SKPaint? _resourceFlyPaint;
    private SKPaint? _attackParticlePaint;
    private bool _disposed;

    public BanditRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"{assembly.GetName().Name}.Resources.icons.military.bandit.svg";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            _svg = new SKSvg();
            _svg.Load(stream);
        }

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

    public void EmitAttackParticle(Vertex cityVertex, HexCoord banditPosition)
    {
        var from = VertexToIsland(cityVertex);
        var (bx, by) = AxialToIsland(banditPosition.Q, banditPosition.R);
        EmitAttackParticle(from, new SKPoint(bx, by));
    }

    public void EmitAttackParticle(SKPoint from, SKPoint to)
    {
        _attackParticles.Add(new AttackParticle { From = from, To = to, Progress = 0f });
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return;
        var islandState = mgs.CurrentIslandState;
        if (islandState == null) return;

        var bandits = islandState.Bandits;
        SyncVisuals(bandits);

        islandState.VisibleIslandMaps.TryGetValue(islandState.PlayerCivilization.Index, out var visibleMap);

        float dt = context.DeltaTime;

        for (int i = 0; i < bandits.Count; i++)
        {
            var bandit = bandits[i];
            var v = _visuals[i];

            // Movement animation
            var targetPoint = HexToPoint(bandit.Position);

            if (!v.ModelPosition.Equals(bandit.Position))
            {
                v.From = CurrentVisualPoint(v);
                v.To = targetPoint;
                v.Progress = 0f;
                v.ModelPosition = bandit.Position;
            }

            if (v.Progress < 1f)
                v.Progress = Math.Min(1f, v.Progress + dt / AnimationDuration);

            var normalPos = Lerp(v.From, v.To, Smoothstep(v.Progress));

            // Detect new raid
            if (bandit.LastRaidTick > 0
                && bandit.LastRaidTick != v.KnownLastRaidTick
                && bandit.LastRaidTick != bandit.LastMovedTick // special case where raid was reset because of move. Skip Raid animation
                && bandit.LastRaidTargetVertex != null)
            {
                v.KnownLastRaidTick = bandit.LastRaidTick;
                v.RaidAnimProgress = 0f;
                v.RaidHomePos = normalPos;
                v.RaidTargetPos = VertexToPoint(bandit.LastRaidTargetVertex);
                v.FlyingResource = null;
                if (bandit.LastStolenResource != null
                    && Enum.TryParse<Resource>(bandit.LastStolenResource, out var res))
                    v.FlyingResource = res;
                v.ResourceFlyProgress = 1f;
            }

            // Advance raid animation
            if (v.RaidAnimProgress < 1f)
            {
                v.RaidAnimProgress = Math.Min(1f, v.RaidAnimProgress + dt / RaidAnimDuration);
                if (v.RaidAnimProgress >= 0.45f && v.ResourceFlyProgress >= 1f && v.FlyingResource != null)
                    v.ResourceFlyProgress = 0f;
            }

            // Advance resource fly animation
            if (v.ResourceFlyProgress < 1f)
                v.ResourceFlyProgress = Math.Min(1f, v.ResourceFlyProgress + dt / ResourceFlyDuration);

            if (visibleMap != null && !visibleMap.HasTile(bandit.Position))
                continue;

            // Calculate bandit position (with or without raid offset)
            SKPoint pos;
            if (v.RaidAnimProgress < 1f)
            {
                float t = v.RaidAnimProgress;
                pos = t < 0.5f
                    ? Lerp(v.RaidHomePos, v.RaidTargetPos, Smoothstep(t * 2f))
                    : Lerp(v.RaidTargetPos, v.RaidHomePos, Smoothstep((t - 0.5f) * 2f));
            }
            else
            {
                pos = normalPos;
            }

            DrawIcon(canvas, pos);

            // Draw flying stolen resource
            if (v.ResourceFlyProgress < 1f && v.FlyingResource != null)
            {
                var flyPos = Lerp(v.RaidTargetPos, v.RaidHomePos, Smoothstep(v.ResourceFlyProgress));
                DrawResourceIcon(canvas, flyPos, v.FlyingResource.Value, 1f - v.ResourceFlyProgress);
            }
        }

        // Draw attack particles
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

    private void DrawIcon(SKCanvas canvas, SKPoint center)
    {
        var picture = _svg?.Picture;
        if (picture == null) return;

        float scale = IconSize / 64f;
        canvas.Save();
        canvas.Translate(center.X - IconSize / 2f, center.Y - IconSize / 2f);
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
        canvas.Restore(); // restore SaveLayer
        canvas.Restore(); // restore transform
    }

    private void SyncVisuals(IList<Bandit> bandits)
    {
        while (_visuals.Count < bandits.Count)
        {
            var pos = HexToPoint(bandits[_visuals.Count].Position);
            _visuals.Add(new BanditVisual
            {
                ModelPosition = bandits[_visuals.Count].Position,
                From = pos,
                To = pos,
                Progress = 1f
            });
        }
        while (_visuals.Count > bandits.Count)
            _visuals.RemoveAt(_visuals.Count - 1);
    }

    private SKPoint CurrentVisualPoint(BanditVisual v)
        => Lerp(v.From, v.To, Smoothstep(v.Progress));

    private SKPoint HexToPoint(HexCoord hex)
    {
        var (x, y) = AxialToIsland(hex.Q, hex.R);
        return new SKPoint(x, y);
    }
    private SKPoint VertexToPoint(Vertex vertex)
    {
        return VertexToIsland(vertex);
    }

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static float Smoothstep(float t)
        => t * t * (3f - 2f * t);

    public void Dispose()
    {
        if (_disposed) return;
        _svg = null;
        _attackSvg = null;
        _resourceFlyPaint?.Dispose();
        _resourceFlyPaint = null;
        _attackParticlePaint?.Dispose();
        _attackParticlePaint = null;
        _disposed = true;
    }
}
