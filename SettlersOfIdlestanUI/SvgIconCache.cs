using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;

namespace SettlersOfIdlestanUI;

/// <summary>
/// Rend les icones SVG embarquees du jeu en bitmaps Avalonia.
///
/// Les icones sont partagees avec la couche Skia (memes ressources, meme convention de nom
/// "Resources.icons.<famille>.<nom>.svg"), mais Avalonia ne sait pas afficher un SKSvg. On
/// rasterise donc a la taille demandee, une fois, dans le buffer d'un WriteableBitmap — sans
/// passer par un encodage PNG intermediaire.
///
/// Le cache est indexe par (nom, taille) : la meme icone affichee a deux tailles produit deux
/// bitmaps, ce qui evite tout reechantillonnage a l'affichage.
///
/// Non thread-safe : a n'utiliser que depuis le thread UI.
/// </summary>
public sealed class SvgIconCache : IDisposable
{
    private readonly ResourceManager _resources = new();
    private readonly Dictionary<(string Name, int Size, uint Tint), Bitmap?> _cache = new();
    private bool _disposed;

    /// <summary>
    /// Rasterise une icone a <paramref name="size"/> pixels de cote.
    /// Renvoie null si la ressource n'existe pas, pour que l'appelant degrade sans planter.
    /// </summary>
    /// <param name="tint">Remplace toutes les couleurs de l'icone par celle-ci, en preservant
    /// l'alpha. Les icones monochromes du jeu (militaire) sont dessinees ainsi, faute de quoi
    /// leur couleur d'origine se perdrait sur le fond colore d'un bouton.</param>
    public Bitmap? Get(string resourceName, int size, SKColor? tint = null)
    {
        if (_disposed || size <= 0) return null;

        var key = (resourceName, size, tint.HasValue ? (uint)tint.Value : 0u);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var bitmap = Rasterize(resourceName, size, tint);
        _cache[key] = bitmap;
        return bitmap;
    }

    /// <summary>Icone de ressource de jeu (bois, brique...), par nom d'enum.</summary>
    public Bitmap? GetResourceIcon(string resourceEnumName, int size) =>
        Get($"Resources.icons.resources.{resourceEnumName.ToLowerInvariant()}.svg", size);

    private Bitmap? Rasterize(string resourceName, int size, SKColor? tint)
    {
        var svg = _resources.LoadImage(resourceName);
        var picture = svg?.Picture;
        if (picture == null) return null;

        var cull = picture.CullRect;
        if (cull.Width <= 0 || cull.Height <= 0) return null;

        var bitmap = new WriteableBitmap(
            new PixelSize(size, size),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, buffer.Address, buffer.RowBytes);
            if (surface == null) return null;

            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            // Homothetie centree : les icones ne sont pas toutes carrees, on preserve le ratio
            // plutot que d'etirer.
            float scale = Math.Min(size / cull.Width, size / cull.Height);
            canvas.Translate(
                (size - cull.Width * scale) / 2f,
                (size - cull.Height * scale) / 2f);
            canvas.Scale(scale);
            canvas.Translate(-cull.Left, -cull.Top);

            if (tint == null)
            {
                canvas.DrawPicture(picture);
            }
            else
            {
                // SrcIn sur un calque : la couleur remplace celle de l'icone la ou elle est
                // opaque, et laisse le reste transparent. Meme rendu que la couche Skia.
                using var paint = new SKPaint
                {
                    IsAntialias = true,
                    ColorFilter = SKColorFilter.CreateBlendMode(tint.Value, SKBlendMode.SrcIn),
                };
                canvas.SaveLayer(paint);
                canvas.DrawPicture(picture);
                canvas.Restore();
            }

            canvas.Flush();
        }

        return bitmap;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var bitmap in _cache.Values) bitmap?.Dispose();
        _cache.Clear();
        _resources.Dispose();
    }
}
