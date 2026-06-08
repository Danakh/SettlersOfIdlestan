using SkiaSharp;
using Svg.Skia;
using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.HexGrid;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;

namespace SettlersOfIdlestanSkia.Renderers.Island;

public class HarvestRenderer : IGameRenderer
{
    private readonly HarvestParticleSystem _particleSystem;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _icons = new();
    private SKPaint? _layerPaint;
    private bool _disposed;
    private Func<bool>? _showParticles;
    private readonly Func<int> _currentLayer;

    private const float IconSize = 16f;
    private const float SvgViewBox = 32f;

    public HarvestRenderer(HarvestParticleSystem particleSystem, ResourceManager resourceManager, Func<int> currentLayer)
    {
        _particleSystem = particleSystem ?? throw new ArgumentNullException(nameof(particleSystem));
        _resourceManager = resourceManager ?? throw new ArgumentNullException(nameof(resourceManager));
        _currentLayer = currentLayer ?? throw new ArgumentNullException(nameof(currentLayer));
    }

    public void Connect(
        HarvestService harvestService,
        GameControllerService gameControllerService,
        Func<HexCoord, SKPoint> hexToIsland,
        Func<Vertex, SKPoint> vertexToIsland,
        Func<bool> isPrestigeTransitionPending,
        Func<bool> isIslandTabActive,
        Func<bool>? showParticles = null)
    {
        _showParticles = showParticles;

        harvestService.OnHarvestCompleted += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            if (_showParticles?.Invoke() == false) return;
            if (gameControllerService.PlayerCivilizationIndex != args.CivilizationIndex) return;
            if (args.HexCoord.Z != _currentLayer()) return;

            var hexCenter = hexToIsland(args.HexCoord);
            var cityCenter = vertexToIsland(args.CityPosition);
            _particleSystem.EmitParticles(hexCenter, cityCenter, args.Resources);
        };

        harvestService.OnRandomResourceGenerated += (_, args) =>
        {
            if (isPrestigeTransitionPending()) return;
            if (!isIslandTabActive()) return;
            if (_showParticles?.Invoke() == false) return;
            if (gameControllerService.PlayerCivilizationIndex != args.CivilizationIndex) return;
            if (args.CityPosition.Z != _currentLayer()) return;

            var cityCenter = vertexToIsland(args.CityPosition);
            var above = new SKPoint(cityCenter.X, cityCenter.Y - 20f);
            _particleSystem.EmitParticle(cityCenter, above, args.Resource, 0.5f);
        };
    }

    public void Initialize(SKSize canvasSize)
    {
        _layerPaint = new SKPaint { IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            _icons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg");
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
                canvas.Save();
                canvas.Translate(position.X - IconSize / 2f, position.Y - IconSize / 2f);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);
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
