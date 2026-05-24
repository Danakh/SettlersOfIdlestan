using SkiaSharp;
using SettlersOfIdlestan.Model.Bandits;
using SettlersOfIdlestan.Model.Game;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestanSkia.Core;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers;

public class BanditRenderer : HexBasedRenderer, IGameRenderer
{
    private const float AnimationDuration = 1f; // secondes

    private sealed class BanditVisual
    {
        public HexCoord ModelPosition = new(0, 0);
        public SKPoint From;
        public SKPoint To;
        public float Progress = 1f; // 1 = positionné, pas d'animation en cours
        public bool Initialized;
    }

    private readonly List<BanditVisual> _visuals = new();
    private bool _disposed;
    private SKPaint? _iconPaint;
    private SKFont? _iconFont;

    public void Initialize(SKSize canvasSize)
    {
        _iconPaint = new SKPaint
        {
            Color = new SKColor(60, 0, 0, 220),
            IsAntialias = true
        };
        _iconFont = new SKFont(SKTypeface.Default, 16);
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

            // Détection de mouvement : le modèle a changé de position
            if (!v.ModelPosition.Equals(bandit.Position))
            {
                v.From = CurrentVisualPoint(v);
                v.To = targetPoint;
                v.Progress = 0f;
                v.ModelPosition = bandit.Position;
            }

            // Avance l'animation
            if (v.Progress < 1f)
                v.Progress = Math.Min(1f, v.Progress + dt / AnimationDuration);

            var pos = Lerp(v.From, v.To, Smoothstep(v.Progress));
            canvas.DrawText("☠", pos.X, pos.Y + 6f, SKTextAlign.Center, _iconFont!, _iconPaint!);
        }
    }

    private void SyncVisuals(IList<Bandit> bandits)
    {
        // Ajuste la taille de _visuals pour correspondre à la liste de bandits
        while (_visuals.Count < bandits.Count)
        {
            var v = new BanditVisual();
            var pos = HexToPoint(bandits[_visuals.Count].Position);
            v.ModelPosition = bandits[_visuals.Count].Position;
            v.From = pos;
            v.To = pos;
            v.Progress = 1f;
            v.Initialized = true;
            _visuals.Add(v);
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
        _iconPaint?.Dispose();
        _iconFont?.Dispose();
        _disposed = true;
    }
}
