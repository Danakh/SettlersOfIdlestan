using SkiaSharp;
using SettlersOfIdlestan.Model.IslandMap;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Représente une particule de récolte animée.
/// </summary>
public class HarvestParticle
{
    /// <summary>
    /// Position de départ (centre de l'hex source).
    /// </summary>
    public SKPoint StartPosition { get; set; }

    /// <summary>
    /// Position de fin (centre de la ville).
    /// </summary>
    public SKPoint EndPosition { get; set; }

    /// <summary>
    /// Temps restant avant la disparition (0 à Duration).
    /// </summary>
    public float TimeRemaining { get; set; }

    /// <summary>
    /// Durée totale de l'animation en secondes.
    /// </summary>
    public float Duration { get; set; }

    /// <summary>
    /// Type de ressource associé à la particule.
    /// </summary>
    public Resource Resource { get; set; }

    /// <summary>
    /// Progression de l'animation (0 à 1).
    /// </summary>
    public float Progress => 1f - (TimeRemaining / Duration);

    /// <summary>
    /// Indique si la particule est toujours active.
    /// </summary>
    public bool IsAlive => TimeRemaining > 0;

    public HarvestParticle(SKPoint startPos, SKPoint endPos, Resource resource, float duration = 0.8f)
    {
        StartPosition = startPos;
        EndPosition = endPos;
        Resource = resource;
        Duration = duration;
        TimeRemaining = duration;
    }

    /// <summary>
    /// Met à jour l'état de la particule.
    /// </summary>
    public void Update(float deltaTime)
    {
        TimeRemaining -= deltaTime;
    }

    /// <summary>
    /// Retourne la position actuelle interpolée entre le début et la fin.
    /// </summary>
    public SKPoint GetCurrentPosition()
    {
        float t = Progress;
        // Utilise une easing function (ease-out-cubic) pour une animation plus naturelle
        t = 1f - (1f - t) * (1f - t) * (1f - t);

        return new SKPoint(
            StartPosition.X + (EndPosition.X - StartPosition.X) * t,
            StartPosition.Y + (EndPosition.Y - StartPosition.Y) * t
        );
    }

    /// <summary>
    /// Retourne l'opacité actuelle (fade out à la fin).
    /// </summary>
    public byte GetCurrentAlpha()
    {
        float remainingRatio = TimeRemaining / Duration;
        // Fade out pendant les 30% derniers de l'animation
        if (remainingRatio < 0.3f)
        {
            return (byte)(remainingRatio / 0.3f * 255);
        }
        return 255;
    }
}

/// <summary>
/// Gère l'ensemble des particules de récolte.
/// </summary>
public class HarvestParticleSystem
{
    private readonly List<HarvestParticle> _particles = new();

    public IReadOnlyList<HarvestParticle> Particles => _particles.AsReadOnly();

    /// <summary>
    /// Ajoute une nouvelle particule de récolte.
    /// </summary>
    public void EmitParticle(SKPoint startPos, SKPoint endPos, Resource resource, float duration = 0.8f)
    {
        _particles.Add(new HarvestParticle(startPos, endPos, resource, duration));
    }

    /// <summary>
    /// Met à jour toutes les particules et supprime les mortes.
    /// </summary>
    public void Update(float deltaTime)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            _particles[i].Update(deltaTime);
            if (!_particles[i].IsAlive)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Efface toutes les particules.
    /// </summary>
    public void Clear()
    {
        _particles.Clear();
    }
}
