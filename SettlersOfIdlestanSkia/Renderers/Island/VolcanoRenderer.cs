using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Particules de fireball qui volent du hex volcan vers chaque ville touchée lors d'une éruption.
/// </summary>
public class VolcanoRenderer : HexBasedRenderer, IGameRenderer
{
    private const float FireballDuration = 0.7f;
    private const float FireballIconSize = 20f;

    private sealed class FireballParticle
    {
        public SKPoint From;
        public SKPoint To;
        public float Progress;
    }

    private readonly List<FireballParticle> _particles = new();
    private readonly ResourceManager _resourceManager;
    private SKSvg? _fireballSvg;
    private SKPaint? _fireballPaint;
    private bool _disposed;

    public VolcanoRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        _fireballSvg = _resourceManager.LoadImage("Resources.icons.features.fireball.svg");
        _fireballPaint = new SKPaint { IsAntialias = true };
    }

    public void Connect(
        VolcanoController volcanoController,
        GameControllerService gameControllerService,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive)
    {
        volcanoController.VolcanoHitCity += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            var worldState = gameControllerService.CurrentWorldState;
            if (worldState == null) return;
            if (args.VolcanoPosition.Z != worldState.CurrentViewedLayer) return;

            var (fx, fy) = AxialToIsland(args.VolcanoPosition.Q, args.VolcanoPosition.R);
            var to = VertexToIsland(args.TargetCityVertex);
            _particles.Add(new FireballParticle { From = new SKPoint(fx, fy), To = to, Progress = 0f });
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState) return;

        float dt = context.DeltaTime;

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Progress = Math.Min(1f, p.Progress + dt / FireballDuration);

            float t = Smoothstep(p.Progress);
            var pos = Lerp(p.From, p.To, t);
            float alpha = p.Progress < 0.75f ? 1f : (1f - p.Progress) / 0.25f;

            DrawFireball(canvas, pos, alpha);

            if (p.Progress >= 1f)
                _particles.RemoveAt(i);
        }
    }

    private void DrawFireball(SKCanvas canvas, SKPoint center, float alpha)
    {
        var picture = _fireballSvg?.Picture;
        if (picture == null || _fireballPaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _fireballPaint.ColorFilter = SKColorFilter.CreateBlendMode(
            new SKColor(255, 255, 255, alphaB), SKBlendMode.SrcIn);

        float naturalSize = Math.Max(picture.CullRect.Width, picture.CullRect.Height);
        float scale = naturalSize > 0f ? FireballIconSize / naturalSize : 1f;

        canvas.Save();
        canvas.Translate(center.X - FireballIconSize / 2f, center.Y - FireballIconSize / 2f);
        canvas.Scale(scale);
        canvas.SaveLayer(new SKRect(0, 0, naturalSize, naturalSize), _fireballPaint);
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
        _fireballSvg = null;
        _fireballPaint?.Dispose();
        _fireballPaint = null;
        _disposed = true;
    }
}
