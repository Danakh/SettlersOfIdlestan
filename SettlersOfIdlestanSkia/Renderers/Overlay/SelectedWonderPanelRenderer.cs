using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestanSkia.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;

namespace SettlersOfIdlestanSkia.Renderers.Overlay;

public class SelectedWonderPanelRenderer : IGameRenderer
{
    private readonly WonderService _wonderService;
    private readonly InputHandlingService _inputService;
    private readonly LocalizationService _localization;
    private readonly ResourceManager _resourceManager;
    private readonly Dictionary<Resource, SKSvg?> _resourceIcons = new();

    private SKSize _canvasSize;
    private SKFont? _font15;
    private SKFont? _font12;
    private SKFont? _font10;
    private SKPaint? _bgPaint;
    private SKPaint? _borderPaint;
    private SKPaint? _textPaint;
    private SKPaint? _dimTextPaint;
    private SKPaint? _checkboxActivePaint;
    private SKPaint? _checkboxInactivePaint;
    private SKPaint? _checkboxBorderPaint;
    private SKPaint? _barBgPaint;
    private SKPaint? _barFillPaint;
    private SKPaint? _closePaint;

    private const float PanelWidth = 280;
    private const float RowHeight = 50;
    private const float TitleHeight = 32;
    private const float Padding = 10;
    private const float BarHeight = 10;

    public float TopOverride { get; set; } = 0f;
    public bool HasSelection => _wonderService.SelectedWonder != null;
    private SKRect _panelBounds = SKRect.Empty;
    private SKRect _closeRect = SKRect.Empty;
    private SKRect _collapseTabRect = SKRect.Empty;
    private readonly Dictionary<SKRect, Resource> _checkboxRects = new();
    private SKPaint? _collapseTabPaint;
    private SKPaint? _scrollTrackPaint;
    private SKPaint? _scrollThumbPaint;
    private bool _collapsed = false;
    private int _scrollOffset = 0;
    private int _lastResourceCount = 0;
    private int _lastVisibleCount = 0;
    private const float CollapseTabW = 14f;
    private const float CollapseTabH = 24f;
    private float _lastUiScale = 0f;

    public bool IsInputEnabled { get; set; } = true;
    public bool ContainsPoint(SKPoint point) =>
        (!_panelBounds.IsEmpty && _panelBounds.Contains(point.X, point.Y)) ||
        (!_collapseTabRect.IsEmpty && _collapseTabRect.Contains(point.X, point.Y));

