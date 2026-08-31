using SettlersOfIdlestanSkia.Core;
using SkiaSharp;
using System;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public abstract class PanelRendererBase
{
    protected SKFont? Font10;

    // Shared state
    protected SKSize CanvasSize;
    protected float LastUiScale;
    protected bool Collapsed;

    public virtual void Initialize(SKSize canvasSize)
    {
        CanvasSize = canvasSize;
    }

    protected void UpdateScale(float scale)
    {
        if (scale == LastUiScale) return;
        LastUiScale = scale;
        Font10?.Dispose(); Font10 = new SKFont { Size = 10 * scale, Typeface = SkiaFonts.Regular };
    }

    public virtual void Dispose()
    {
        Font10?.Dispose();
    }
}
