using SkiaSharp;
using System.Reflection;
using Svg.Skia;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Gestionnaire centralisé des ressources (polices, images, caches).
/// Évite les allocations répétées et gère le cycle de vie des ressources.
/// </summary>
public class ResourceManager : IDisposable
{
    private static readonly Assembly _assembly = typeof(ResourceManager).Assembly;

    private readonly Dictionary<string, SKSvg> _images = new();
    private bool _disposed;

    /// <summary>
    /// Charge une image depuis un chemin.
    /// </summary>
    public SKSvg? LoadImage(string resourceName)
    {
        if (_images.TryGetValue(resourceName, out var cached))
            return cached;

        // Résolution du nom complet de la ressource
        string fullName = $"{_assembly.GetName().Name}.{resourceName}";

        using var stream = _assembly.GetManifestResourceStream(fullName);

        if (stream == null)
            return null;

        var svg = new SKSvg();
        svg.Load(stream);

        _images[resourceName] = svg;
        return svg;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _images.Clear();
        _disposed = true;
    }
}
