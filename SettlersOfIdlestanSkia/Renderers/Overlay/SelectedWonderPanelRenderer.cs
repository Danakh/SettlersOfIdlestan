using SettlersOfIdlestan.Controller.Island;
using SettlersOfIdlestan.Model.IslandMap;
using SettlersOfIdlestan.Services.Localization;
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
    private readonly ILocalizationService _localization;
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

    private SKRect _panelBounds = SKRect.Empty;
    private SKRect _closeRect = SKRect.Empty;
    private readonly Dictionary<SKRect, Resource> _checkboxRects = new();

    public bool IsInputEnabled { get; set; } = true;
    public bool ContainsPoint(SKPoint point) => !_panelBounds.IsEmpty && _panelBounds.Contains(point.X, point.Y);

    public SelectedWonderPanelRenderer(
        WonderService wonderService,
        InputHandlingService inputService,
        ILocalizationService localization,
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

        foreach (Resource resource in Enum.GetValues(typeof(Resource)))
        {
            string name = resource.ToString().ToLower();
            try { _resourceIcons[resource] = _resourceManager.LoadImage($"Resources.icons.resources.{name}.svg"); }
            catch { _resourceIcons[resource] = null; }
        }
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        var wonder = _wonderService.SelectedWonder;
        if (wonder == null)
        {
            _panelBounds = SKRect.Empty;
            _checkboxRects.Clear();
            return;
        }

        _checkboxRects.Clear();

        var cost = WonderController.GetLevelCost(wonder.Level + 1);
        int resourceCount = cost.Count;

        float panelX = _canvasSize.Width - PanelWidth - 10;
        float panelY = 60;
        float panelHeight = TitleHeight + resourceCount * RowHeight + Padding;

        _panelBounds = new SKRect(panelX, panelY, panelX + PanelWidth, panelY + panelHeight);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _bgPaint);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, _borderPaint);

        // Titre + bouton fermer
        string title = _localization.Get("wonder_panel_title") + " " + (wonder.Level + 1);
        canvas.DrawText(title, panelX + Padding, panelY + TitleHeight - 8, _font15, _textPaint);

        const float closeSize = 20;
        float closeX = panelX + PanelWidth - Padding - closeSize;
        float closeY = panelY + (TitleHeight - closeSize) / 2;
        _closeRect = new SKRect(closeX, closeY, closeX + closeSize, closeY + closeSize);
        canvas.DrawRoundRect(_closeRect, 4, 4, _closePaint);
        canvas.DrawText("✕", _closeRect.MidX, _closeRect.MidY + 5, SKTextAlign.Center, _font12, _textPaint);

        float y = panelY + TitleHeight;

        foreach (var kvp in cost)
        {
            Resource resource = kvp.Key;
            long required = kvp.Value;
            long invested = wonder.InvestedResources.TryGetValue(resource, out var inv) ? inv : 0;
            bool enabled = wonder.InvestmentEnabled.Contains(resource);
            bool done = invested >= required;

            float rowCenterY = y + RowHeight / 2;

            // Checkbox
            const float cbSize = 14f;
            float cbX = panelX + Padding;
            float cbY = rowCenterY - RowHeight / 4 - cbSize / 2;
            var cbRect = new SKRect(cbX, cbY, cbX + cbSize, cbY + cbSize);
            canvas.DrawRoundRect(cbRect, 3, 3, done ? _checkboxActivePaint : (enabled ? _checkboxActivePaint : _checkboxInactivePaint));
            canvas.DrawRoundRect(cbRect, 3, 3, _checkboxBorderPaint);
            if (enabled || done)
            {
                using var checkPaint = new SKPaint { Color = SKColors.White, StrokeWidth = 2f, Style = SKPaintStyle.Stroke, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                canvas.DrawLine(cbX + 2.5f, cbY + cbSize / 2f, cbX + cbSize / 2f - 1f, cbY + cbSize - 3f, checkPaint);
                canvas.DrawLine(cbX + cbSize / 2f - 1f, cbY + cbSize - 3f, cbX + cbSize - 2f, cbY + 3f, checkPaint);
            }
            if (!done)
                _checkboxRects[new SKRect(cbX - 3, cbY - 3, cbX + cbSize + 3, cbY + cbSize + 3)] = resource;

            // Icône ressource
            const float iconSize = 16f;
            float iconX = panelX + Padding + cbSize + 4;
            float iconY = rowCenterY - RowHeight / 4 - iconSize / 2;
            if (_resourceIcons.TryGetValue(resource, out var svg) && svg?.Picture != null)
            {
                float scale = iconSize / 32f;
                canvas.Save();
                canvas.Translate(iconX, iconY);
                canvas.Scale(scale);
                canvas.DrawPicture(svg.Picture);
                canvas.Restore();
            }

            // Nom de la ressource
            float textX = iconX + iconSize + 4;
            string resName = _localization.Get("resource_" + resource.ToString().ToLower());
            canvas.DrawText(resName, textX, rowCenterY - RowHeight / 4 + 5, _font12, done ? _dimTextPaint : _textPaint);

            // Montant investie / requis (à droite)
            string amountText = $"{invested}/{required}";
            canvas.DrawText(amountText, panelX + PanelWidth - Padding, rowCenterY - RowHeight / 4 + 5, SKTextAlign.Right, _font10, done ? _barFillPaint : _dimTextPaint);

            // Barre de progression (en bas de chaque rangée)
            float barX = panelX + Padding;
            float barY = y + RowHeight - BarHeight - 8;
            float barWidth = PanelWidth - 2 * Padding;
            float fillWidth = done ? barWidth : (required > 0 ? Math.Min(barWidth, (float)((double)invested / required * barWidth)) : 0);

            canvas.DrawRoundRect(barX, barY, barWidth, BarHeight, 3, 3, _barBgPaint);
            if (fillWidth > 0)
                canvas.DrawRoundRect(barX, barY, fillWidth, BarHeight, 3, 3, _barFillPaint);

            y += RowHeight;
        }
    }

    private void HandlePointerPressed(object? sender, PointerEventArgs e)
    {
        if (!IsInputEnabled || e.Button != PointerButton.Left) return;

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
    }
}
