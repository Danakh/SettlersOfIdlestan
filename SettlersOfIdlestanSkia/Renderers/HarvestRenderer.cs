using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers;

public class HarvestRenderer : IGameRenderer
{
    private readonly HarvestParticleSystem _particleSystem;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _icons = new();
    private SKPaint? _layerPaint;
    private bool _disposed;

    private const float IconSize = 16f;
    private const float SvgViewBox = 32f;

    public HarvestRenderer(HarvestParticleSystem particleSystem, ResourceManager resourceManager)
    {
        _particleSystem = particleSystem ?? throw new ArgumentNullException(nameof(particleSystem));
        _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
    }

    public void Initialize(SKSize canvasSize)
    {
        _layerPaint = new SKPaint { IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            try
            {
                _icons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
            }
            catch
            {
                _icons[resource] = null;
            }
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        if (_layerPaint == null)
            return;

        _particleSystem.Update(context.DeltaTime);

        float scale = IconSize / SvgViewBox;

        foreach (var particle in _particleSystem.Particles)
        {
            var position = particle.GetCurrentPosition();
            var alpha = particle.GetCurrentAlpha();

            _icons.TryGetValue(particle.Resource, out var svg);
            var picture = svg?.Picture;

            if (picture != null)
            {
                _layerPaint.Color = new SKColor(255, 255, 255, alpha);
                canvas.Save();
                canvas.Translate(position.X - IconSize / 2f, position.Y - IconSize / 2f);
                canvas.Scale(scale);
                canvas.SaveLayer(_layerPaint);
                canvas.DrawPicture(picture);
                canvas.Restore();
                canvas.Restore();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _layerPaint?.Dispose();
        _disposed = true;
    }
}
