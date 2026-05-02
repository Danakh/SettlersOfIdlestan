using SettlersOfIdlestan.Model.Buildings;
using SettlersOfIdlestan.Services.Localization;
using SettlersOfIdlestanSkia.Core;
using SettlersOfIdlestanSkia.Services;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SettlersOfIdlestanSkia.Renderers;

public class SelectedCityPanelRenderer : IGameRenderer
{
    private readonly ILocalizationService _localization;
    private readonly CityBuildingService _cityBuildingService;
    private readonly InputHandlingService _inputService;
    private SKSize _canvasSize;
    private SKPoint _lastPointerPosition = SKPoint.Empty;
    private string? _hoveredBuildingType = null;
    private const float PanelWidth = 260;
    private const float RowHeight = 36;
    private const float Padding = 10;
    private Dictionary<SKRect, string> _hoverRects = new Dictionary<SKRect, string>();

    public SelectedCityPanelRenderer(ILocalizationService localization, CityBuildingService cityBuildingService, InputHandlingService inputService)
    {
        _localization = localization;
        _cityBuildingService = cityBuildingService;
        _inputService = inputService;
        _inputService.PointerMoved += HandlePointerMoved;
        _inputService.PointerPressed += HandlePointerPressed;
    }

    public void Initialize(SKSize canvasSize)
    {
        _canvasSize = canvasSize;
    }

    public void Render(SKCanvas canvas, GameRenderContext context)
    {
        float panelX = _canvasSize.Width - PanelWidth - 10;
        float panelY = 60;
        float y = panelY + Padding;

        var font15 = new SKFont { Size = 15 };
        var font12 = new SKFont { Size = 12 };
        var font10 = new SKFont { Size = 10 };

        using var bgPaint = new SKPaint { Color = new SKColor(30, 30, 40, 220), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var borderPaint = new SKPaint { Color = new SKColor(200, 200, 220, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var costTextPaint = new SKPaint { Color = new SKColor(200, 200, 200, 200), IsAntialias = true };
        using var buttonTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        _hoverRects.Clear();

        var buildings = _cityBuildingService.SelectedCityBuildings().ToList();
        int buildingCount = buildings.Count;
        if (buildingCount == 0)
            return;

        // Fond du panneau
        float panelHeight = buildingCount * RowHeight + 2 * Padding;
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, bgPaint);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, borderPaint);

        foreach (var (building, index) in buildings.Select((item, i) => (item, i)))
        {
            var isBuilt = building.Level > 0;
            var canBuild = building.Level == 0;
            var yRow = y + index * RowHeight;
            var label = _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : "");
            canvas.DrawText(label, panelX + Padding, yRow + 18, font15, textPaint);

            // Afficher le coût des ressources sous le nom
            var cost = isBuilt ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
            if (cost.Count > 0)
            {
                var costText = string.Join(" | ", cost.Select(kvp => $"{_localization.Get($"resource_{kvp.Key.ToString().ToLower()}")}: {kvp.Value}"));
                canvas.DrawText(costText, panelX + Padding, yRow + 30, font10, costTextPaint);
            }

            // Bouton action
            if (canBuild || isBuilt)
            {
                var btnText = isBuilt ? _localization.Get("ActionUpgrade") : _localization.Get("ActionBuild");
                var btnWidth = 90;
                var btnHeight = 26;
                var btnX = panelX + PanelWidth - btnWidth - Padding;
                var btnY = yRow + 6;
                
                using var btnPaint = new SKPaint 
                { 
                    Color = isBuilt ? new SKColor(46, 125, 50, 255) : new SKColor(21, 101, 192, 255), 
                    Style = SKPaintStyle.Fill, 
                    IsAntialias = true 
                };
                canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, btnPaint);
                canvas.DrawText(btnText, btnX + 12, btnY + 18, font12, buttonTextPaint);

                // Stocker les informations du bouton pour la détection de clic
                var btnRect = new SKRect(btnX, btnY, btnX + btnWidth, btnY + btnHeight);
                _hoverRects[btnRect] = building.Type.ToString();
            }
        }

        // Afficher le tooltip (description) si on survole un bâtiment
        if (!string.IsNullOrEmpty(_hoveredBuildingType))
        {
            var hoveredBuilding = buildings.FirstOrDefault(b => b.Type.ToString() == _hoveredBuildingType);
            if (hoveredBuilding != null)
            {
                DrawTooltip(canvas, hoveredBuilding, font10);
            }
        }
    }

    private void DrawTooltip(SKCanvas canvas, Building building, SKFont font)
    {
        var description = _localization.Get(building.DescriptionKey);
        
        using var tooltipBgPaint = new SKPaint { Color = new SKColor(60, 60, 70, 240), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var tooltipBorderPaint = new SKPaint { Color = new SKColor(220, 220, 240, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var tooltipTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        float tooltipWidth = 200;
        float tooltipHeight = 60;
        float tooltipX = _lastPointerPosition.X + 15;
        float tooltipY = _lastPointerPosition.Y + 15;

        // Ajuster la position si le tooltip sort du cadre
        if (tooltipX + tooltipWidth > _canvasSize.Width)
            tooltipX = _lastPointerPosition.X - tooltipWidth - 10;
        if (tooltipY + tooltipHeight > _canvasSize.Height)
            tooltipY = _lastPointerPosition.Y - tooltipHeight - 10;

        canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, tooltipBgPaint);
        canvas.DrawRoundRect(tooltipX, tooltipY, tooltipWidth, tooltipHeight, 8, 8, tooltipBorderPaint);
        
        // Afficher le texte en wrappé
        SkiaTextUtils.DrawWrappedText(canvas, description, tooltipX + 8, tooltipY + 15, tooltipWidth - 16, font, tooltipTextPaint);
    }

    private void HandlePointerMoved(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        _lastPointerPosition = e.Position;
        
        // Détecter si on survole un bâtiment
        _hoveredBuildingType = null;
        foreach (var (rect, buildingType) in _hoverRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _hoveredBuildingType = buildingType;
                break;
            }
        }
    }

    private void HandlePointerPressed(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        // Vérifier les clics sur les boutons
        foreach (var (rect, buildingType) in _hoverRects)
        {
            if (rect.Contains(e.Position.X, e.Position.Y))
            {
                _cityBuildingService.TryExecuteSelectedCityBuildingAction(buildingType);
                break;
            }
        }
    }

    public void Dispose()
    {
        _inputService.PointerMoved -= HandlePointerMoved;
        _inputService.PointerPressed -= HandlePointerPressed;
    }
}
