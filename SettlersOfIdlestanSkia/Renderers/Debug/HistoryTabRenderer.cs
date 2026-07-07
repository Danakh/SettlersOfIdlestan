using System;
using SkiaSharp;
using SettlersOfIdlestan.Controller;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestanSkia.Services.Localization;

namespace SettlersOfIdlestanSkia.Renderers.Debug;

public sealed class HistoryTabRenderer : IDisposable
{
    private const float Padding     = 20f;
    private const float RowHeight   = 20f;
    private const float RowSpacing  = 3f;
    private const float TimeColW    = 72f;

    private readonly CivilizationHistoryController _history;
    private readonly UILayoutService               _uiLayout;
    private readonly LocalizationService           _localization;

    private SKSize _canvasSize;
    private bool   _disposed;

    private readonly SKPaint _bgPaint     = new() { Color = new SKColor(18, 18, 24, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
    private readonly SKPaint _headerPaint = new() { Color = new SKColor(255, 215, 0),     IsAntialias = true };
    private readonly SKPaint _timePaint   = new() { Color = new SKColor(110, 110, 125),   IsAntialias = true };
    private readonly SKPaint _labelPaint  = new() { Color = new SKColor(200, 200, 210),   IsAntialias = true };
    private readonly SKPaint _emptyPaint  = new() { Color = new SKColor(110, 110, 125),   IsAntialias = true };

    private SKFont _headerFont = new() { Size = 17, Typeface = SkiaFonts.Bold };
    private SKFont _rowFont    = new() { Size = 12, Typeface = SkiaFonts.Regular };
    private SKFont _timeFont   = new() { Size = 11, Typeface = SkiaFonts.Regular };
    private float  _lastScale;

    public HistoryTabRenderer(
        CivilizationHistoryController history,
        UILayoutService               uiLayout,
        LocalizationService           localization)
    {
        _history      = history;
        _uiLayout     = uiLayout;
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
        RefreshFonts(_uiLayout.UiScale);
    }

    private void RefreshFonts(float s)
    {
        if (Math.Abs(s - _lastScale) < 0.001f) return;
        _lastScale = s;
        _headerFont.Dispose(); _headerFont = new SKFont { Size = 17 * s, Typeface = SkiaFonts.Bold };
        _rowFont.Dispose();    _rowFont    = new SKFont { Size = 12 * s, Typeface = SkiaFonts.Regular };
        _timeFont.Dispose();   _timeFont   = new SKFont { Size = 11 * s, Typeface = SkiaFonts.Regular };
    }

    public void RenderHistory(SKCanvas canvas, GameRenderContext context)
    {
        if (_disposed) return;

        float s        = context.UiScale;
        float topBarH  = _uiLayout.SecondRowBottom;
        float pad      = Padding * s;

        canvas.DrawRect(new SKRect(0, topBarH, _canvasSize.Width, _canvasSize.Height), _bgPaint);

        RefreshFonts(s);

        float x = pad;
        float y = topBarH + pad;

        SkiaTextUtils.DrawText(canvas, _localization.Get("tab_history"), x, y + 14 * s, _headerFont, _headerPaint);
        y += 28f * s;

        if (_history.Count == 0)
        {
            SkiaTextUtils.DrawText(canvas, _localization.Get("history_empty"), x, y + 14 * s, _rowFont, _emptyPaint);
            return;
        }

        float rowStep   = (RowHeight + RowSpacing) * s;
        float timeColW  = TimeColW * s;
        float maxY      = _canvasSize.Height - pad;

        foreach (var entry in _history.Entries)
        {
            if (y + rowStep > maxY) break;

            string timeStr = FormatTick(entry.Tick);
            SkiaTextUtils.DrawText(canvas, timeStr,      x,              y + 14 * s, SKTextAlign.Left, _timeFont, _timePaint);
            SkiaTextUtils.DrawText(canvas, entry.Label,  x + timeColW,   y + 14 * s, SKTextAlign.Left, _rowFont,  _labelPaint);
            y += rowStep;
        }
    }

    private static string FormatTick(long tick)
    {
        long totalSec = tick / 100;
        long h        = totalSec / 3600;
        long min      = totalSec % 3600 / 60;
        long sec      = totalSec % 60;
        return h > 0
            ? $"{h}h{min:D2}m{sec:D2}s"
            : $"{min:D2}m{sec:D2}s";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _bgPaint.Dispose();
        _headerPaint.Dispose();
        _timePaint.Dispose();
        _labelPaint.Dispose();
        _emptyPaint.Dispose();
        _headerFont.Dispose();
        _rowFont.Dispose();
        _timeFont.Dispose();
        _disposed = true;
    }
}
