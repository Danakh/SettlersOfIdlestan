using SkiaSharp;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SettlersOfIdlestan.Services.Localization;
using System;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers;

public class SelectedCityPanelRenderer : IGameRenderer
{
    private readonly ILocalizationService _localization;
    private readonly CityBuildingService _cityBuildingService;
    private SKSize _canvasSize;
    private bool _disposed;
    private int _hoveredIndex = -1;

    public SelectedCityPanelRenderer(ILocalizationService localization, CityBuildingService cityBuildingService)
    {
        _localization = localization;
        _cityBuildingService = cityBuildingService;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        var selectionInfo = _cityBuildingService.SelectionInfo;
        var buildings = selectionInfo.Buildings;
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

        int buildingCount = buildings.Count;
        if (buildingCount == 0)
            return;

        // Fond du panneau
        canvas.DrawRoundRect(panelX, panelY, panelWidth, buildingCount * rowHeight + 2 * padding, 12, 12, bgPaint);
        canvas.DrawRoundRect(panelX, panelY, panelWidth, buildingCount * rowHeight + 2 * padding, 12, 12, borderPaint);

        for (int i = 0; i < buildingCount; i++)
        {
            var building = buildings[i];
            var isBuilt = building.IsBuilt;
            var canBuild = building.CanBuild;
            var yRow = y + i * rowHeight;
            var label = building.BuildingType + (isBuilt ? $" (Niv {building.Level})" : "");
            canvas.DrawText(label, panelX + padding, yRow + 22, textPaint);

            // Bouton action
            if (canBuild || isBuilt)
            {
                var btnText = isBuilt ? _localization.Get(LocalizationKey.ActionActivate) : _localization.Get(LocalizationKey.ActionBuild);
                var btnWidth = 90;
                var btnHeight = 26;
                var btnX = panelX + panelWidth - btnWidth - padding;
                var btnY = yRow + 6;
                using var btnPaint = new SKPaint { Color = isBuilt ? new SKColor(46, 125, 50, 255) : new SKColor(21, 101, 192, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, btnPaint);
                canvas.DrawText(btnText, btnX + 12, btnY + 18, buttonTextPaint);
                // TODO: Gérer le clic sur le bouton (voir ci-dessous)
            }
        }
    }

    // TODO: Ajouter la gestion du clic sur le bouton (ex: méthode OnPointerPressed à appeler depuis l'extérieur)
    // public void OnPointerPressed(float x, float y) { ... }

    public void Dispose() => _disposed = true;
}
