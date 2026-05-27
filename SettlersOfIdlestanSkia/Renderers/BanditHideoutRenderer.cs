using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SettlersOfIdlestanSkia.Renderers;

public class BanditHideoutRenderer : HexBasedRenderer, IGameRenderer
{
    private const float IconSize = 32f;
    private const float AttackParticleDuration = 0.5f;
    private const float AttackParticleIconSize = 16f;

    private sealed class AttackParticle
    {
        public SKPoint From;
        public SKPoint To;
        public float Progress;
    }

    private readonly List<AttackParticle> _attackParticles = new();
    private readonly ResourceManager _resourceManager;
    private SKSvg? _svg;
    private SKSvg? _attackSvg;
    private SKPaint? _attackParticlePaint;
    private bool _disposed;

    public BanditHideoutRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"{assembly.GetName().Name}.Resources.icons.features.skullcave.svg";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            _svg = new SKSvg();
            _svg.Load(stream);
        }

        _attackParticlePaint = new SKPaint { IsAntialias = true };
        try { _attackSvg = _resourceManager.LoadImage("Resources.icons.military.attack.svg"); } catch { }
    }

    public void EmitAttackParticle(Vertex cityVertex, HexCoord hideoutPosition)
    {
        var from = VertexToIsland(cityVertex);
        var (bx, by) = AxialToIsland(hideoutPosition.Q, hideoutPosition.R);
        _attackParticles.Add(new AttackParticle { From = from, To = new SKPoint(bx, by), Progress = 0f });
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return;
        var islandState = mgs.CurrentIslandState;
        if (islandState == null) return;

        islandState.VisibleIslandMaps.TryGetValue(islandState.PlayerCivilization.Index, out var visibleMap);

        float dt = context.DeltaTime;

        foreach (var hideout in islandState.Features.OfType<BanditHideout>())
        {
            if (!hideout.Found) continue;
            if (visibleMap != null && !visibleMap.HasTile(hideout.Position)) continue;

            var (x, y) = AxialToIsland(hideout.Position.Q, hideout.Position.R);
            DrawIcon(canvas, new SKPoint(x, y));
        }

        for (int i = _attackParticles.Count - 1; i >= 0; i--)
        {
            var p = _attackParticles[i];
            p.Progress = Math.Min(1f, p.Progress + dt / AttackParticleDuration);
            float t = Smoothstep(p.Progress);
            var pos = Lerp(p.From, p.To, t);
            float alpha = p.Progress < 0.7f ? 1f : (1f - p.Progress) / 0.3f;
            DrawAttackParticle(canvas, pos, alpha);
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

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static float Smoothstep(float t)
        => t * t * (3f - 2f * t);

    public void Dispose()
    {
        if (_disposed) return;
        _svg = null;
        _attackSvg = null;
        _attackParticlePaint?.Dispose();
        _attackParticlePaint = null;
        _disposed = true;
    }
}
