using SettlersOfIdlestanSkia.Core;
using SkiaSharp;
using System;

namespace SettlersOfIdlestanSkia.Renderers.Overlay.Panels;

public abstract class PanelRendererBase : IGameRenderer
{
    protected const float CollapseTabW = 22f;
    protected const float CollapseTabH = 32f;

    // Common paints — initialized in Initialize()
    protected SKPaint? BgPaint;
    protected SKPaint? BorderPaint;
    protected SKPaint? TextPaint;
    protected SKPaint? CheckboxActivePaint;
    protected SKPaint? CheckboxInactivePaint;
    protected SKPaint? CheckboxBorderPaint;
    private SKPaint? _collapseTabPaint;
    private SKPaint? _scrollTrackPaint;
    private SKPaint? _scrollThumbPaint;

    // Common fonts
    protected SKFont? Font10;
    protected SKFont? Font12;
    protected SKFont? Font15;
    protected virtual SKTypeface Font15Typeface => SkiaFonts.Regular;

    // Shared state
    protected SKSize CanvasSize;
    protected float LastUiScale;
    protected SKRect PanelBounds = SKRect.Empty;
    protected SKRect CollapseTabRect = SKRect.Empty;
    protected bool Collapsed;

    // Scroll state
    protected int ScrollOffset;
    protected int LastTotalCount;
    protected int LastVisibleCount;

    public float TopOverride { get; set; }
    public bool IsInputEnabled { get; set; } = true;

    public bool ContainsPoint(SKPoint point) =>
        (!PanelBounds.IsEmpty && PanelBounds.Contains(point.X, point.Y)) ||
        (!CollapseTabRect.IsEmpty && CollapseTabRect.Contains(point.X, point.Y));

    public virtual void Initialize(SKSize canvasSize)
    {
        CanvasSize = canvasSize;
        BgPaint            = new SKPaint { Color = new SKColor(30, 30, 40, 220),   Style = SKPaintStyle.Fill,   IsAntialias = true };
        BorderPaint        = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        TextPaint          = new SKPaint { Color = SKColors.White,                  IsAntialias = true };
        CheckboxActivePaint   = new SKPaint { Color = new SKColor(46, 160, 67, 230),  Style = SKPaintStyle.Fill,   IsAntialias = true };
        CheckboxInactivePaint = new SKPaint { Color = new SKColor(40, 40, 50, 200),   Style = SKPaintStyle.Fill,   IsAntialias = true };
        CheckboxBorderPaint   = new SKPaint { Color = new SKColor(160, 160, 180, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        _collapseTabPaint  = new SKPaint { Color = new SKColor(30, 30, 40, 220),   Style = SKPaintStyle.Fill,   IsAntialias = true };
        _scrollTrackPaint  = new SKPaint { Color = new SKColor(50, 50, 65, 200),   Style = SKPaintStyle.Fill,   IsAntialias = true };
        _scrollThumbPaint  = new SKPaint { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill,  IsAntialias = true };
    }

    protected void UpdateScale(float scale)
    {
        if (scale == LastUiScale) return;
        LastUiScale = scale;
        Font10?.Dispose(); Font10 = new SKFont { Size = 10 * scale, Typeface = SkiaFonts.Regular };
        Font12?.Dispose(); Font12 = new SKFont { Size = 12 * scale, Typeface = SkiaFonts.Regular };
        Font15?.Dispose(); Font15 = new SKFont { Size = 15 * scale, Typeface = Font15Typeface };
    }

    // Draws bg rect + border (corner radius 12 by default, unscaled — scale applied inside)
    protected void DrawPanelChrome(SKCanvas canvas, float x, float y, float width, float height, float cornerRadius = 12f)
    {
        float r = cornerRadius * LastUiScale;
        canvas.DrawRoundRect(x, y, width, height, r, r, BgPaint);
        canvas.DrawRoundRect(x, y, width, height, r, r, BorderPaint);
    }

    // Draws the collapse handle rectangle with a triangle arrow
    protected void DrawCollapseTabRect(SKCanvas canvas, SKRect rect, bool pointRight)
    {
        float s = LastUiScale;
        canvas.DrawRoundRect(rect, 4 * s, 4 * s, _collapseTabPaint);
        canvas.DrawRoundRect(rect, 4 * s, 4 * s, BorderPaint);

        float cx = rect.MidX;
        float cy = rect.MidY;
        float tw = rect.Width  * 0.52f;
        float th = rect.Height * 0.58f;

        using var path = new SKPath();
        if (pointRight)
        {
            path.MoveTo(cx - tw * 0.5f, cy - th * 0.5f);
            path.LineTo(cx + tw * 0.5f, cy);
            path.LineTo(cx - tw * 0.5f, cy + th * 0.5f);
        }
        else
        {
            path.MoveTo(cx + tw * 0.5f, cy - th * 0.5f);
            path.LineTo(cx - tw * 0.5f, cy);
            path.LineTo(cx + tw * 0.5f, cy + th * 0.5f);
        }
        path.Close();
        canvas.DrawPath(path, TextPaint);
    }

    // Scrollbar track + thumb; scrollW is always 5f * scale
    protected void DrawScrollbar(SKCanvas canvas, float trackX, float trackTop, float trackH, int totalCount, int visibleCount, int scrollOffset)
    {
        float s = LastUiScale;
        float scrollW = 5f * s;
        canvas.DrawRoundRect(trackX, trackTop, scrollW, trackH, 3 * s, 3 * s, _scrollTrackPaint);
        float thumbH   = Math.Max(16f * s, (float)visibleCount / totalCount * trackH);
        float maxScroll = Math.Max(1, totalCount - visibleCount);
        float thumbTop  = trackTop + (float)scrollOffset / maxScroll * (trackH - thumbH);
        canvas.DrawRoundRect(trackX, thumbTop, scrollW, thumbH, 3 * s, 3 * s, _scrollThumbPaint);
    }

    // Returns true (and toggles Collapsed) when the collapse tab was hit
    protected bool HandleCollapseTabPress(SKPoint pos)
    {
        if (!CollapseTabRect.IsEmpty && CollapseTabRect.Contains(pos.X, pos.Y))
        {
            Collapsed = !Collapsed;
            return true;
        }
        return false;
    }

    public void HandleScroll(float delta)
    {
        if (Collapsed) return;
        int dir = delta > 0 ? -1 : 1;
        ScrollOffset = Math.Clamp(ScrollOffset + dir, 0, Math.Max(0, LastTotalCount - LastVisibleCount));
    }

    public abstract void Render(SKCanvas canvas, GameRenderContext context);

    public virtual void Dispose()
    {
        BgPaint?.Dispose();
        BorderPaint?.Dispose();
        TextPaint?.Dispose();
        CheckboxActivePaint?.Dispose();
        CheckboxInactivePaint?.Dispose();
        CheckboxBorderPaint?.Dispose();
        _collapseTabPaint?.Dispose();
        _scrollTrackPaint?.Dispose();
        _scrollThumbPaint?.Dispose();
        Font10?.Dispose();
        Font12?.Dispose();
        Font15?.Dispose();
    }
}
