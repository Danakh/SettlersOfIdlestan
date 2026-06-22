using SkiaSharp;
using SettlersOfIdlestanSkia.Core;

namespace SettlersOfIdlestanSkia.Trailer;

/// <summary>
/// Combine plusieurs IGameRenderer (île + overlay UI + tooltips) en un seul renderer, dans l'ordre
/// donné, pour que VideoExportController (qui ne prend qu'un seul IGameRenderer) capture la scène
/// complète comme à l'écran plutôt que la seule île.
/// </summary>
internal sealed class CompositeGameRenderer : IGameRenderer
{
    private readonly IGameRenderer[] _renderers;

    public CompositeGameRenderer(params IGameRenderer[] renderers)
    {
        _renderers = renderers;
    }

    public void Initialize(SKSize canvasSize)
    {
        foreach (var renderer in _renderers)
            renderer.Initialize(canvasSize);
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        foreach (var renderer in _renderers)
            renderer.Render(canvas, context);
    }

    public void Dispose()
    {
        foreach (var renderer in _renderers)
            renderer.Dispose();
    }
}
