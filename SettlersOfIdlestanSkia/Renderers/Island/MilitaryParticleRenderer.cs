using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SettlersOfIdlestanSkia.Renderers.Island;

public class MilitaryParticleRenderer : HexBasedRenderer, IGameRenderer
{
    private const float SegmentDuration = 0.35f;
    private const float ParticleIconSize = 16f;
    private const float SvgNativeSize = 64f;

    private sealed class MilitaryParticle
    {
        public List<SKPoint> Path = new();
        public float Progress;
    }

    private readonly List<MilitaryParticle> _particles = new();
    private SKSvg? _attackSvg;
    private SKPaint? _paint;
    private bool _disposed;

    public void Initialize(SKSize canvasSize)
    {
        _paint = new SKPaint { IsAntialias = true };
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
        Func<bool> isPrestigeTransitionPending)
    {
        militaryController.SoldierAttackedCity += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            EmitParticle(args.Path);
        };
    }

    private void EmitParticle(List<Vertex> vertexPath)
    {
        if (vertexPath.Count == 0) return;
        var pathPoints = vertexPath.Select(v => VertexToIsland(v)).ToList();
        _particles.Add(new MilitaryParticle { Path = pathPoints, Progress = 0f });
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        float dt = context.DeltaTime;

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            int segments = Math.Max(1, p.Path.Count - 1);
            float duration = segments * SegmentDuration;
            p.Progress = Math.Min(1f, p.Progress + dt / duration);

            float t = p.Progress * segments;
            int seg = Math.Min((int)t, segments - 1);
            float segT = Smoothstep(Math.Clamp(t - seg, 0f, 1f));

            var from = p.Path[seg];
            var to = p.Path[Math.Min(seg + 1, p.Path.Count - 1)];
            var pos = new SKPoint(
                from.X + (to.X - from.X) * segT,
                from.Y + (to.Y - from.Y) * segT);

            float alpha = p.Progress > 0.8f ? (1f - p.Progress) / 0.2f : 1f;
            DrawIcon(canvas, pos, alpha);

            if (p.Progress >= 1f)
                _particles.RemoveAt(i);
        }
    }

    private void DrawIcon(SKCanvas canvas, SKPoint center, float alpha)
    {
        var picture = _attackSvg?.Picture;
        if (picture == null || _paint == null) return;

        byte a = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _paint.Color = new SKColor(220, 60, 60, a);
        _paint.ColorFilter = SKColorFilter.CreateBlendMode(new SKColor(220, 60, 60, a), SKBlendMode.SrcIn);

        float scale = ParticleIconSize / SvgNativeSize;
        canvas.Save();
        canvas.Translate(center.X - ParticleIconSize / 2f, center.Y - ParticleIconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, SvgNativeSize, SvgNativeSize), _paint);
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
        _disposed = true;
    }
}
