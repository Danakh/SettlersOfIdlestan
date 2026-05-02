using SkiaSharp;

namespace SettlersOfIdlestanSkia.Services;

/// <summary>
/// Gestionnaire centralisé des ressources (polices, images, caches).
/// Évite les allocations répétées et gère le cycle de vie des ressources.
/// </summary>
public class ResourceManager : IDisposable
{
    private readonly Dictionary<string, SKTypeface> _typefaces = [];
    private readonly Dictionary<string, SKImage> _images = [];
    private readonly Dictionary<string, SKPaint> _paints = [];
    private bool _disposed;

    /// <summary>
    /// Récupère ou crée une police par son nom.
    /// </summary>
    public SKTypeface GetOrCreateTypeface(string fontName)
    {
        return GetOrCreateTypeface(fontName, SKFontStyle.Normal);
    }

    /// <summary>
    /// Récupère ou crée une police par son nom et son style.
    /// </summary>
    public SKTypeface GetOrCreateTypeface(string fontName, SKFontStyle fontStyle)
    {
        var key = $"{fontName}_{fontStyle}";
        
        if (_typefaces.TryGetValue(key, out var typeface))
            return typeface;

        typeface = SKTypeface.FromFamilyName(fontName, fontStyle);
        _typefaces[key] = typeface;
        return typeface;
    }

    /// <summary>
    /// Récupère ou crée une SKPaint avec les paramètres spécifiés.
    /// </summary>
    public SKPaint GetOrCreatePaint(string key, SKColor color, SKStrokeCap strokeCap = SKStrokeCap.Round)
    {
        if (_paints.TryGetValue(key, out var paint))
            return paint;

        paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            StrokeCap = strokeCap
        };

        _paints[key] = paint;
        return paint;
    }

    /// <summary>
    /// Charge une image depuis un chemin.
    /// </summary>
    public SKImage? LoadImage(string imagePath)
    {
        if (_images.TryGetValue(imagePath, out var image))
            return image;

        if (!File.Exists(imagePath))
            return null;

        var bitmap = SKBitmap.Decode(imagePath);
        if (bitmap == null)
            return null;

        image = SKImage.FromBitmap(bitmap);
        bitmap.Dispose();

        _images[imagePath] = image;
        return image;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (var typeface in _typefaces.Values)
            typeface?.Dispose();

        foreach (var image in _images.Values)
            image?.Dispose();

        foreach (var paint in _paints.Values)
            paint?.Dispose();

        _typefaces.Clear();
        _images.Clear();
        _paints.Clear();
        _disposed = true;
    }
}
