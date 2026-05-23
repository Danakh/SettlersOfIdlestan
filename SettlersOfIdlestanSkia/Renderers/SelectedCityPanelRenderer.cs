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
    private BuildingType? _hoveredBuildingType = null;
    private const float PanelWidth = 260;
    private const float RowHeight = 36;
    private const float Padding = 10;
    private Dictionary<SKRect, BuildingType> _btnRects = new Dictionary<SKRect, BuildingType>();
    private Dictionary<SKRect, BuildingType> _hoverRects = new Dictionary<SKRect, BuildingType>();
    public float ReservedBottomHeight { get; set; }
    public bool IsInputEnabled { get; set; } = true;

    public void Close()
    {
        _cityBuildingService.ClearSelectedCity();
        _hoveredBuildingType = null;
        _btnRects.Clear();
        _hoverRects.Clear();
    }

    public SelectedCityPanelRenderer( CityBuildingService cityBuildingService, ILocalizationService localization, InputHandlingService inputService)
    {
        _cityBuildingService = cityBuildingService;
        _inputService = inputService;
        _localization = localization;
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

        _btnRects.Clear();
        _hoverRects.Clear();

        var buildings = _cityBuildingService.SelectedCityBuildingsAndBuildables().ToList();
        int buildingCount = buildings.Count;
        if (buildingCount == 0)
            return;

        // Fond du panneau
        float maxPanelHeight = Math.Max(0, _canvasSize.Height - panelY - ReservedBottomHeight - 10);
        int visibleBuildingCount = Math.Min(buildingCount, Math.Max(0, (int)((maxPanelHeight - 2 * Padding) / RowHeight)));
        if (visibleBuildingCount == 0)
            return;

        var visibleBuildings = buildings.Take(visibleBuildingCount).ToList();
        float panelHeight = visibleBuildingCount * RowHeight + 2 * Padding;
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, bgPaint);
        canvas.DrawRoundRect(panelX, panelY, PanelWidth, panelHeight, 12, 12, borderPaint);

        foreach (var (building, index) in visibleBuildings.Select((item, i) => (item, i)))
        {
            var isBuilt = building.Level > 0;
            var canBuild = building.Level == 0;
            var canBuildOrUpgrade = _cityBuildingService.CanBuildOrUpgrade(building);
            var isAtMaxLevel = _cityBuildingService.IsAtMaxLevel(building);
            var yRow = y + index * RowHeight;
            var label = _localization.Get(building.NameKey) + (isBuilt ? $" (Niv {building.Level})" : "");
            canvas.DrawText(label, panelX + Padding, yRow + 18, font15, textPaint);

            // Afficher le coût des ressources sous le nom
            var cost = isBuilt ? building.GetUpgradeCost(building.Level + 1) : building.GetBuildCost();
            if (cost.Count > 0)
            {
                var costText = SkiaTextUtils.computeCostString(_localization, cost);
                canvas.DrawText(costText, panelX + Padding, yRow + 30, font10, costTextPaint);
            }

            // Bouton action
            if (canBuild || isBuilt)
            {
                var btnText = isBuilt ? _localization.Get("action_upgrade") : _localization.Get("action_build");
                var btnWidth = 90;
                var btnHeight = 26;
                var btnX = panelX + PanelWidth - btnWidth - Padding;
                var btnY = yRow + 6;
                
                SKColor btnColor;
                SKColor btnTextColor;
                bool isButtonEnabled = canBuildOrUpgrade;

                if (isAtMaxLevel)
                {
                    btnColor = new SKColor(100, 100, 100, 200);
                    btnTextColor = new SKColor(150, 150, 150, 255);
                    btnText = _localization.Get("action_maxlevel");
                }
                else if (!isButtonEnabled)
                {
                    btnColor = new SKColor(100, 100, 100, 200);
                    btnTextColor = new SKColor(150, 150, 150, 255);
                }
                else
                {
                    btnColor = isBuilt ? new SKColor(46, 125, 50, 255) : new SKColor(21, 101, 192, 255);
                    btnTextColor = SKColors.White;
                }

                using var btnPaint = new SKPaint 
                { 
                    Color = btnColor, 
                    Style = SKPaintStyle.Fill, 
                    IsAntialias = true 
                };
                canvas.DrawRoundRect(btnX, btnY, btnWidth, btnHeight, 7, 7, btnPaint);
                
                using var btnTextPaint = new SKPaint { Color = btnTextColor, IsAntialias = true };
                float btnCenterX = btnX + btnWidth / 2;
                float btnCenterY = btnY + btnHeight / 2 + 6;
                canvas.DrawText(btnText, btnCenterX, btnCenterY, SKTextAlign.Center, font12, btnTextPaint);

                // Stocker les informations du bouton pour la détection de clic
                var btnRect = new SKRect(btnX, btnY, btnX + btnWidth, btnY + btnHeight);
                _btnRects[btnRect] = building.Type;
                var hoverRect = new SKRect(panelX, btnY, panelX + PanelWidth, btnY + btnHeight);
                _hoverRects[hoverRect] = building.Type;
            }
        }

        // Afficher le tooltip (description) si on survole un bâtiment
        if (_hoveredBuildingType.HasValue)
        {
            var hoveredBuilding = visibleBuildings.FirstOrDefault(b => b.Type == _hoveredBuildingType.Value);
            if (hoveredBuilding != null)
            {
                var buildingName = _localization.Get(hoveredBuilding.NameKey);
                var levelDescriptionKey = hoveredBuilding.DescriptionKey + "_" + hoveredBuilding.Level;
                var description = _localization.Get(levelDescriptionKey);
                if (levelDescriptionKey == description)
                    description = _localization.Get(hoveredBuilding.DescriptionKey);
                var cost = hoveredBuilding.Level == 0 ? hoveredBuilding.GetBuildCost() : hoveredBuilding.GetUpgradeCost(hoveredBuilding.Level + 1);
                var costDescription = SkiaTextUtils.computeCostString(_localization, cost);
                TooltipRenderUtils.DrawTooltip(canvas, _canvasSize, _lastPointerPosition, new string[] { buildingName, "", description, "", costDescription }, font10);
            }
        }
    }

    private void HandlePointerMoved(object? sender, SettlersOfIdlestanSkia.Services.PointerEventArgs e)
    {
        if (!IsInputEnabled)
        {
            _hoveredBuildingType = null;
            return;
        }

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
        if (!IsInputEnabled || e.Button != PointerButton.Left)
            return;

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
