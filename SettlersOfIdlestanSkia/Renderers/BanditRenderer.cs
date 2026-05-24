using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using System.Collections.Generic;
using System.Reflection;

namespace SettlersOfIdlestanSkia.Renderers;

public class BanditRenderer : HexBasedRenderer, IGameRenderer
{
    private const float AnimationDuration = 1f;
    private const float IconSize = 24f;

    private sealed class BanditVisual
    {
        public HexCoord ModelPosition = new(0, 0);
        public SKPoint From;
        public SKPoint To;
        public float Progress = 1f;
    }

    private readonly List<BanditVisual> _visuals = new();
    private SKSvg? _svg;
    private bool _disposed;

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
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (context.GameState is not MainGameState mgs) return;
        var islandState = mgs.CurrentIslandState;
        if (islandState == null) return;

        var bandits = islandState.Bandits;
        SyncVisuals(bandits);

        float dt = context.DeltaTime;

        for (int i = 0; i < bandits.Count; i++)
        {
            var bandit = bandits[i];
            var v = _visuals[i];

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

            var pos = Lerp(v.From, v.To, Smoothstep(v.Progress));
            DrawIcon(canvas, pos);
        }
    }

    private void DrawIcon(SKCanvas canvas, SKPoint center)
    {
        var picture = _svg?.Picture;
        if (picture == null) return;

        // La viewBox du SVG est 64×64 → on scale à IconSize
        float scale = IconSize / 64f;
        canvas.Save();
        canvas.Translate(center.X - IconSize / 2f, center.Y - IconSize / 2f);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Restore();
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

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static float Smoothstep(float t)
        => t * t * (3f - 2f * t);

    public void Dispose()
    {
        if (_disposed) return;
        _svg = null;
        _disposed = true;
    }
}
