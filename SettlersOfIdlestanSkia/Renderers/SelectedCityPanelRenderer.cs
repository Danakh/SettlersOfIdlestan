using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Services.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using SettlersOfIdlestan.Model.City;

namespace SettlersOfIdlestanSkia.Renderers;

/// <summary>
/// Affiche le panneau latéral des bâtiments de la ville sélectionnée, avec traduction et tooltips.
/// </summary>
public class SelectedCityPanelRenderer : IGameRenderer
{
    private readonly ILocalizationService _localization;
    private SKSize _canvasSize;
    private bool _disposed;
    private int _hoveredIndex = -1;

    public City? SelectedCity { get; set; }

    public SelectedCityPanelRenderer(ILocalizationService localization)
    {
        _localization = localization;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        float panelWidth = 260;
        float panelX = _canvasSize.Width - panelWidth - 10;
        float panelY = 60;
        float rowHeight = 36;
        float padding = 10;
        float y = panelY + padding;

        using var bgPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, TextSize = 15, IsAntialias = true };
        using var buttonPaint = new SKPaint { Color = new SKColor(21, 101, 192, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var buttonTextPaint = new SKPaint { Color = SKColors.White, TextSize = 13, IsAntialias = true };

        int buildingCount = SelectedCity?.Buildings.Count ?? 0;

        // Fond du panneau
        canvas.DrawRoundRect(panelX, panelY, panelWidth, buildingCount * rowHeight + 2 * padding, 12, 12, bgPaint);
        canvas.DrawRoundRect(panelX, panelY, panelWidth, buildingCount * rowHeight + 2 * padding, 12, 12, borderPaint);

        for (int i = 0; i < buildingCount; i++)
        {
            var building = SelectedCity!.Buildings[i];
            bool isBuilt = (building.Level > 0);
            var yRow = y + i * rowHeight;
            var nameKey = building.NameKey;
            var descKey = building.DescriptionKey;
            var name = _localization.Get((LocalizationKey)Enum.Parse(typeof(LocalizationKey), nameKey, true));
            var desc = _localization.Get((LocalizationKey)Enum.Parse(typeof(LocalizationKey), descKey, true));
            var suffix = isBuilt ? $" (Niv {building.Level})" : string.Empty;
            var label = $"{name}{suffix}";

            // Nom du bâtiment
            canvas.DrawText(label, panelX + padding, yRow + 22, textPaint);

            // Bouton action
            var btnText = isBuilt ? _localization.Get(LocalizationKey.ActionActivate) : _localization.Get(LocalizationKey.ActionBuild);
            var btnWidth = 90;
            var btnHeight = 26;
            var btnX = panelX + panelWidth - btnWidth - padding;
            var btnY = yRow + 6;
            using var btnPaint = new SKPaint { Color = isBuilt ? new SKColor(46, 125, 50, 255) : new SKColor(21, 101, 192, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
            canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, btnPaint);
            canvas.DrawText(btnText, btnX + 12, btnY + 18, buttonTextPaint);

            // Tooltip si survolé
            if (i == _hoveredIndex)
            {
                float tooltipWidth = Math.Max(180, textPaint.MeasureText(desc) + 20);
                float tooltipX = btnX - tooltipWidth - 10;
                float tooltipY = btnY - 8;
                using var tooltipBg = new SKPaint { Color = new SKColor(50, 50, 70, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
                using var tooltipBorder = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
                canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, 38, 7, 7, tooltipBg);
                canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, 38, 7, 7, tooltipBorder);
                canvas.DrawText(desc, tooltipX + 10, tooltipY + 24, textPaint);
            }
        }
    }

    public void Dispose() => _disposed = true;
}
