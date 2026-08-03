using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Xunit;

namespace SOIUITests;

/// <summary>
/// Garde-fou d'infrastructure : si le lease SkiaSharp devient indisponible sur un backend
/// (mise a jour d'Avalonia, portage WASM/iOS), toute la carte cesse d'etre dessinee.
/// Ce test doit echouer bruyamment dans ce cas plutot que de laisser un ecran noir.
/// </summary>
public class SkiaCanvasControlTests
{
    [AvaloniaFact]
    public void Le_lease_skia_fournit_un_canvas_et_les_pixels_sont_ecrits()
    {
        const int size = 200;

        var map = new ProbeMapControl();
        map.Measure(new Size(size, size));
        map.Arrange(new Rect(0, 0, size, size));

        var rtb = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            map.Render(ctx);
        }

        Assert.Null(map.LeaseFailureReason);
        Assert.True(map.HasRenderedThroughSkia, "Aucun frame n'a ete dessine via SkiaSharp.");

        const int stride = size * 4;
        var buffer = new byte[stride * size];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            rtb.CopyPixels(new PixelRect(0, 0, size, size), handle.AddrOfPinnedObject(), buffer.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        // Preuve forte : on relit un pixel reellement ecrit par le Clear() de SkiaSharp (BGRA).
        int offset = (size / 2 * stride) + (size / 2 * 4);
        Assert.Equal(ProbeMapControl.FillColor.Blue, buffer[offset + 0]);
        Assert.Equal(ProbeMapControl.FillColor.Green, buffer[offset + 1]);
        Assert.Equal(ProbeMapControl.FillColor.Red, buffer[offset + 2]);
    }
}
