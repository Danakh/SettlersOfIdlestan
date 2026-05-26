using SkiaSharp;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

public class HarvestParticle
{
    public SKPoint StartPosition { get; set; }
    public SKPoint EndPosition { get; set; }
    /// <summary>Point de contrôle de la courbe de Bézier quadratique.</summary>
    public SKPoint ControlPoint { get; set; }
    public float TimeRemaining { get; set; }
    public float Duration { get; set; }
    public Resource Resource { get; set; }
    public float Progress => 1f - (TimeRemaining / Duration);
    public bool IsAlive => TimeRemaining > 0;

    public HarvestParticle(SKPoint startPos, SKPoint endPos, SKPoint controlPoint, Resource resource, float duration = 0.8f)
    {
        StartPosition = startPos;
        EndPosition = endPos;
        ControlPoint = controlPoint;
        Resource = resource;
        Duration = duration;
        TimeRemaining = duration;
    }

    public void Update(float deltaTime)
    {
        TimeRemaining -= deltaTime;
    }

    public SKPoint GetCurrentPosition()
    {
        float t = Progress;
        // ease-out-cubic
        t = 1f - (1f - t) * (1f - t) * (1f - t);

        // Bézier quadratique: B(t) = (1-t)²·P0 + 2(1-t)t·P1 + t²·P2
        float mt = 1f - t;
        return new SKPoint(
            mt * mt * StartPosition.X + 2f * mt * t * ControlPoint.X + t * t * EndPosition.X,
            mt * mt * StartPosition.Y + 2f * mt * t * ControlPoint.Y + t * t * EndPosition.Y
        );
    }

    public byte GetCurrentAlpha()
    {
        float remainingRatio = TimeRemaining / Duration;
        if (remainingRatio < 0.3f)
            return (byte)(remainingRatio / 0.3f * 255);
        return 255;
    }
}

public class HarvestParticleSystem
{
    private readonly List<HarvestParticle> _particles = new();

    public IReadOnlyList<HarvestParticle> Particles => _particles.AsReadOnly();

    /// <summary>
    /// Émet une particule unique (trajectoire droite, point de contrôle au milieu).
    /// </summary>
    public void EmitParticle(SKPoint startPos, SKPoint endPos, Resource resource, float duration = 0.8f)
    {
        SKPoint controlPoint = new SKPoint((startPos.X + endPos.X) / 2f, (startPos.Y + endPos.Y) / 2f);
        _particles.Add(new HarvestParticle(startPos, endPos, controlPoint, resource, duration));
    }

    /// <summary>
    /// Émet une particule par unité de ressource dans le ResourceSet. Chaque particule décrit une
    /// spline de Bézier dont le point de contrôle est réparti équitablement en largeur par rapport
    /// à l'axe start→end (step de 15 px entre deux trajectoires adjacentes).
    /// </summary>
    public void EmitParticles(SKPoint startPos, SKPoint endPos, IEnumerable<KeyValuePair<Resource, int>> resources, float duration = 0.8f)
    {
        var flat = new List<Resource>();
        foreach (var kvp in resources)
            for (int i = 0; i < kvp.Value; i++)
                flat.Add(kvp.Key);

        int n = flat.Count;
        if (n == 0) return;

        SKPoint mid = new SKPoint((startPos.X + endPos.X) / 2f, (startPos.Y + endPos.Y) / 2f);

        float dx = endPos.X - startPos.X;
        float dy = endPos.Y - startPos.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        float perpX = 0f, perpY = 0f;
        if (len > 0f) { perpX = -dy / len; perpY = dx / len; }

        const float spreadStep = 15f;
        float halfSpan = (n - 1) * spreadStep / 2f;

        for (int i = 0; i < n; i++)
        {
            float offset = n > 1 ? (i * spreadStep - halfSpan) : 0f;
            SKPoint controlPoint = new SKPoint(mid.X + perpX * offset, mid.Y + perpY * offset);
            _particles.Add(new HarvestParticle(startPos, endPos, controlPoint, flat[i], duration));
        }
    }

    public void Update(float deltaTime)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            _particles[i].Update(deltaTime);
            if (!_particles[i].IsAlive)
                _particles.RemoveAt(i);
        }
    }

    public void Clear()
    {
        _particles.Clear();
    }
}
