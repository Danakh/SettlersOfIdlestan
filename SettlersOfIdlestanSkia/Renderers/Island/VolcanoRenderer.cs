using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Military;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.IslandFeatures;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Renderers.Debug;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Island;

/// <summary>
/// Rendu du volcan : icône dormante / active selon la proximité de l'éruption,
/// tremblement dans les 3 dernières secondes, et particules fireball vers les villes touchées.
/// </summary>
public class VolcanoRenderer : HexBasedRenderer, IGameRenderer
{
    // Seuils (en ticks, 100 ticks = 1 s à vitesse normale)
    private const long TicksBeforeIconSwitch = 1_000L; // 10 s → eruption SVG
    private const long TicksBeforeShake     = 300L;    // 3 s  → tremblement

    private const float IconSize         = 32f;
    private const float FireballDuration = 0.7f;
    private const float FireballIconSize = 20f;
    private const float ShakeFrequency   = 35f;   // rad/s
    private const float ShakeMaxAmplitude = 4f;   // px

    private sealed class FireballParticle
    {
        public SKPoint From;
        public SKPoint To;
        public float Progress;
    }

    private readonly List<FireballParticle> _particles = new();
    private readonly ResourceManager _resourceManager;
    private SKSvg? _dormantSvg;
    private SKSvg? _eruptionSvg;
    private SKSvg? _fireballSvg;
    private SKPaint? _fireballPaint;
    private bool _disposed;

    public VolcanoRenderer(ResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void Initialize(SKSize canvasSize)
    {
        _dormantSvg  = _resourceManager.LoadImage("Resources.icons.features.volcano-dormant.svg");
        _eruptionSvg = _resourceManager.LoadImage("Resources.icons.features.volcano-eruption.svg");
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
        if (context.GameState is not MainGameState mgs) return;
        var worldState = mgs.CurrentWorldState;
        if (worldState == null) return;

        long currentTick = mgs.Clock.CurrentTick;
        float totalTime  = context.TotalTime;
        float dt         = context.DeltaTime;

        // Filtre de visibilité
        VisibleIslandMap? visibleMap = null;
        if (!DebugSettings.ShowFullMap && !mgs.GodState.AscensionState.IsEyeOfGodActive)
            worldState.Visibility.GetForZ(worldState.CurrentViewedLayer)
                .TryGetValue(worldState.PlayerCivilization.Index, out visibleMap);

        // ── Icônes volcans ─────────────────────────────────────────────────────
        foreach (var volcano in worldState.Features.OfType<VolcanoFeature>())
        {
            if (!volcano.Found) continue;
            if (volcano.Position.Z != context.CurrentLayer) continue;
            if (visibleMap != null && !visibleMap.HasTile(volcano.Position)) continue;

            long ticksUntil = volcano.LastEruptionTick + VolcanoController.EruptionIntervalTicks - currentTick;

            bool useEruptionIcon = ticksUntil <= TicksBeforeIconSwitch;
            bool doShake         = ticksUntil > 0 && ticksUntil <= TicksBeforeShake;

            var svg = useEruptionIcon ? _eruptionSvg : _dormantSvg;
            var (cx, cy) = AxialToIsland(volcano.Position.Q, volcano.Position.R);

            float offsetX = 0f;
            if (doShake)
            {
                float eruptionProgress = 1f - (float)ticksUntil / TicksBeforeShake;
                float amplitude = eruptionProgress * ShakeMaxAmplitude;
                offsetX = (float)Math.Sin(totalTime * ShakeFrequency) * amplitude;
            }

            DrawIcon(canvas, new SKPoint(cx + offsetX, cy), svg, IconSize);
        }

        // ── Particules fireball ────────────────────────────────────────────────
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.Progress = Math.Min(1f, p.Progress + dt / FireballDuration);

            float t     = Smoothstep(p.Progress);
            var   pos   = Lerp(p.From, p.To, t);
            float alpha = p.Progress < 0.75f ? 1f : (1f - p.Progress) / 0.25f;

            DrawFireball(canvas, pos, alpha);

            if (p.Progress >= 1f)
                _particles.RemoveAt(i);
        }
    }

    private static void DrawIcon(SKCanvas canvas, SKPoint center, SKSvg? svg, float size)
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

    private void DrawFireball(SKCanvas canvas, SKPoint center, float alpha)
    {
        var picture = _fireballSvg?.Picture;
        if (picture == null || _fireballPaint == null) return;

        byte alphaB = (byte)(Math.Clamp(alpha, 0f, 1f) * 255);
        _fireballPaint.Color = SKColors.White.WithAlpha(alphaB);
        _fireballPaint.ColorFilter = null;

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
        _dormantSvg  = null;
        _eruptionSvg = null;
        _fireballSvg = null;
        _fireballPaint?.Dispose();
        _fireballPaint = null;
        _disposed = true;
    }
}