    public SelectedWonderPanelRenderer(
        WonderService wonderService,
        InputHandlingService inputService,
        LocalizationService localization,
        ResourceManager resourceManager)
    {
        _wonderService = wonderService;
        _inputService = inputService;
        _localization = localization;
        _resourceManager = resourceManager;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;

        _font15 = new SKFont { Size = 15, Typeface = SkiaFonts.Bold };
        _font12 = new SKFont { Size = 12, Typeface = SkiaFonts.Regular };
        _font10 = new SKFont { Size = 10, Typeface = SkiaFonts.Regular };
        _bgPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _borderPaint = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _dimTextPaint = new SKPaint { Color = new SKColor(160, 160, 170, 200), IsAntialias = true };
        _checkboxActivePaint = new SKPaint { Color = new SKColor(46, 160, 67, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxInactivePaint = new SKPaint { Color = new SKColor(40, 40, 50, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _checkboxBorderPaint = new SKPaint { Color = new SKColor(160, 160, 180, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        _barBgPaint = new SKPaint { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _barFillPaint = new SKPaint { Color = new SKColor(180, 140, 30, 230), Style = SKPaintStyle.Fill, IsAntialias = true };
        _closePaint = new SKPaint { Color = new SKColor(200, 80, 80, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _collapseTabPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        _scrollTrackPaint = new SKPaint { Color = new SKColor(50, 50, 65, 200), Style = SKPaintStyle.Fill, IsAntialias = true };
        _scrollThumbPaint = new SKPaint { Color = new SKColor(130, 130, 165, 210), Style = SKPaintStyle.Fill, IsAntialias = true };

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            try { _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg"); }
            catch { _resourceIcons[resource] = null; }
        }
    }

    public void HandleScroll(float delta)
    {
        if (_collapsed) return;
        int dir = delta > 0 ? -1 : 1;
        _scrollOffset = Math.Clamp(_scrollOffset + dir, 0, Math.Max(0, _lastResourceCount - _lastVisibleCount));
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        var wonder = _wonderService.SelectedWonder;
        if (wonder == null)
        {
            _panelBounds = SKRect.Empty;
            _collapseTabRect = SKRect.Empty;
            _checkboxRects.Clear();
            return;
        }

        _checkboxRects.Clear();

        if (context.UiScale != _lastUiScale)
        {
            _lastUiScale = context.UiScale;
            _font10?.Dispose(); _font10 = new SKFont { Size = 10 * _lastUiScale, Typeface = SkiaFonts.Regular };
            _font12?.Dispose(); _font12 = new SKFont { Size = 12 * _lastUiScale, Typeface = SkiaFonts.Regular };
            _font15?.Dispose(); _font15 = new SKFont { Size = 15 * _lastUiScale, Typeface = SkiaFonts.Bold };
        }

        float s = _lastUiScale;
        float panelWidth   = PanelWidth * s;
        float rowHeight    = RowHeight * s;
        float titleHeight  = TitleHeight * s;
        float padding      = Padding * s;
        float barH         = BarHeight * s;
        float collapseTabW = CollapseTabW * s;
        float collapseTabH = CollapseTabH * s;

        var cost = WonderController.GetLevelCost(wonder.Level + 1);
        int resourceCount = cost.Count;
        var costList = cost.ToList();

        float panelX = _canvasSize.Width - panelWidth - 10 * s;
        float panelY = TopOverride > 0f ? TopOverride : PlayerResourcesOverlayRenderer.BarHeight * s + 10 * s;
        float tabTop = panelY + 8f * s;

        if (_collapsed)
        {
            _collapseTabRect = new SKRect(_canvasSize.Width - collapseTabW, tabTop, _canvasSize.Width, tabTop + collapseTabH);
            _panelBounds = _collapseTabRect;
            canvas.DrawRoundRect(_collapseTabRect, 4 * s, 4 * s, _collapseTabPaint);
            canvas.DrawRoundRect(_collapseTabRect, 4 * s, 4 * s, _borderPaint);
            SkiaTextUtils.DrawText(canvas, "◄", _collapseTabRect.MidX, _collapseTabRect.MidY + 5f * s, SKTextAlign.Center, _font12, _textPaint);
            return;
        }

        float maxPanelHeight = Math.Max(0, _canvasSize.Height - panelY - 20 * s);
        int visibleResourceCount = Math.Min(resourceCount, Math.Max(0, (int)((maxPanelHeight - titleHeight - padding) / rowHeight)));
        _lastResourceCount = resourceCount;
        _lastVisibleCount = visibleResourceCount;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, resourceCount - visibleResourceCount));
        bool needsScrollbar = resourceCount > visibleResourceCount;

        float panelHeight = titleHeight + visibleResourceCount * rowHeight + padding;
        _panelBounds = new SKRect(panelX, panelY, panelX + panelWidth, panelY + panelHeight);
        canvas.DrawRoundRect(panelX, panelY, panelWidth, panelHeight, 12 * s, 12 * s, _bgPaint);
        canvas.DrawRoundRect(panelX, panelY, panelWidth, panelHeight, 12 * s, 12 * s, _borderPaint);

        // Titre + bouton fermer
        string title = _localization.Get("wonder_panel_title") + " " + (wonder.Level + 1);
        SkiaTextUtils.DrawText(canvas, title, panelX + padding, panelY + titleHeight - 8 * s, _font15, _textPaint);

        float closeSize = 20 * s;
        float closeX = panelX + panelWidth - padding - closeSize;
        float closeY = panelY + (titleHeight - closeSize) / 2;
        _closeRect = new SKRect(closeX, closeY, closeX + closeSize, closeY + closeSize);
        canvas.DrawRoundRect(_closeRect, 4 * s, 4 * s, _closePaint);
        SkiaTextUtils.DrawText(canvas, "✕", _closeRect.MidX, _closeRect.MidY + 5 * s, SKTextAlign.Center, _font12, _textPaint);

        float y = panelY + titleHeight;

        foreach (var kvp in costList.Skip(_scrollOffset).Take(visibleResourceCount))
        {
            Resource resource = kvp.Key;
            long required = kvp.Value;
            long invested = wonder.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
            bool enabled = wonder.InvestmentEnabled.Contains(resource);
            bool done = invested >= required;

            float rowCenterY = y + rowHeight / 2;

            // Checkbox
            float cbSize = 14f * s;
            float cbX = panelX + padding;
            float cbY = rowCenterY - rowHeight / 4 - cbSize / 2;
            var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
            canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, done ? _checkboxActivePaint : (enabled ? _checkboxActivePaint : _checkboxInactivePaint));
            canvas.DrawRoundRect(cbRect, 3 * s, 3 * s, _checkboxBorderPaint);
            if (enabled || done)
            {
                using var checkPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2f * s, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                canvas.DrawLine(cbX + 2.5f * s, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, checkPaint);
                canvas.DrawLine(cbX + cbSize / 2f - 1f * s, cbY + cbSize - 3f * s, cbX + cbSize - 2f * s, cbY + 3f * s, checkPaint);
            }
            if (!done)
                _checkboxRects[new SKRect(cbX - 3 * s, cbY - 3 * s, cbX + cbSize + 3 * s, cbY + cbSize + 3 * s)] = resource;

            // Icône ressource
            float iconSize = 16f * s;
            float iconX = panelX + padding + cbSize + 4 * s;
            float iconY = rowCenterY - rowHeight / 4 - iconSize / 2;
            if (_resourceIcons.TryGetValue(resource, out var svg) && svg?.Picture != null)
            {
                float svgScale = iconSize / 32f;
                canvas.Save();
                canvas.Translate(iconX, iconY);
                canvas.Scale(svgScale);
                canvas.DrawPicture(svg.Picture);
                canvas.Restore();
            }

            // Nom de la ressource
            float textX = iconX + iconSize + 4 * s;
            string resName = _localization.Get("resource_" + resource.ToString().ToLower());
            SkiaTextUtils.DrawText(canvas, resName, textX, rowCenterY - rowHeight / 4 + 5 * s, _font12, done ? _dimTextPaint : _textPaint);

            // Montant investie / requis (à droite)
            string amountText = $"{invested}/{required}";
            SkiaTextUtils.DrawText(canvas, amountText, panelX + panelWidth - padding, rowCenterY - rowHeight / 4 + 5 * s, SKTextAlign.Right, _font10, done ? _barFillPaint : _dimTextPaint);

            // Barre de progression (en bas de chaque rangée)
            float barX = panelX + padding;
            float barY = y + rowHeight - barH - 8 * s;
            float barWidth = panelWidth - 2 * padding;
            float fillWidth = done ? barWidth : (required > 0 ? Math.Min(barWidth, (float)((double)invested / required * barWidth)) : 0);

            canvas.DrawRoundRect(barX, barY, barWidth, barH, 3 * s, 3 * s, _barBgPaint);
            if (fillWidth > 0)
                canvas.DrawRoundRect(barX, barY, fillWidth, barH, 3 * s, 3 * s, _barFillPaint);

            y += rowHeight;
        }

        // Scrollbar
        if (needsScrollbar)
        {
            float scrollW = 5f * s;
            float trackX = panelX + panelWidth - scrollW - 2f * s;
            float trackTop = panelY + titleHeight;
            float trackH = visibleResourceCount * rowHeight;
            canvas.DrawRoundRect(trackX, trackTop, scrollW, trackH, 3 * s, 3 * s, _scrollTrackPaint);
            float thumbH = Math.Max(16f * s, (float)visibleResourceCount / resourceCount * trackH);
            float maxScroll = Math.Max(1, resourceCount - visibleResourceCount);
            float thumbTop = trackTop + (float)_scrollOffset / maxScroll * (trackH - thumbH);
            canvas.DrawRoundRect(trackX, thumbTop, scrollW, thumbH, 3 * s, 3 * s, _scrollThumbPaint);
        }

        // Onglet collapse
        _collapseTabRect = new SKRect(panelX - collapseTabW, tabTop, panelX, tabTop + collapseTabH);
        canvas.DrawRoundRect(_collapseTabRect, 4 * s, 4 * s, _collapseTabPaint);
        canvas.DrawRoundRect(_collapseTabRect, 4 * s, 4 * s, _borderPaint);
        SkiaTextUtils.DrawText(canvas, "►", _collapseTabRect.MidX, _collapseTabRect.MidY + 5f * s, SKTextAlign.Center, _font12, _textPaint);
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (e.Button != PointerButton.Left) return;

        if (!_collapseTabRect.IsEmpty && _collapseTabRect.Contains(e.Position.X, e.Position.Y))
        {
            _collapsed = !_collapsed;
            return;
        }

        if (!IsInputEnabled) return;

        if (!_closeRect.IsEmpty && _closeRect.Contains(e.Position.X, e.Position.Y))
        {
            _wonderService.ClearSelectedWonder();
            return;
        }

        foreach (var (rect, resource) in _checkboxRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _wonderService.ToggleInvestment(resource);
                return;
            }
        }
    }

    public void Close()
    {
        _wonderService.ClearSelectedWonder();
        _collapsed = false;
        _scrollOffset = 0;
        _panelBounds = SKRect.Empty;
        _closeRect = SKRect.Empty;
        _collapseTabRect = SKRect.Empty;
        _checkboxRects.Clear();
    }

    public void Dispose()
    {
        _inputService.PointerPressed -= HandlePointerPressed;
        _font15?.Dispose();
        _font12?.Dispose();
        _font10?.Dispose();
        _bgPaint?.Dispose();
        _borderPaint?.Dispose();
        _textPaint?.Dispose();
        _dimTextPaint?.Dispose();
        _checkboxActivePaint?.Dispose();
        _checkboxInactivePaint?.Dispose();
        _checkboxBorderPaint?.Dispose();
        _barBgPaint?.Dispose();
        _barFillPaint?.Dispose();
        _closePaint?.Dispose();
        _collapseTabPaint?.Dispose();
        _scrollTrackPaint?.Dispose();
        _scrollThumbPaint?.Dispose();
    }
}
