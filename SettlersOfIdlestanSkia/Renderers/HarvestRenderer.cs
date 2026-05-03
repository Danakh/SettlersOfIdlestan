using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Renderer responsable de l'affichage des particules de récolte.
/// Les particules se déplacent du centre de l'hex source vers le centre de la ville.
/// </summary>
public class HarvestRenderer : IGameRenderer
{
    private readonly HarvestParticleSystem _particleSystem;
    private SKPaint? _particlePaint;
    private bool _disposed;

    public HarvestRenderer(HarvestParticleSystem particleSystem)
    {
        _particleSystem = particleSystem ?? throw new ArgumentNullException(nameof(particleSystem));
    }

    public void Initialize(SKSize canvasSize)
    {
        _particlePaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_particlePaint == null)
            return;

        // Met à jour les particules
        _particleSystem.Update(context.DeltaTime);

        // Affiche chaque particule
        foreach (var particle in _particleSystem.Particles)
        {
            var position = particle.GetCurrentPosition();
            var alpha = particle.GetCurrentAlpha();

            // Crée une couleur avec l'alpha dynamique
            var color = particle.Color;
            var colorWithAlpha = new SKColor(color.Red, color.Green, color.Blue, alpha);

            _particlePaint.Color = colorWithAlpha;

            // Taille de la particule (réduit vers la fin pour un effet de disparition)
            float size = 5f * (1f - particle.Progress * 0.5f);

            canvas.DrawCircle(position, size, _particlePaint);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _particlePaint?.Dispose();
        _disposed = true;
    }
}
