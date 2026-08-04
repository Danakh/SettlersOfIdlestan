using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SettlersOfIdlestanUI;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Les icones de l'overlay sont des SVG embarques, partages avec le rendu Skia. Si la
/// convention de nom ou l'embarquement casse, l'UI perd silencieusement ses icones :
/// ces tests transforment cette panne muette en echec franc.
/// </summary>
public class SvgIconCacheTests
{
    [AvaloniaFact]
    public void Une_icone_de_ressource_est_rasterisee_avec_des_pixels_visibles()
    {
        using var cache = new SvgIconCache();

        var bitmap = cache.GetResourceIcon("Wood", 32);

        Assert.NotNull(bitmap);
        Assert.Equal(new PixelSize(32, 32), bitmap!.PixelSize);
        Assert.True(HasVisiblePixels(bitmap), "L'icone est entierement transparente : rien n'a ete dessine.");
    }

    [AvaloniaTheory]
    [InlineData("Wood")]
    [InlineData("Brick")]
    [InlineData("Stone")]
    [InlineData("Gold")]
    [InlineData("Food")]
    [InlineData("Ore")]
    [InlineData("Steel")]
    [InlineData("Glass")]
    [InlineData("Crystal")]
    [InlineData("Mithril")]
    public void Toutes_les_icones_de_ressources_existent(string resourceName)
    {
        using var cache = new SvgIconCache();
        Assert.NotNull(cache.GetResourceIcon(resourceName, 22));
    }

    [AvaloniaFact]
    public void Le_cache_renvoie_la_meme_instance_pour_un_meme_couple_nom_taille()
    {
        using var cache = new SvgIconCache();

        var first = cache.GetResourceIcon("Wood", 22);
        var second = cache.GetResourceIcon("Wood", 22);
        var other = cache.GetResourceIcon("Wood", 32);

        Assert.Same(first, second);
        Assert.NotSame(first, other);
    }

    [AvaloniaFact]
    public void Une_icone_inconnue_renvoie_null_plutot_que_de_lever()
    {
        using var cache = new SvgIconCache();
        Assert.Null(cache.Get("Resources.icons.resources.inexistante.svg", 22));
    }

    private static bool HasVisiblePixels(Bitmap bitmap)
    {
        int w = bitmap.PixelSize.Width, h = bitmap.PixelSize.Height;
        int stride = w * 4;
        var buffer = new byte[stride * h];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally { handle.Free(); }

        // Canal alpha (BGRA) : au moins un pixel non transparent.
        for (int i = 3; i < buffer.Length; i += 4)
            if (buffer[i] != 0) return true;
        return false;
    }
}
